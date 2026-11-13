

using System.Collections.Generic;
using UnityEngine;

using Unity.Profiling;

[DisallowMultipleComponent]

[RequireComponent(typeof(Camera))]

public sealed class CameraController : MonoBehaviour

{

    private static readonly HashSet<int> GarageUIPointerIds = new HashSet<int>();

    private static readonly ProfilerMarker FollowMarker =

        new ProfilerMarker("ChaseCam.LateUpdate");

    [Header("Target and Follow")]

    [SerializeField] private Transform target;

    [SerializeField] private Rigidbody targetRigidbody;

    [Header("01 - High-Angle Camera")]

    [Tooltip("Horizontal offset behind the vehicle. Height and look-ahead determine the pitch.")]
    [SerializeField, Min(0.1f)] private float distance = 4.5f;

    [SerializeField, Min(0f)] private float height = 20f;

    [SerializeField] private float targetHeight = 1.2f;

    [Tooltip("Distance ahead of the vehicle to aim the gameplay camera.")]
    [SerializeField, Min(0f)] private float lookAheadDistance = 3f;

    [SerializeField]

    private bool ignoreTargetPitchAndRoll = true;

    [Header("02 - Position Follow")]

    [Tooltip("Enable to smooth the camera's position. Disable for immediate following.")]

    [SerializeField] private bool usePositionSmoothing = true;

    [SerializeField, Min(0.001f)]

    private float positionSmoothTime = 0.18f;

    [Header("03 - Rotation Follow")]

    [Tooltip("Enable to limit how quickly the camera rotates toward its desired view.")]

    [SerializeField] private bool useRotationSmoothing = true;

    [SerializeField, Min(0f)]

    private float cameraRotationSpeed = 180f;

    [Tooltip("Independently smooth the camera's heading behind the vehicle.")]

    [SerializeField] private bool useHeadingDamping = true;

    [SerializeField, Min(0f)]

    private float headingDamping = 3.3f;

    [Header("04 - Acceleration / Braking Screen Effect")]

    [SerializeField] private bool useDynamicScreenPosition = true;

    [SerializeField, Range(0.1f, 0.9f)]

    private float screenCenterY = 0.52f;

    [SerializeField, Range(0f, 0.4f)]

    private float maxVerticalScreenOffset = 0.035f;

    [SerializeField, Range(0f, 0.3f)]

    private float accelerationScreenOffset = 0.022f;

    [SerializeField, Range(0f, 0.3f)]

    private float brakingScreenOffset = 0.016f;

    [SerializeField, Min(0.1f)]

    private float accelerationForMaxOffset = 8f;

    [Tooltip("Smooth the screen-position effect independently.")]

    [SerializeField] private bool smoothScreenPosition = true;

    [SerializeField, Min(0f)]

    private float verticalRepositionSmoothTime = 0.3f;

    [Header("05 - Speed-Based Zoom")]

    [SerializeField] private bool useSpeedBasedZoom = true;

    [SerializeField, Min(0f)]

    private float extraDistanceAtMaxSpeed = 1f;

    [SerializeField, Min(0.1f)]

    private float speedForMaxDistance = 22f;

    [Tooltip("Smooth zoom changes independently of the zoom effect toggle.")]

    [SerializeField] private bool smoothZoom = true;

    [SerializeField, Min(0f)]

    private float accelerationZoomTime = 0.25f;

    [SerializeField, Min(0f)]

    private float decelerationZoomTime = 0.5f;

    [Header("06 - Steering Lateral Sway")]

    [SerializeField] private bool useSteeringSway = true;

    [SerializeField, Min(0f)]

    private float lateralSway = 0.45f;

    [SerializeField] private bool smoothSway = true;

    [SerializeField, Min(0f)]

    private float swaySmoothTime = 0.2f;

    [Header("07 - Steering Camera Roll")]

    [SerializeField] private bool useSteeringRoll = true;

    [SerializeField, Min(0f)]

    private float steeringRoll = 1.3f;

    [SerializeField] private bool smoothRoll = true;

    [SerializeField, Min(0f)]

    private float rollSmoothTime = 0.2f;

    [Header("08 - Engine Vibration")]

    [SerializeField] private bool useEngineVibration = false;

    [SerializeField, Min(0f)]

    private float engineVibration = 0.025f;

