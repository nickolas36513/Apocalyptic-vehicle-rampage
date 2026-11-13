using System;
using UnityEngine;
using Unity.Profiling;

[RequireComponent(typeof(Rigidbody))]
public sealed class VehicleController : MonoBehaviour
{
    private const float SteeringFullResponseSpeed = 1.6f;
    private const float SteeringAngularAcceleration = 11f;
    private const float SteeringLookAheadSeconds = 0.1f;
    private const float MinimumDriftSpeed = 2f;
    private const float DriftRecoveryRange = 10f;
    private const float HandbrakeDriftAllowance = 10f;
    private const float MinimumGroundNormalY = 0.5f;
    private const float GripRate = 8f;

    private static readonly ProfilerMarker UpdateMarker =
        new ProfilerMarker("VehicleController.Update");

    private static readonly ProfilerMarker PhysicsMarker =
        new ProfilerMarker("VehicleController.FixedUpdate");

    private static readonly ProfilerMarker WheelVisualMarker =
        new ProfilerMarker("VehicleController.WheelVisuals");

    [Serializable]
    public sealed class WheelVisual
    {
        [Header("Wheel References")]
        [Tooltip("Independent pivot located at this wheel's steering joint.")]
        public Transform steeringPivot;

        [Tooltip("Wheel mesh, which must be a child of the steering pivot.")]
        public Transform wheelMesh;

        [Header("Wheel Options")]
        public bool steer;
        public bool roll = true;
        public bool invertSteering;
        public bool invertRoll;

        [Header("Wheel Dimensions")]
        [Min(0.01f)]
        public float radius = 0.35f;

        [Header("Local Axes")]
        [Tooltip("Local steering axis of the pivot. Y is typical.")]
        public Vector3 steeringAxis = Vector3.up;

        [Tooltip("Local rolling axis of the wheel mesh. X for your model.")]
        public Vector3 rollingAxis = Vector3.right;

        [NonSerialized] public Quaternion initialPivotRotation;
        [NonSerialized] public Quaternion initialMeshRotation;
        [NonSerialized] public float rollAngle;
        [NonSerialized] public bool initialized;
    }

    [Header("Driving")]
    [SerializeField] private float acceleration = 8f;
    [SerializeField] private float deceleration = 2.5f;
    [SerializeField] private float maxSpeed = 25f;
    [SerializeField] private float reverseSpeed = 8f;
    [SerializeField] private float brakeStrength = 30f;

    [Header("Steering Physics")]
    [SerializeField] private float turnSpeed = 75f;
    [SerializeField, Range(0f, 1f)]
    private float minSteeringFactor = 0.35f;
    [SerializeField] private float steeringResponse = 6f;

    [Header("Input Smoothing")]
    [SerializeField] private float throttleResponse = 5f;

    [Header("Grip and Drift")]
    [Tooltip("How quickly the vehicle sheds sideways velocity on the ground.")]
    [SerializeField, Range(0f, 1f)]
    private float lateralGrip = 0.8f;
    [SerializeField, Range(0f, 1f)]
    private float handbrakeGrip = 0.2f;
    [Tooltip("Grip reduction when cornering at speed. Zero keeps full grip.")]
    [SerializeField, Range(0f, 0.6f)]
    private float cornerDrift = 0.36f;
    [Tooltip("Drift angle in degrees at which normal grip starts returning.")]
    [SerializeField, Range(5f, 30f)]
    private float driftRecoveryAngle = 14f;

    [Header("Stability")]
    [SerializeField] private bool keepUpright = true;
    [SerializeField] private float uprightStrength = 5f;
    [Tooltip("Outward roll acceleration during fast corners. Zero disables the extra lean.")]
    [SerializeField, Range(0f, 25f)]
    private float cornerLean = 14f;

    [Header("Wheel Visuals")]
    [SerializeField] private WheelVisual[] wheels = Array.Empty<WheelVisual>();
    [SerializeField] private float maxVisualSteerAngle = 30f;

    private Rigidbody rb;
    private WheelCollider[] suspensionWheels;

    private float throttleInput;
    private float steeringInput;
    private float throttle;
    private float steering;
    private float forwardSpeed;

    private bool handbrake;
    private bool brake;
    private bool keyboardInputEnabled = true;

