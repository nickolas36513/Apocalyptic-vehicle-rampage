
using System;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class LoadingScreenController : UIScreenController
{
    // ---------------------------------------------------------
    // UI REFERENCES
    // ---------------------------------------------------------

    private VisualElement progressFill;
    private VisualElement errorPanel;
    private VisualElement loadingRoot;
    private Label enterPrompt;

    private Label loadingDescription;
    private Label progressValue;
    private Label tipDescription;
    private Label footerStatus;
    private Label errorMessage;

    private Button retryButton;
    private Button returnButton;

    private bool uiReady;
    private bool waitingForEnter;

    // ---------------------------------------------------------
    // LOADING STATE
    // ---------------------------------------------------------

    [SerializeField]
    private float progressAnimationDuration = 0.25f;

    private float progress;
    private float displayedProgress;
    private float animationStartProgress;
    private float animationTargetProgress;
    private float animationStartTime;

    private IVisualElementScheduledItem progressAnimation;

    private string status = "Preparing gameplay...";

    private string tip =
        "Keep moving. Standing still is an invitation to become someone's lunch.";

    private string error;

    private bool hasError;
    private bool retryAvailable = true;

    public float Progress => progress;
    public bool HasError => hasError;
    public bool IsProgressDisplayComplete =>
        Mathf.Approximately(displayedProgress, progress);

    // ---------------------------------------------------------
    // EVENTS
    // ---------------------------------------------------------

    public event Action RetryRequested;
    public event Action ReturnRequested;
    public event Action EnterRequested;

    // ---------------------------------------------------------
    // UI BINDING
    // ---------------------------------------------------------

    protected override void OnBindUI()
    {
        uiReady = false;

        progressFill = GetRequired<VisualElement>("progress-fill");
        errorPanel = GetRequired<VisualElement>("error-panel");
        loadingRoot = GetRequired<VisualElement>("loading-screen");
        enterPrompt = GetRequired<Label>("enter-prompt");

        loadingDescription = GetRequired<Label>("loading-description");
        progressValue = GetRequired<Label>("progress-value");
        tipDescription = GetRequired<Label>("tip-description");
        footerStatus = GetRequired<Label>("footer-status");
        errorMessage = GetRequired<Label>("error-message");

        retryButton = GetRequired<Button>("retry-button");
        returnButton = GetRequired<Button>("return-button");

        if (!HasValidReferences())
        {
            Debug.LogError(
                "[LoadingScreenController] UI binding failed.",
                this);

            return;
        }

        retryButton.clicked += HandleRetry;
        returnButton.clicked += HandleReturn;
        loadingRoot.RegisterCallback<ClickEvent>(HandleScreenClick);

        uiReady = true;

        displayedProgress = progress;

        RefreshUI();
    }

    private T GetRequired<T>(string elementName)
        where T : VisualElement
    {
        if (TryGetElement(elementName, out T element))
            return element;

        Debug.LogError(
            $"[LoadingScreenController] Missing UI element: {elementName}",
            this);

        return null;
    }

    private bool HasValidReferences()
    {
        return
            progressFill != null &&
            errorPanel != null &&
            loadingRoot != null &&
            enterPrompt != null &&
            loadingDescription != null &&
            progressValue != null &&
            tipDescription != null &&
            footerStatus != null &&
            errorMessage != null &&
            retryButton != null &&
            returnButton != null;
    }

    protected override void OnUnbindUI()
    {
        StopProgressAnimation();

        if (retryButton != null)
            retryButton.clicked -= HandleRetry;

        if (returnButton != null)
            returnButton.clicked -= HandleReturn;

        if (loadingRoot != null)
            loadingRoot.UnregisterCallback<ClickEvent>(HandleScreenClick);

        uiReady = false;

        progressFill = null;
        errorPanel = null;
        loadingRoot = null;
        enterPrompt = null;

        loadingDescription = null;
        progressValue = null;
        tipDescription = null;
        footerStatus = null;
        errorMessage = null;

        retryButton = null;
        returnButton = null;
    }

    // ---------------------------------------------------------
    // PUBLIC API
    // ---------------------------------------------------------

    public void SetProgress(float value)
    {
        progress = Mathf.Clamp01(value);

        if (!uiReady)
            return;

        StartProgressAnimation();
    }

    public void SetStatus(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        status = message;

        RefreshStatus();
    }

    public void SetTip(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        tip = message;

        if (uiReady)
            tipDescription.text = tip;
    }

    public void SetRetryAvailable(bool available)
    {
        retryAvailable = available;

        if (uiReady)
            retryButton.SetEnabled(available);
    }

    public void SetReadyToEnter(bool ready)
    {
        waitingForEnter = ready && !hasError;

        if (uiReady && enterPrompt != null)
            enterPrompt.style.display = waitingForEnter
                ? DisplayStyle.Flex
                : DisplayStyle.None;

        RefreshStatus();
    }

    public void ShowError(string message, bool canRetry = true)
    {
        ShowError(message, canRetry, "RETURN");
    }

    public void ShowError(string message, bool canRetry, string returnLabel)
    {
        hasError = true;
        SetReadyToEnter(false);

        error = string.IsNullOrWhiteSpace(message)
            ? "An unexpected error occurred."
            : message;

        retryAvailable = canRetry;

        status = "Initialization failed.";

        RefreshUI();
        if (returnButton != null)
            returnButton.text = returnLabel;
    }

    public void ClearError()
    {
        hasError = false;
        error = null;

        RefreshError();
        RefreshStatus();
    }

    public void ResetScreen()
    {
        StopProgressAnimation();

        progress = 0f;
        displayedProgress = 0f;

        status = "Preparing gameplay...";

        error = null;
        hasError = false;

        retryAvailable = true;
        waitingForEnter = false;

        RefreshUI();
    }

    // ---------------------------------------------------------
    // PROGRESS ANIMATION
    // ---------------------------------------------------------

    private void StartProgressAnimation()
    {
        StopProgressAnimation();

        animationStartProgress = displayedProgress;
        animationTargetProgress = progress;
        animationStartTime = Time.unscaledTime;

        if (Mathf.Approximately(
            animationStartProgress,
            animationTargetProgress))
        {
            displayedProgress = animationTargetProgress;
            RefreshProgressVisuals();
            return;
        }

        if (progressAnimationDuration <= 0f)
        {
            displayedProgress = animationTargetProgress;
            RefreshProgressVisuals();
            return;
        }

        progressAnimation = progressFill.schedule
            .Execute(AnimateProgress)
            .Every(16);
    }

    private void AnimateProgress()
    {
        if (!uiReady || progressFill == null)
        {
            StopProgressAnimation();
            return;
        }

        float elapsed =
            Time.unscaledTime - animationStartTime;

        float t = Mathf.Clamp01(
            elapsed / progressAnimationDuration);

        // Smoothstep interpolation.
        t = t * t * (3f - 2f * t);

        displayedProgress = Mathf.Lerp(
            animationStartProgress,
            animationTargetProgress,
            t);

        RefreshProgressVisuals();

        if (elapsed >= progressAnimationDuration)
        {
            displayedProgress = animationTargetProgress;

            RefreshProgressVisuals();

            StopProgressAnimation();
        }
    }

    private void StopProgressAnimation()
    {
        if (progressAnimation == null)
            return;

        progressAnimation.Pause();
        progressAnimation = null;
    }

    // ---------------------------------------------------------
    // UI REFRESH
    // ---------------------------------------------------------

    private void RefreshUI()
    {
        if (!uiReady)
            return;

        RefreshProgressVisuals();
        RefreshStatus();

        tipDescription.text = tip;

        RefreshError();
    }

    private void RefreshProgressVisuals()
    {
        if (!uiReady || progressFill == null)
            return;

        progressFill.style.width =
            Length.Percent(displayedProgress * 100f);

        progressValue.text =
            $"{Mathf.RoundToInt(displayedProgress * 100f)}%";
    }

    private void RefreshStatus()
    {
        if (!uiReady)
            return;

        loadingDescription.text = status;

        footerStatus.text = hasError
            ? "INITIALIZATION FAILED"
            : waitingForEnter ? "TOUCH TO ENTER" : "INITIALIZING...";
    }

    private void RefreshError()
    {
        if (!uiReady)
            return;

        if (hasError)
        {
            errorPanel.RemoveFromClassList("is-hidden");

            errorMessage.text = error;

            retryButton.SetEnabled(retryAvailable);
        }
        else
        {
            errorPanel.AddToClassList("is-hidden");
        }
    }

    // ---------------------------------------------------------
    // BUTTON CALLBACKS
    // ---------------------------------------------------------

    private void HandleRetry()
    {
        if (!hasError || !retryAvailable)
            return;

        RetryRequested?.Invoke();
    }

    private void HandleReturn()
    {
        ReturnRequested?.Invoke();
    }

    private void HandleScreenClick(ClickEvent evt)
    {
        if (!waitingForEnter || hasError)
            return;

        waitingForEnter = false;
        if (enterPrompt != null)
            enterPrompt.style.display = DisplayStyle.None;

        RefreshStatus();
        EnterRequested?.Invoke();
    }

    // ---------------------------------------------------------
    // SCREEN CALLBACKS
    // ---------------------------------------------------------

    protected override void OnShown()
    {
        RefreshUI();
    }

    protected override void OnDestroy()
    {
        StopProgressAnimation();

        RetryRequested = null;
        ReturnRequested = null;
        EnterRequested = null;

        base.OnDestroy();
    }
}