    [SerializeField, Min(0f)]

    private float vibrationFrequency = 18f;

    [Header("09 - Impact Position Shake")]

    [SerializeField] private bool useImpactPositionShake = true;

    [SerializeField, Min(0f)]

    private float impactPositionShake = 0.08f;

    [Header("10 - Impact Rotation Shake")]

    [SerializeField] private bool useImpactRotationShake = true;

    [SerializeField, Min(0f)]

    private float impactRotationShake = 0.65f;

    [SerializeField, Min(0.01f)]

    private float impactDuration = 0.18f;

    [Header("11 - Dynamic Field Of View")]

    [SerializeField] private bool useDynamicFOV = true;

    [SerializeField, Range(30f, 100f)]

    private float baseFOV = 50f;

    [SerializeField, Range(30f, 110f)]

    private float maxSpeedFOV = 53f;

    [SerializeField] private bool smoothFOV = true;

    [SerializeField, Min(0f)]

    private float fovSmoothTime = 0.3f;

    private Transform cachedTransform;

    private Camera cachedCamera;

    private float currentYaw;

    private float currentDistance;

    private float distanceVelocity;

    private Vector3 positionVelocity;

    private Quaternion smoothedRotation;

    private float currentSway;

    private float swayVelocity;

    private float currentRoll;

    private float rollVelocity;

    private float currentFOV;

    private float fovVelocity;

    private float currentImpact;

    private float impactTimer;

    private float shakePhase;

    private float dynamicScreenY;

    private float dynamicScreenYVelocity;

    private Vector3 previousVelocity;
    private Vector3 planarTargetVelocity;

    private Vector3 previousForward;

    private float currentSpeed;

    private float currentAcceleration;

    private float currentTurnRate;
    private VehicleController targetVehicleController;

    private bool initialized;


    // =========================================================
    // CAMERA MODE
    // =========================================================

    private enum CameraMode
    {
        Disabled,
        Garage,
        MainMenu,
        Gameplay
    }

    private CameraMode cameraMode = CameraMode.Disabled;

    // =========================================================
    // GARAGE ORBIT
    // =========================================================

    [Header("12 - Garage Orbit")]
    [Tooltip("Start in garage mode when a Garage Focus Target is assigned.")]
    [SerializeField] private bool startInGarage = true;

    [Tooltip("Usually the InspectionPivot placed around the vehicle's visual center.")]
    [SerializeField] private Transform garageFocusTarget;

    [Tooltip("Fixed radius of the horizontal orbit around the inspection pivot.")]
    [SerializeField, Min(0.1f)] private float garageDistance = 7f;

    [Tooltip("Degrees of orbit rotation per horizontal screen pixel.")]
    [SerializeField, Min(0f)] private float garageDragSensitivity = 0.25f;

    private Transform garageFocus;
    private float garagePreviewCenter = 0.5f;
    private float garageYaw;
    private bool isDragging;
    private float previousMouseX;

    [Header("Main Menu Front View")]
    [Tooltip("Optional scene transform that sets the Main Menu camera position and rotation.")]
    [SerializeField] private Transform menuCameraHolder;

    [Tooltip("Fallback distance when a menu camera holder is not assigned.")]
    [SerializeField, Min(0.1f)] private float menuDistance = 8f;
    [SerializeField, Min(0f)] private float menuHeight = 2.2f;
    [Tooltip("Shifts the vehicle toward the right of the menu backdrop.")]
    [SerializeField] private float menuFramingOffset = 0.8f;

    // =========================================================
    // MODE TRANSITIONS
    // =========================================================

    [Header("13 - Camera Mode Transition")]
    [SerializeField, Min(0f)] private float transitionDuration = 0.65f;

    private bool transitionActive;
    private float transitionElapsed;
    private Vector3 transitionStartOffset;
    private float? transitionEndAngle;
    private Quaternion transitionStartRotation;
    private float transitionStartFOV;


    public Transform Target => target;

    public float CurrentDistance => currentDistance;

    public bool IsInGarageMode => cameraMode == CameraMode.Garage;
    public bool IsInGameplayMode => cameraMode == CameraMode.Gameplay;
    public Transform GarageFocusTarget => garageFocus != null ? garageFocus : garageFocusTarget;