    public float ForwardSpeed => forwardSpeed;
    public float Steering => steering;
    public float Throttle => throttle;
    public Rigidbody Rigidbody => rb;
    public bool IsGrounded { get; private set; }
    /// <summary>Signed ground travel angle in degrees relative to the steered direction.</summary>
    public float DriftAngle { get; private set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.interpolation = RigidbodyInterpolation.Interpolate;

        CacheSuspensionWheels();
        CacheWheelVisuals();
    }

    private void Update()
    {
        using (UpdateMarker.Auto())
        {
            if (keyboardInputEnabled)
                ReadKeyboardInput();
        }
    }

    private void FixedUpdate()
    {
        using (PhysicsMarker.Auto())
        {
            DriftAngle = 0f;
            if (rb.isKinematic)
            {
                IsGrounded = false;
                forwardSpeed = 0f;
                return;
            }

            float dt = Time.fixedDeltaTime;
            throttle = Mathf.MoveTowards(throttle, throttleInput, throttleResponse * dt);
            steering = Mathf.MoveTowards(steering, steeringInput, steeringResponse * dt);

            Vector3 velocity = rb.linearVelocity;
            Vector3 bodyForward = rb.rotation * Vector3.forward;
            forwardSpeed = Vector3.Dot(velocity, bodyForward);

            IsGrounded = TryGetGroundNormal(out Vector3 up);
            if (!IsGrounded)
                return;

            Vector3 forward = Vector3.ProjectOnPlane(bodyForward, up);
            if (forward.sqrMagnitude < 0.0001f)
                return;

            forward.Normalize();
            Vector3 right = Vector3.Cross(up, forward);
            float longitudinalSpeed = Vector3.Dot(velocity, forward);
            float lateralSpeed = Vector3.Dot(velocity, right);
            forwardSpeed = CalculateDriveSpeed(longitudinalSpeed, dt);
            float desiredYawRate = CalculateDesiredYawRate();

            float cornering = steering * Mathf.InverseLerp(3f, 14f, Mathf.Abs(longitudinalSpeed));
            float grip = CalculateCornerGrip(longitudinalSpeed, lateralSpeed, desiredYawRate, cornering);
            float lateralCorrection = lateralSpeed * (1f - Mathf.Exp(-GripRate * grip * dt));
            Vector3 correction = forward * (forwardSpeed - longitudinalSpeed) -
                right * lateralCorrection;
            if (correction.sqrMagnitude > 0.000001f)
                rb.linearVelocity = velocity + correction;

            ApplySteering(up, desiredYawRate, dt);
            ApplyUprightStability(up, forward, cornering);
        }
    }

    private void LateUpdate()
    {
        UpdateWheelVisuals();
    }

    private void ReadKeyboardInput()
    {
        SetInput(
            Input.GetAxisRaw("Vertical"),
            Input.GetAxisRaw("Horizontal"),
            Input.GetKey(KeyCode.Space),
            Input.GetKey(KeyCode.LeftShift) ||
            Input.GetKey(KeyCode.RightShift));
    }

    private void CacheSuspensionWheels()
    {
        suspensionWheels = GetComponentsInChildren<WheelCollider>();
        foreach (WheelCollider wheel in suspensionWheels)
        {
            if (wheel.attachedRigidbody != rb)
                continue;

            // The wheels handle suspension. This controller owns tire grip.
            WheelFrictionCurve forwardFriction = wheel.forwardFriction;
            forwardFriction.stiffness = 0f;
            wheel.forwardFriction = forwardFriction;

            WheelFrictionCurve sidewaysFriction = wheel.sidewaysFriction;
            sidewaysFriction.stiffness = 0f;
            wheel.sidewaysFriction = sidewaysFriction;
        }
    }

    private bool TryGetGroundNormal(out Vector3 normal)
    {
        normal = Vector3.zero;
        foreach (WheelCollider wheel in suspensionWheels)
        {
            if (wheel != null && wheel.enabled && wheel.gameObject.activeInHierarchy &&
                wheel.attachedRigidbody == rb &&
                wheel.GetGroundHit(out WheelHit hit) && hit.normal.y >= MinimumGroundNormalY)
            {
                normal += hit.normal;
            }
        }

        if (normal.sqrMagnitude < 0.0001f)
            return false;

        normal.Normalize();
        return true;
    }

