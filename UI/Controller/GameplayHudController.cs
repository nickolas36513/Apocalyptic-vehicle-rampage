
using System;
using UnityEngine;
using UnityEngine.UIElements;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class GameplayHudController : UIScreenController
{
    private const float RefreshInterval = 0.1f;

    private VisualElement hud;
    private VisualElement pauseOverlay;

    private VisualElement healthFill;
    private VisualElement fuelFill;

    private Label speedLabel;
    private Label healthLabel;
    private Label fuelLabel;

    private Button pauseButton;
    private Button resumeButton;
    private Button endRunButton;

    private float nextRefresh;

    private int speed = -1;
    private int health = -1;
    private int fuel = -1;

    private bool isPaused;
    private float previousTimeScale = 1f;

    private VehicleController suspendedController;
    private bool previousControllerEnabled;

    public bool IsPaused => isPaused;

    protected override void OnBindUI()
    {
        // Allow world input to pass through unused HUD space.
        Host.Root.pickingMode = PickingMode.Ignore;

        if (!TryGetElement("gameplay-hud", out hud) ||
            !TryGetElement("pause-overlay", out pauseOverlay) ||
            !TryGetElement("speed-value", out speedLabel) ||
            !TryGetElement("health-value", out healthLabel) ||
            !TryGetElement("fuel-value", out fuelLabel) ||
            !TryGetElement("health-fill", out healthFill) ||
            !TryGetElement("fuel-fill", out fuelFill) ||
            !TryGetElement("pause-button", out pauseButton) ||
            !TryGetElement("resume-button", out resumeButton) ||
            !TryGetElement("end-run-button", out endRunButton))
        {
            throw new InvalidOperationException(
                "Gameplay HUD bindings are incomplete.");
        }

        hud.pickingMode = PickingMode.Ignore;

        pauseOverlay.pickingMode = PickingMode.Position;
        pauseButton.pickingMode = PickingMode.Position;
        resumeButton.pickingMode = PickingMode.Position;

        // Prevent duplicate registrations if UI is rebound.
        pauseButton.clicked -= PauseGame;
        pauseButton.clicked += PauseGame;

        resumeButton.clicked -= ResumeGame;
        resumeButton.clicked += ResumeGame;
        endRunButton.clicked += EndRun;

        pauseOverlay.style.display = DisplayStyle.None;

        speed = health = fuel = -1;
    }

    protected override void OnShown()
    {
        Refresh();

        nextRefresh = Time.unscaledTime + RefreshInterval;
    }

    private void Update()
    {
        if (!IsVisible)
            return;

        if (PlayerManager.Instance?.ActiveRuntime?.Vitals.IsDestroyed == true)
        {
            EndRun();
            return;
        }

        if (PauseKeyPressed())
        {
            if (isPaused)
                ResumeGame();
            else
                PauseGame();
        }

        if (hud == null || Time.unscaledTime < nextRefresh)
            return;

        nextRefresh = Time.unscaledTime + RefreshInterval;

        Refresh();
    }

    private static bool PauseKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               Keyboard.current.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Escape);
#else
        return false;
#endif
    }

    // =========================================================
    // PAUSE
    // =========================================================

    public void PauseGame()
    {
        if (!IsVisible || isPaused || pauseOverlay == null)
            return;

        // Do not claim ownership of an existing external pause.
        if (Mathf.Approximately(Time.timeScale, 0f))
            return;

        previousTimeScale = Time.timeScale;

        PlayerManager player = PlayerManager.Instance;

        suspendedController = null;
        previousControllerEnabled = false;

        if (player != null && player.ActiveVehicle != null)
        {
            suspendedController =
                player.ActiveVehicle.GetComponent<VehicleController>();

            if (suspendedController != null)
            {
                previousControllerEnabled =
                    suspendedController.enabled;

                suspendedController.StopInput();
                suspendedController.enabled = false;
            }
        }

        isPaused = true;

        Time.timeScale = 0f;

        pauseOverlay.style.display = DisplayStyle.Flex;
        pauseButton.SetEnabled(false);
    }

    // =========================================================
    // RESUME
    // =========================================================

    public void ResumeGame()
    {
        if (!isPaused)
            return;

        isPaused = false;

        if (pauseOverlay != null)
            pauseOverlay.style.display = DisplayStyle.None;

        if (pauseButton != null)
            pauseButton.SetEnabled(true);

        // Restore controller state without overriding a
        // controller that was already disabled.
        if (suspendedController != null)
        {
            suspendedController.StopInput();
            suspendedController.enabled =
                previousControllerEnabled;
        }

        suspendedController = null;

        Time.timeScale = previousTimeScale;
    }

    private void EndRun()
    {
        Bootloader bootloader = Bootloader.Instance;
        if (bootloader == null || bootloader.UI == null ||
            bootloader.StateMachine?.CurrentState != GameState.Running ||
            bootloader.StateMachine.IsTransitioning)
            return;

        if (!bootloader.UI.Show("GameOver", out string error))
        {
            Debug.LogError($"Could not open Game Over: {error}", this);
            return;
        }

        if (!bootloader.ChangeState(GameState.Transitioning))
        {
            bootloader.UI.Hide("GameOver");
            return;
        }

        PlayerManager.Instance?.ParkForMenu();
    }

    // =========================================================
    // VEHICLE TELEMETRY
    // =========================================================

    private void Refresh()
    {
        if (hud == null)
            return;

        VehicleRuntime vehicle =
            PlayerManager.Instance?.ActiveRuntime;

        bool hasVehicle =
            vehicle != null &&
            vehicle.GameObject != null &&
            vehicle.Rigidbody != null;

        DisplayStyle display =
            hasVehicle
                ? DisplayStyle.Flex
                : DisplayStyle.None;

        if (hud.style.display.value != display)
            hud.style.display = display;

        if (!hasVehicle)
            return;

        Vector3 velocity = vehicle.Rigidbody.linearVelocity;
        velocity.y = 0f;

        int nextSpeed =
            Mathf.RoundToInt(velocity.magnitude * 3.6f);

        if (nextSpeed != speed)
        {
            speed = nextSpeed;
            speedLabel.text = speed.ToString();
        }

        RefreshMeter(
            healthLabel,
            healthFill,
            vehicle.Vitals.Health,
            vehicle.Vitals.MaximumHealth,
            ref health);

        RefreshMeter(
            fuelLabel,
            fuelFill,
            vehicle.Fuel.Fuel,
            vehicle.Fuel.Capacity,
            ref fuel);
    }

    private static void RefreshMeter(
        Label label,
        VisualElement fill,
        float value,
        float capacity,
        ref int previous)
    {
        int percent = capacity > 0f
            ? Mathf.RoundToInt(
                Mathf.Clamp01(value / capacity) * 100f)
            : 0;

        if (percent == previous)
            return;

        previous = percent;

        label.text = $"{percent}%";

        fill.style.width = Length.Percent(percent);

        fill.EnableInClassList(
            "hud-low",
            percent <= 20);
    }

    // =========================================================
    // APPLICATION LIFECYCLE
    // =========================================================

    private void OnApplicationPause(bool paused)
    {
        if (paused && IsVisible)
            PauseGame();
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused && IsVisible)
            PauseGame();
    }

    private void OnDisable()
    {
        ResumeGame();
    }

    protected override void OnHidden() => ResumeGame();

    protected override void OnUnbindUI()
    {
        ResumeGame();

        if (pauseButton != null)
            pauseButton.clicked -= PauseGame;

        if (resumeButton != null)
            resumeButton.clicked -= ResumeGame;

        if (endRunButton != null)
            endRunButton.clicked -= EndRun;
    }
}