    /// <summary>UI Toolkit screens report pointers captured by interactive controls.</summary>
    public static void SetGarageUIPointerBlocked(int pointerId, bool blocked)
    {
        if (blocked)
            GarageUIPointerIds.Add(pointerId);
        else
            GarageUIPointerIds.Remove(pointerId);
    }

    public static void ClearGarageUIInputBlockers() => GarageUIPointerIds.Clear();

    private void Awake()

    {

        cachedTransform = transform;

        cachedCamera = GetComponent<Camera>();

        currentFOV = baseFOV;

        dynamicScreenY = screenCenterY;

        currentDistance = distance;

    }

    private void Start()
    {
        if (startInGarage && garageFocusTarget != null)
        {
            EnterGarage(garageFocusTarget);
            return;
        }

        if (target != null)
        {
            targetRigidbody = target.GetComponent<Rigidbody>();
            cameraMode = CameraMode.Gameplay;
            SnapBehindTarget();
        }
    }

    private void LateUpdate()
    {
        using (FollowMarker.Auto())
        {
            if (cameraMode == CameraMode.Garage)
            {
                if (garageFocus == null)
                    return;

                UpdateGarageInput();
                ApplyGarageCameraPose();
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f || cameraMode != CameraMode.Gameplay || target == null)
                return;

            if (!initialized)
                InitializeCamera();

            UpdateVehicleTelemetry(dt);
            UpdateHeading(dt);
            UpdateScreenPosition(dt);
            UpdateZoom(dt);
            UpdateSway(dt);
            UpdateRoll(dt);
            UpdateImpact(dt);
            UpdateFOV(dt);
            ApplyCameraPose(dt);
        }
    }

    private void UpdateGarageInput()
    {
        if (GarageUIPointerIds.Count > 0 || Input.touchCount > 1)
        {
            isDragging = false;
            return;
        }

        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
                isDragging = true;
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                isDragging = false;
            else if (touch.phase == TouchPhase.Moved && isDragging)
                RotateGarage(touch.deltaPosition.x);

            return;
        }

        if (!Input.GetMouseButton(0))
        {
            isDragging = false;
            return;
        }

        float mouseX = Input.mousePosition.x;
        if (Input.GetMouseButtonDown(0))
        {
            isDragging = true;
            previousMouseX = mouseX;
        }