    private float CalculateDriveSpeed(float speed, float dt)
    {
        bool changingDirection = Mathf.Abs(speed) > 0.15f && speed * throttleInput < -0.01f;
        if (brake || changingDirection)
            return Mathf.MoveTowards(speed, 0f, brakeStrength * dt);

        // Cut drive torque while the handbrake is held, even with throttle input.
        if (handbrake)
            return Mathf.MoveTowards(speed, 0f, brakeStrength * 0.35f * dt);

        if (Mathf.Abs(throttle) < 0.01f || throttle * throttleInput <= 0f)
            return Mathf.MoveTowards(speed, 0f, deceleration * dt);

        float speedLimit = throttle > 0f ? maxSpeed : reverseSpeed;
        float speedFraction = Mathf.Clamp01(Mathf.Abs(speed) / Mathf.Max(speedLimit, 0.01f));
        float pull = acceleration * Mathf.Abs(throttle) * Mathf.Lerp(1f, 0.35f, speedFraction);
        return Mathf.MoveTowards(speed, throttle * speedLimit, pull * dt);
    }

    private float CalculateCornerGrip(
        float longitudinalSpeed, float lateralSpeed, float desiredYawRate, float cornering)
    {
        float grip = handbrake
            ? handbrakeGrip
            : lateralGrip * (1f - cornerDrift * Mathf.Abs(cornering));

        float planarSpeedSquared = longitudinalSpeed * longitudinalSpeed + lateralSpeed * lateralSpeed;
        if (planarSpeedSquared < MinimumDriftSpeed * MinimumDriftSpeed)
            return grip;

        // Measure both angles in the direction of travel so reversing still has a zero-slip heading.
        float travelSign = Mathf.Sign(longitudinalSpeed);
        float travelAngle = Mathf.Atan2(lateralSpeed * travelSign, Mathf.Abs(longitudinalSpeed)) * Mathf.Rad2Deg;
        float desiredAngle = desiredYawRate * SteeringLookAheadSeconds * Mathf.Rad2Deg;
        DriftAngle = Mathf.DeltaAngle(desiredAngle, travelAngle);

        float recoveryStart = driftRecoveryAngle + (handbrake ? HandbrakeDriftAllowance : 0f);
        float recovery = Mathf.InverseLerp(recoveryStart, recoveryStart + DriftRecoveryRange, Mathf.Abs(DriftAngle));
        return Mathf.Lerp(grip, Mathf.Max(grip, lateralGrip), recovery);
    }

    private float CalculateDesiredYawRate()
    {
        float speed = Mathf.Abs(forwardSpeed);
        float lowSpeedFactor = Mathf.Clamp01(speed / SteeringFullResponseSpeed);
        float highSpeedFactor = Mathf.Lerp(
            1f, minSteeringFactor, Mathf.Clamp01(speed / Mathf.Max(maxSpeed, 0.01f)));
        return steering * turnSpeed * Mathf.Deg2Rad *
            lowSpeedFactor * highSpeedFactor * Mathf.Sign(forwardSpeed);
    }

    private void ApplySteering(Vector3 up, float desiredYawRate, float dt)
    {
        Vector3 angularVelocity = rb.angularVelocity;
        float yawRate = Vector3.Dot(angularVelocity, up);
        float nextYawRate = Mathf.MoveTowards(yawRate, desiredYawRate, SteeringAngularAcceleration * dt);
        if (Mathf.Abs(nextYawRate - yawRate) > 0.0001f)
            rb.angularVelocity = angularVelocity + up * (nextYawRate - yawRate);
    }

    private void ApplyUprightStability(Vector3 up, Vector3 forward, float cornering)
    {
        Vector3 currentUp = rb.rotation * Vector3.up;
        Vector3 torque = Vector3.zero;
        if (keepUpright && uprightStrength > 0f)
        {
            Vector3 correctionAxis = Vector3.Cross(currentUp, up);
            Vector3 tiltVelocity = Vector3.ProjectOnPlane(rb.angularVelocity, up);
            torque = correctionAxis * uprightStrength -
                tiltVelocity * (2f * Mathf.Sqrt(uprightStrength));
        }

        // Lean into the suspension on the outside of a turn.
        if (Vector3.Dot(currentUp, up) > 0.9f)
            torque += forward * (cornering * cornerLean);

        if (torque.sqrMagnitude > 0.0001f)
            rb.AddTorque(torque, ForceMode.Acceleration);
    }

    // ---------------------------------------------------------
    // Wheel visual setup
    // ---------------------------------------------------------