        if (isDragging)
        {
            RotateGarage(mouseX - previousMouseX);
            previousMouseX = mouseX;
        }
    }

    private void RotateGarage(float horizontalPixels)
    {
        garageYaw = Mathf.Repeat(garageYaw - horizontalPixels * garageDragSensitivity, 360f);
    }

    private void ApplyGarageCameraPose()
    {
        Quaternion rotation = Quaternion.Euler(0f, garageYaw, 0f);
        Vector3 position = garageFocus.position - rotation * Vector3.forward * garageDistance;
        if (cachedCamera != null && !cachedCamera.orthographic)
        {
            float horizontalSlope = Mathf.Tan(cachedCamera.fieldOfView * Mathf.Deg2Rad * 0.5f) * cachedCamera.aspect;
            float framingYaw = Mathf.Atan((1f - 2f * garagePreviewCenter) * horizontalSlope) * Mathf.Rad2Deg;
            rotation *= Quaternion.Euler(0f, framingYaw, 0f);
        }
        cachedTransform.SetPositionAndRotation(position, rotation);
    }

    public void SetGaragePreviewCenter(float normalizedX)
    {
        garagePreviewCenter = Mathf.Clamp(normalizedX, 0.25f, 0.75f);
    }

    private void BeginTransition(Transform gameplayTarget)
    {
        if (transitionDuration <= 0f)
        {
            transitionActive = false;
            return;
        }

        transitionStartOffset = cachedTransform.position - gameplayTarget.position;
        transitionEndAngle = null;
        transitionStartRotation = cachedTransform.rotation;
        transitionStartFOV = cachedCamera.fieldOfView;
        transitionElapsed = 0f;
        transitionActive = true;
    }

    private void ApplyTransitionPose(
        Vector3 desiredPosition,
        Quaternion desiredRotation,
        float desiredFOV,
        float dt)
    {
        transitionElapsed += dt;

        float normalizedTime = Mathf.Clamp01(
            transitionElapsed / Mathf.Max(0.0001f, transitionDuration));

        float blend = Mathf.SmoothStep(0f, 1f, normalizedTime);

        // Blend around the vehicle so a front-to-rear handoff clears the body.
        Vector3 targetPosition = target.position;
        Vector3 endOffset = desiredPosition - targetPosition;
        Vector3 startFlat = Vector3.ProjectOnPlane(transitionStartOffset, Vector3.up);
        Vector3 endFlat = Vector3.ProjectOnPlane(endOffset, Vector3.up);
        if (startFlat.sqrMagnitude > 0.0001f && endFlat.sqrMagnitude > 0.0001f)
        {
            float startAngle = Mathf.Atan2(startFlat.x, startFlat.z) * Mathf.Rad2Deg;
            float desiredAngle = Mathf.Atan2(endFlat.x, endFlat.z) * Mathf.Rad2Deg;
            float previousEndAngle = transitionEndAngle ?? startAngle;
            float endAngle = previousEndAngle + Mathf.DeltaAngle(previousEndAngle, desiredAngle);
            transitionEndAngle = endAngle;
            Quaternion startHeading = Quaternion.Euler(0f, startAngle, 0f);
            Quaternion endHeading = Quaternion.Euler(0f, endAngle, 0f);
            Quaternion heading = Quaternion.Euler(
                0f, Mathf.Lerp(startAngle, endAngle, blend), 0f);
            float radius = Mathf.Lerp(startFlat.magnitude, endFlat.magnitude, blend);
            float elevation = Mathf.Lerp(transitionStartOffset.y, endOffset.y, blend);

            cachedTransform.position = targetPosition + heading * Vector3.forward * radius +
                Vector3.up * elevation;
            cachedTransform.rotation = heading * Quaternion.Slerp(
                Quaternion.Inverse(startHeading) * transitionStartRotation,
                Quaternion.Inverse(endHeading) * desiredRotation, blend);
        }
        else
        {
            cachedTransform.position = targetPosition + Vector3.Lerp(
                transitionStartOffset, endOffset, blend);
            cachedTransform.rotation = Quaternion.Slerp(
                transitionStartRotation, desiredRotation, blend);
        }

        if (!cachedCamera.orthographic)
        {
            cachedCamera.fieldOfView = Mathf.Lerp(
                transitionStartFOV,
                desiredFOV,
                blend);
        }

        if (normalizedTime >= 1f)
        {
            transitionActive = false;
            positionVelocity = Vector3.zero;
        }
    }

    private void UpdateVehicleTelemetry(float dt)

    {

        Vector3 velocity = targetRigidbody != null

            ? targetRigidbody.linearVelocity

            : Vector3.zero;

        float previousSpeed = currentSpeed;
        planarTargetVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
        currentSpeed = planarTargetVelocity.magnitude;

        currentAcceleration = (currentSpeed - previousSpeed) / dt;

        if (targetRigidbody != null)

        {

            Vector3 velocityDelta = velocity - previousVelocity;

            if (initialized &&

                (useImpactPositionShake || useImpactRotationShake))

            {

                float velocityChange = velocityDelta.magnitude;

                if (velocityChange > 5f)

                {

                    AddImpact(

                        Mathf.Clamp01(velocityChange / 15f));

                }

            }

        }

        previousVelocity = velocity;

        Vector3 forward = GetTargetForward();

        if (initialized)

        {

            currentTurnRate = Vector3.SignedAngle(

                previousForward,

                forward,

                Vector3.up) / dt;

        }

        previousForward = forward;

    }

    private void UpdateHeading(float dt)

    {

        Vector3 forward = GetTargetForward();

        float targetYaw = Quaternion.LookRotation(

            forward,

            Vector3.up).eulerAngles.y;

        if (!useHeadingDamping || headingDamping <= 0f)

        {

            currentYaw = targetYaw;

            return;

        }

        float blend = 1f - Mathf.Exp(-headingDamping * dt);

        currentYaw = Mathf.LerpAngle(

            currentYaw,

            targetYaw,

            blend);

    }

    private void UpdateScreenPosition(float dt)

    {

        if (!useDynamicScreenPosition)

        {

            dynamicScreenY = screenCenterY;

            dynamicScreenYVelocity = 0f;

            return;

        }

        float accelerationFactor = Mathf.Clamp(

            currentAcceleration /

            Mathf.Max(0.1f, accelerationForMaxOffset),

            -1f,

            1f);

        float screenOffset = accelerationFactor >= 0f

            ? -accelerationFactor * accelerationScreenOffset

            : -accelerationFactor * brakingScreenOffset;

        float minY = Mathf.Clamp01(

            screenCenterY - maxVerticalScreenOffset);

        float maxY = Mathf.Clamp01(

            screenCenterY + maxVerticalScreenOffset);

        float desiredY = Mathf.Clamp(

            screenCenterY + screenOffset,

            minY,

            maxY);

        if (!smoothScreenPosition ||

            verticalRepositionSmoothTime <= 0f)

        {

            dynamicScreenY = desiredY;

            dynamicScreenYVelocity = 0f;

            return;

        }

        dynamicScreenY = Mathf.SmoothDamp(

            dynamicScreenY,

            desiredY,

            ref dynamicScreenYVelocity,

            verticalRepositionSmoothTime,

            Mathf.Infinity,

            dt);

    }

    private void UpdateZoom(float dt)

    {

        float desiredDistance = distance;

        if (useSpeedBasedZoom)

        {

            float speedFactor = Mathf.Clamp01(

                currentSpeed /

                Mathf.Max(0.1f, speedForMaxDistance));

            desiredDistance +=

                extraDistanceAtMaxSpeed * speedFactor;

        }

        if (!smoothZoom)

        {

            currentDistance = desiredDistance;

            distanceVelocity = 0f;

            return;

        }

        float smoothTime = desiredDistance > currentDistance

            ? accelerationZoomTime

            : decelerationZoomTime;

        if (smoothTime <= 0f)

        {

            currentDistance = desiredDistance;

            distanceVelocity = 0f;

            return;

        }

        currentDistance = Mathf.SmoothDamp(

            currentDistance,

            desiredDistance,

            ref distanceVelocity,

            smoothTime,

            Mathf.Infinity,

            dt);

    }

    private void UpdateSway(float dt)

    {

        float desiredSway = 0f;

        if (useSteeringSway)

        {

            float speedFactor = Mathf.Clamp01(

                currentSpeed /

                Mathf.Max(0.1f, speedForMaxDistance));

            float turnFactor = GetSteeringFactor();

            desiredSway =

                -turnFactor * lateralSway * speedFactor;

        }

        currentSway = SmoothEffect(

            currentSway,

            desiredSway,

            ref swayVelocity,

            smoothSway,

            swaySmoothTime,

            dt);

    }

    private void UpdateRoll(float dt)

    {

        float desiredRoll = 0f;

        if (useSteeringRoll)

        {

            float speedFactor = Mathf.Clamp01(

                currentSpeed /

                Mathf.Max(0.1f, speedForMaxDistance));

            float turnFactor = GetSteeringFactor();

            desiredRoll =

                -turnFactor * steeringRoll * speedFactor;

        }

        currentRoll = SmoothEffect(

            currentRoll,

            desiredRoll,

            ref rollVelocity,

            smoothRoll,

            rollSmoothTime,

            dt);

        shakePhase += dt * vibrationFrequency;

    }

    private float GetSteeringFactor()
    {
        if (targetVehicleController != null)
            return targetVehicleController.Steering;

        return Mathf.Clamp(currentTurnRate / 120f, -1f, 1f);
    }

    private static float SmoothEffect(

        float current,

        float targetValue,

        ref float velocity,

        bool useSmoothing,

        float smoothTime,

        float dt)

    {

        if (!useSmoothing || smoothTime <= 0f)

        {

            velocity = 0f;

            return targetValue;

        }

        return Mathf.SmoothDamp(

            current,

            targetValue,

            ref velocity,

            smoothTime,

            Mathf.Infinity,

            dt);

    }

    private void UpdateImpact(float dt)

    {

        if (impactTimer > 0f)

        {

            impactTimer -= dt;

            currentImpact = Mathf.Lerp(

                currentImpact,

                0f,

                1f - Mathf.Exp(-12f * dt));

        }

        else

        {

            currentImpact = 0f;

        }

    }

    private void UpdateFOV(float dt)

    {

        float desiredFOV = baseFOV;

        if (useDynamicFOV)

        {

            float speedFactor = Mathf.Clamp01(

                currentSpeed /

                Mathf.Max(0.1f, speedForMaxDistance));

            desiredFOV = Mathf.Lerp(

                baseFOV,

                maxSpeedFOV,

                speedFactor);

        }

        if (!smoothFOV || fovSmoothTime <= 0f)

        {

            currentFOV = desiredFOV;

            fovVelocity = 0f;

        }

        else

        {

            currentFOV = Mathf.SmoothDamp(

                currentFOV,

                desiredFOV,

                ref fovVelocity,

                fovSmoothTime,

                Mathf.Infinity,

                dt);

        }

        if (!transitionActive && !cachedCamera.orthographic)

            cachedCamera.fieldOfView = currentFOV;

    }

    private void ApplyCameraPose(float dt, bool forceSnap = false)

    {

        Quaternion heading = Quaternion.Euler(

            0f,

            currentYaw,

            0f);

        Vector3 targetPosition = target.position;

        Vector3 centerPoint =

            targetPosition + Vector3.up * targetHeight +
            heading * Vector3.forward * lookAheadDistance;

        Vector3 desiredPosition =

            targetPosition

            + Vector3.up * height

            - heading * Vector3.forward * currentDistance

            + heading * Vector3.right * currentSway;

        // For perspective, frame from the position the camera will actually reach.
        // Orthographic keeps its existing order because composition shifts its position.
        bool smoothPosition = !transitionActive && !forceSnap && usePositionSmoothing;
        Vector3 followPosition = smoothPosition && !cachedCamera.orthographic
            ? SmoothFollowPosition(
                desiredPosition + planarTargetVelocity * (positionSmoothTime * 0.85f), dt)
            : desiredPosition;
        Vector3 direction = centerPoint - followPosition;

        if (direction.sqrMagnitude < 0.0001f)

            return;

        Quaternion desiredRotation = Quaternion.LookRotation(

            direction,

            Vector3.up);

        if (cachedCamera.orthographic)

        {

            float worldOffset =

                (dynamicScreenY - 0.5f)

                * 2f

                * cachedCamera.orthographicSize;

            desiredPosition -=

                desiredRotation * Vector3.up * worldOffset;

        }

        else

        {

            float halfFovRadians =

                currentFOV * 0.5f * Mathf.Deg2Rad;

            float pitchOffset = Mathf.Atan(

                2f

                * (dynamicScreenY - 0.5f)

                * Mathf.Tan(halfFovRadians)) * Mathf.Rad2Deg;

            desiredRotation *= Quaternion.Euler(

                pitchOffset,

                0f,

                0f);

        }

        desiredRotation *= Quaternion.AngleAxis(

            currentRoll,

            Vector3.forward);

        if (transitionActive)
        {
            ApplyTransitionPose(
                desiredPosition,
                desiredRotation,
                currentFOV,
                dt);
        }
        else
        {
            if (smoothPosition && cachedCamera.orthographic)
            {
                cachedTransform.position = SmoothFollowPosition(desiredPosition, dt);
            }
            else if (smoothPosition)
            {
                cachedTransform.position = followPosition;
            }
            else
            {
                cachedTransform.position = desiredPosition;
                positionVelocity = Vector3.zero;
            }

            if (!forceSnap && useRotationSmoothing)
            {
                smoothedRotation = Quaternion.RotateTowards(
                    cachedTransform.rotation,
                    desiredRotation,
                    cameraRotationSpeed * dt);

                cachedTransform.rotation = smoothedRotation;
            }
            else
            {
                cachedTransform.rotation = desiredRotation;
                smoothedRotation = desiredRotation;
            }
        }

        Vector3 vibration = Vector3.zero;

        if (useEngineVibration)

        {

            float speedFactor = Mathf.Clamp01(

                currentSpeed /

                Mathf.Max(0.1f, speedForMaxDistance));

            float amplitude = engineVibration * speedFactor;

            vibration = heading * new Vector3(

                Mathf.Sin(shakePhase) * amplitude,

                Mathf.Sin(shakePhase * 1.37f) * amplitude * 0.5f,

                0f);

        }

        Vector3 impactOffset = Vector3.zero;

        if (useImpactPositionShake && currentImpact > 0.001f)

        {

            float noiseA = Mathf.Sin(shakePhase * 31.7f);

            float noiseB = Mathf.Sin(shakePhase * 43.1f + 1.3f);

            impactOffset = new Vector3(

                noiseA,

                noiseB,

                0f) * (impactPositionShake * currentImpact);

        }

        float impactRoll = 0f;

        if (useImpactRotationShake && currentImpact > 0.001f)

        {

            impactRoll = Mathf.Sin(shakePhase * 37f)

                       * impactRotationShake

                       * currentImpact;

        }

        cachedTransform.position += vibration + impactOffset;

        if (Mathf.Abs(impactRoll) > 0.001f)

        {

            cachedTransform.rotation *= Quaternion.AngleAxis(

                impactRoll,

                Vector3.forward);

        }

    }

    private Vector3 SmoothFollowPosition(Vector3 desiredPosition, float dt)
    {
        return Vector3.SmoothDamp(
            cachedTransform.position, desiredPosition, ref positionVelocity,
            positionSmoothTime, Mathf.Infinity, dt);
    }

    private Vector3 GetTargetForward()

    {

        Vector3 forward = target.forward;

        if (ignoreTargetPitchAndRoll)

        {

            forward = Vector3.ProjectOnPlane(

                forward,

                Vector3.up);

            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.Cross(target.right, Vector3.up);
        }

        return forward.sqrMagnitude < 0.0001f

            ? Vector3.forward

            : forward.normalized;

    }

    private void InitializeCamera()

    {

        if (target == null)

            return;

        if (targetRigidbody == null)

            targetRigidbody = target.GetComponent<Rigidbody>();

        targetVehicleController = target.GetComponent<VehicleController>();

        currentYaw = Quaternion.LookRotation(

            GetTargetForward(),

            Vector3.up).eulerAngles.y;

        currentDistance = distance;

        currentFOV = baseFOV;

        dynamicScreenY = screenCenterY;

        currentSway = 0f;

        swayVelocity = 0f;

        currentRoll = 0f;

        rollVelocity = 0f;

        distanceVelocity = 0f;

        positionVelocity = Vector3.zero;

        dynamicScreenYVelocity = 0f;

        fovVelocity = 0f;

        previousVelocity = targetRigidbody != null

            ? targetRigidbody.linearVelocity

            : Vector3.zero;

        previousForward = GetTargetForward();

        planarTargetVelocity = Vector3.ProjectOnPlane(previousVelocity, Vector3.up);
        currentSpeed = planarTargetVelocity.magnitude;

        currentAcceleration = 0f;

        currentTurnRate = 0f;

        currentImpact = 0f;

        impactTimer = 0f;

        shakePhase = 0f;

        if (!transitionActive && !cachedCamera.orthographic)

            cachedCamera.fieldOfView = currentFOV;

        initialized = true;

    }

    public void SetTarget(Transform newTarget)
    {
        if (newTarget == null)
        {
            target = null;
            targetRigidbody = null;
            targetVehicleController = null;
            initialized = false;
            transitionActive = false;
            cameraMode = CameraMode.Disabled;
            return;
        }

        if (cameraMode == CameraMode.Garage || cameraMode == CameraMode.MainMenu)
            BeginTransition(newTarget);

        target = newTarget;
        targetRigidbody = target.GetComponent<Rigidbody>();
        cameraMode = CameraMode.Gameplay;
        initialized = false;

        if (transitionActive)
            InitializeCamera();
        else
            SnapBehindTarget();
    }

    public void EnterMainMenu(Transform parkedVehicle)
    {
        if (parkedVehicle == null)
            return;

        target = parkedVehicle;
        cameraMode = CameraMode.MainMenu;
        transitionActive = false;
        garageFocus = null;
        isDragging = false;
        if (!cachedCamera.orthographic)
            cachedCamera.fieldOfView = baseFOV;

        if (menuCameraHolder != null)
        {
            cachedTransform.SetPositionAndRotation(menuCameraHolder.position, menuCameraHolder.rotation);
            return;
        }

        Vector3 forward = Vector3.ProjectOnPlane(parkedVehicle.forward, Vector3.up);
        forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        Vector3 vehicleRight = Vector3.Cross(Vector3.up, forward);
        Vector3 position = parkedVehicle.position +
            forward * menuDistance + Vector3.up * menuHeight;
        Vector3 aim = parkedVehicle.position + Vector3.up * targetHeight +
            vehicleRight * menuFramingOffset;

        cachedTransform.SetPositionAndRotation(
            position, Quaternion.LookRotation(aim - position, Vector3.up));
    }

    public void EnterGameplay(Transform gameplayTarget)
    {
        if (gameplayTarget == null)
        {
            Debug.LogWarning("[CameraController] Cannot enter gameplay without a target.", this);
            return;
        }

        SetTarget(gameplayTarget);
    }

    public void EnterGarage(Transform focusTarget)
    {
        if (focusTarget == null)
        {
            Debug.LogWarning("[CameraController] Cannot enter garage without a focus target.", this);
            return;
        }

        if (cameraMode != CameraMode.Garage || garageFocus != focusTarget)
        {
            Vector3 towardPivot = focusTarget.position - cachedTransform.position;
            towardPivot.y = 0f;
            if (towardPivot.sqrMagnitude < 0.0001f)
            {
                towardPivot = cachedTransform.forward;
                towardPivot.y = 0f;
            }
            if (towardPivot.sqrMagnitude >= 0.0001f)
                garageYaw = Mathf.Atan2(towardPivot.x, towardPivot.z) * Mathf.Rad2Deg;
        }

        garageFocusTarget = focusTarget;
        garageFocus = focusTarget;
        cameraMode = CameraMode.Garage;
        initialized = false;
        transitionActive = false;
        isDragging = false;
        ApplyGarageCameraPose();
    }

    public void SetGarageFocusTarget(Transform focusTarget)
    {
        if (focusTarget == null)
        {
            Debug.LogWarning("[CameraController] Garage focus target cannot be null.", this);
            return;
        }

        garageFocusTarget = focusTarget;

        if (cameraMode == CameraMode.Garage)
            EnterGarage(focusTarget);
    }

    public void ExitGarage()
    {
        if (cameraMode != CameraMode.Garage)
            return;

        garageFocus = null;
        isDragging = false;
        if (target != null)
        {
            // SetTarget preserves the existing transition path when leaving Garage mode.
            SetTarget(target);
            return;
        }

        transitionActive = false;
        cameraMode = CameraMode.Disabled;
    }

    public void AddImpact(float strength)

    {

        if (!useImpactPositionShake && !useImpactRotationShake)

            return;

        currentImpact = Mathf.Max(

            currentImpact,

            Mathf.Clamp01(strength));

        impactTimer = impactDuration;

    }

    public void SnapBehindTarget()

    {

        if (target == null)
            return;

        transitionActive = false;
        InitializeCamera();

        currentDistance = distance;

        Vector3 forward = GetTargetForward();

        currentYaw = Quaternion.LookRotation(

            forward,

            Vector3.up).eulerAngles.y;

        ApplyCameraPose(0f, true);

    }

    private void OnDisable()
    {
        isDragging = false;
        GarageUIPointerIds.Clear();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
            return;

        isDragging = false;
        GarageUIPointerIds.Clear();
    }