    private void CacheWheelVisuals()
    {
        if (wheels == null)
            return;

        foreach (WheelVisual wheel in wheels)
        {
            if (wheel == null)
                continue;

            if (wheel.steeringPivot != null)
            {
                wheel.initialPivotRotation =
                    wheel.steeringPivot.localRotation;
            }

            if (wheel.wheelMesh != null)
            {
                wheel.initialMeshRotation =
                    wheel.wheelMesh.localRotation;
            }

            wheel.rollAngle = 0f;
            wheel.initialized = true;
        }
    }

    private void UpdateWheelVisuals()
    {
        using (WheelVisualMarker.Auto())
        {
            if (wheels == null)
                return;

            foreach (WheelVisual wheel in wheels)
            {
                if (wheel == null || !wheel.initialized)
                    continue;

                UpdateWheelSteering(wheel);
                UpdateWheelRolling(wheel);
            }
        }
    }

    private void UpdateWheelSteering(WheelVisual wheel)
    {
        if (!wheel.steer || wheel.steeringPivot == null)
            return;

        Vector3 axis = wheel.steeringAxis;

        if (axis.sqrMagnitude < 0.0001f)
            return;

        axis.Normalize();

        float direction = wheel.invertSteering ? -1f : 1f;
        float angle = steering * maxVisualSteerAngle * direction;

        // Rotate this wheel around its OWN pivot, not the shared axle.
        wheel.steeringPivot.localRotation =
            wheel.initialPivotRotation *
            Quaternion.AngleAxis(angle, axis);
    }

    private void UpdateWheelRolling(WheelVisual wheel)
    {
        if (!wheel.roll || wheel.wheelMesh == null)
            return;

        Vector3 axis = wheel.rollingAxis;

        if (axis.sqrMagnitude < 0.0001f)
            return;

        axis.Normalize();

        float radius = Mathf.Max(wheel.radius, 0.01f);
        float direction = wheel.invertRoll ? -1f : 1f;

        wheel.rollAngle +=
            (forwardSpeed / radius) *
            Mathf.Rad2Deg *
            Time.deltaTime *
            direction;

        wheel.rollAngle = Mathf.Repeat(wheel.rollAngle, 360f);

        // Roll around the wheel mesh's local axis.
        wheel.wheelMesh.localRotation =
            wheel.initialMeshRotation *
            Quaternion.AngleAxis(wheel.rollAngle, axis);
    }

    // ---------------------------------------------------------
    // Public input API: usable by keyboard, joystick or UI
    // ---------------------------------------------------------

    public void SetThrottle(float value)
    {
        throttleInput = Mathf.Clamp(value, -1f, 1f);
    }

    public void SetSteering(float value)
    {
        steeringInput = Mathf.Clamp(value, -1f, 1f);
    }

    public void SetInput(
        float throttleValue,
        float steeringValue,
        bool handbrakeValue = false,
        bool brakeValue = false)
    {
        throttleInput = Mathf.Clamp(throttleValue, -1f, 1f);
        steeringInput = Mathf.Clamp(steeringValue, -1f, 1f);
        handbrake = handbrakeValue;
        brake = brakeValue;
    }

    public void StopInput()
    {
        throttleInput = 0f;
        steeringInput = 0f;
        throttle = 0f;
        steering = 0f;
        handbrake = false;
        brake = false;
    }

    public void SetKeyboardInputEnabled(bool enabled)
    {
        keyboardInputEnabled = enabled;

        if (!enabled)
            StopInput();
    }

    private void OnDisable()
    {
        StopInput();
        IsGrounded = false;
        DriftAngle = 0f;
        forwardSpeed = 0f;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        acceleration = Mathf.Max(0f, acceleration);
        deceleration = Mathf.Max(0f, deceleration);
        maxSpeed = Mathf.Max(0f, maxSpeed);
        reverseSpeed = Mathf.Max(0f, reverseSpeed);
        brakeStrength = Mathf.Max(0f, brakeStrength);
        turnSpeed = Mathf.Max(0f, turnSpeed);
        steeringResponse = Mathf.Max(0f, steeringResponse);
        throttleResponse = Mathf.Max(0f, throttleResponse);
        uprightStrength = Mathf.Max(0f, uprightStrength);
        maxVisualSteerAngle = Mathf.Max(0f, maxVisualSteerAngle);
    }
#endif
}