#if UNITY_EDITOR

    private void OnValidate()

    {

        distance = Mathf.Max(0.1f, distance);

        height = Mathf.Max(0f, height);
        lookAheadDistance = Mathf.Max(0f, lookAheadDistance);

        positionSmoothTime = Mathf.Max(0.001f, positionSmoothTime);

        cameraRotationSpeed = Mathf.Max(0f, cameraRotationSpeed);

        headingDamping = Mathf.Max(0f, headingDamping);

        verticalRepositionSmoothTime =

            Mathf.Max(0f, verticalRepositionSmoothTime);

        accelerationZoomTime = Mathf.Max(0f, accelerationZoomTime);

        decelerationZoomTime = Mathf.Max(0f, decelerationZoomTime);

        swaySmoothTime = Mathf.Max(0f, swaySmoothTime);

        rollSmoothTime = Mathf.Max(0f, rollSmoothTime);

        fovSmoothTime = Mathf.Max(0f, fovSmoothTime);

        impactDuration = Mathf.Max(0.01f, impactDuration);

        garageDistance = Mathf.Max(0.1f, garageDistance);
        garageDragSensitivity = Mathf.Max(0f, garageDragSensitivity);
        menuDistance = Mathf.Max(0.1f, menuDistance);
        menuHeight = Mathf.Max(0f, menuHeight);
        transitionDuration = Mathf.Max(0f, transitionDuration);
    }

#endif

}
