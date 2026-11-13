
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-10000)]
public sealed class Bootloader : MonoBehaviour
{
    #region Configuration

    private const string UICatalogResourcePath = "UIScreenCatalog";
    private const string GameBootstrapConfigurationResourcePath =
        "GameBootstrapConfiguration";

    private const string LoadingScreenID = "Loading";
    private const string MainMenuScreenID = "MainMenu";

    private const string WorldSceneName = "WorldScene";

    #endregion

    #region Static Instance

    private static Bootloader instance;

    public static Bootloader Instance => instance;

    #endregion

    #region Private Fields

    private GameStartupPipeline startupPipeline;
    private GameStartupTaskRegistry startupTaskRegistry;
    private GameInstallerRegistry installers;
    private UIScreenCatalog uiCatalog;
    private UIScreenRegistry startupFailureUI;
    private LoadingScreenController loadingScreen;
    private CancellationTokenSource startupCancellation;
    private CancellationTokenSource shutdownCancellation;

    private Task startupTask;
    private Task shutdownTask;

    private bool shutdownStarted;
    private bool isDestroying;

    #endregion

    #region Public Properties

    public GameStartupPipeline StartupPipeline => startupPipeline;
    public GameStartupTaskRegistry StartupTasks => startupTaskRegistry;
    public GameInstallerRegistry Installers => installers;

    public GameEventBus Events { get; private set; }
    public GameServiceRegistry Services { get; private set; }
    public GameStateMachine StateMachine { get; private set; }
    public GameDataStore Data { get; private set; }

    public SaveSystem Save { get; private set; }
    public SceneTransitionManager SceneTransitions { get; private set; }
    public ApplicationLifecycleManager Lifecycle { get; private set; }
    public GameShutdownCoordinator ShutdownCoordinator { get; private set; }

    public UIScreenRegistry UI { get; private set; }
    public PlayerProfileRuntimeService PlayerProfiles { get; private set; }

    public bool IsInitialized { get; private set; }

    #endregion

    #region Bootstrap

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
#if UNITY_EDITOR
        // The standalone test range must start before any profile or menu services.
        if (FindAnyObjectByType<GameplaySandbox>(FindObjectsInactive.Include) != null)
            return;
#endif

        if (instance != null)
            return;

        GameObject root = new GameObject(nameof(Bootloader));
        root.AddComponent<Bootloader>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        DontDestroyOnLoad(gameObject);

        Initialize();
    }

    private void Start()
    {
        // Let Unity activate the entry scene before checking whether
        // WorldScene needs to be loaded additively.
        if (instance == this && StateMachine?.CurrentState == GameState.Initializing)
            StartStartup();
    }

    #endregion

    #region Initialization

    private void Initialize()
    {
        if (IsInitialized)
            return;

        try
        {
            InitializeCoreSystems();

            RegisterInstallers();

            ValidateCoreConfiguration();

            EnterInitializingState();

            InstallServices();

            ValidateServiceRegistrations();

            InitializeServices();

            ResolveServices();

            // Loading is the first screen displayed.
            InitializeUI();

            InitializeStartupPipeline();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            HandleInitializationFailure(exception);
        }
    }

    private void ValidateServiceRegistrations()
    {
        if (!Services.Validate(out string error))
        {
            throw new InvalidOperationException(
                $"Service validation failed: {error}");
        }

    }

    private void ValidateCoreConfiguration()
    {
        if (Events == null)
        {
            throw new InvalidOperationException(
                "GameEventBus is missing.");
        }

        if (Services == null)
        {
            throw new InvalidOperationException(
                "GameServiceRegistry is missing.");
        }

        if (StateMachine == null)
        {
            throw new InvalidOperationException(
                "GameStateMachine is missing.");
        }

        if (uiCatalog == null)
        {
            throw new InvalidOperationException(
                "UIScreenCatalog is missing.");
        }

        if (!Application.CanStreamedLevelBeLoaded(WorldSceneName))
        {
            throw new InvalidOperationException(
                $"Scene '{WorldSceneName}' is not available. " +
                "Check Build Profiles.");
        }
    }

    private void InitializeCoreSystems()
    {
        Events = new GameEventBus();

        Services = new GameServiceRegistry();

        StateMachine = new GameStateMachine(Events);

        Data = new GameDataStore(Events);

        SceneTransitions = new SceneTransitionManager(
            Events,
            StateMachine);

        installers = new GameInstallerRegistry();

        startupTaskRegistry = new GameStartupTaskRegistry();

        ShutdownCoordinator = new GameShutdownCoordinator();

        shutdownCancellation = new CancellationTokenSource();
    }

    private void EnterInitializingState()
    {
        if (!StateMachine.TryChangeState(GameState.Initializing))
        {
            throw new InvalidOperationException(
                "Could not enter Initializing state.");
        }
    }

    private void InstallServices()
    {
        installers.InstallAll(
            Services,
            startupTaskRegistry);
    }

    private void InitializeServices()
    {
        if (!Services.InitializeAll())
        {
            throw new InvalidOperationException(
                "Service initialization failed.", Services.InitializationError);
        }
    }

    private void ResolveServices()
    {
        Save = Services.Get<SaveSystem>();

        Lifecycle = Services.Get<ApplicationLifecycleManager>();

        UI = Services.Get<UIScreenRegistry>();
        PlayerProfiles = Services.Get<PlayerProfileRuntimeService>();

        if (UI == null || !UI.IsInitialized)
        {
            throw new InvalidOperationException(
                "UIScreenRegistry failed to initialize.");
        }

        Events.Subscribe<GameStateChangedEvent>(UpdateGameplayHud);
    }

    private void UpdateGameplayHud(GameStateChangedEvent change)
    {
        const string hudId = "GameplayHUD";
        if (UI == null || !UI.IsInitialized)
            return;

        if (change.CurrentState == GameState.Running)
        {
            if (!UI.Show(hudId, out string error))
                Debug.LogError($"[Gameplay HUD] {error}");
        }
        else if (change.PreviousState == GameState.Running)
        {
            UI.Hide(hudId);
        }
    }

    private void InitializeStartupPipeline()
    {
        startupPipeline = new GameStartupPipeline();

        startupPipeline.Configure(
            startupTaskRegistry.Seal());
    }

    private void StartStartup()
    {
        startupCancellation = new CancellationTokenSource();

        startupTask = RunStartupAsync(
            startupCancellation.Token);
    }

    #endregion

    #region Installer Registration

    private void RegisterInstallers()
    {
        uiCatalog = Resources.Load<UIScreenCatalog>(UICatalogResourcePath);
        if (uiCatalog == null)
        {
            throw new InvalidOperationException(
                $"UIScreenCatalog not found at Resources/{UICatalogResourcePath}.");
        }

        GameBootstrapConfiguration bootstrapConfiguration =
            Resources.Load<GameBootstrapConfiguration>(
                GameBootstrapConfigurationResourcePath);

        if (bootstrapConfiguration == null)
        {
            throw new InvalidOperationException(
                "GameBootstrapConfiguration was not found at " +
                $"Resources/{GameBootstrapConfigurationResourcePath}.");
        }

        installers.Register(
            new CoreGameInstaller(Events, bootstrapConfiguration));

        installers.Register(
            new UIInstaller(uiCatalog));
    }

    #endregion

    #region UI Initialization


    private void InitializeUI()
    {
        if (UI == null)
        {
            throw new InvalidOperationException(
                "UIScreenRegistry is unavailable.");
        }

        if (!UI.Show(LoadingScreenID, out string error))
        {
            throw new InvalidOperationException(
                $"Could not display Loading screen. {error}");
        }

        if (!UI.TryGetController<LoadingScreenController>(
                LoadingScreenID,
                out loadingScreen))
        {
            throw new InvalidOperationException(
                "LoadingScreenController could not be retrieved.");
        }

        loadingScreen.ResetScreen();
        loadingScreen.SetStatus("Initializing application...");
        loadingScreen.SetProgress(0f);

    }


    #endregion

    #region Startup Pipeline

    private async Task RunStartupAsync(CancellationToken token)
    {
        try
        {
            // =============================================
            // 1. Application startup pipeline
            // =============================================


            loadingScreen?.SetStatus("Initializing application...");
            loadingScreen?.SetProgress(0f);

            await startupPipeline.ExecuteAsync(token);

            token.ThrowIfCancellationRequested();

            if (this == null || isDestroying || shutdownStarted)
                return;

            if (StateMachine == null)
            {
                throw new InvalidOperationException(
                    "[Bootloader] GameStateMachine is unavailable.");
            }

            if (StateMachine.CurrentState != GameState.Initializing)
            {
                throw new InvalidOperationException(
                    "[Bootloader] Expected Initializing state after startup " +
                    $"pipeline. Current state: {StateMachine.CurrentState}");
            }

            if (!StateMachine.TryChangeState(GameState.Ready))
            {
                throw new InvalidOperationException(
                    "[Bootloader] Could not enter Ready state.");
            }

            SetLoadingProgress(0.10f);
            SetLoadingStatus("Application initialized.");


            // =============================================
            // 2. Verify Loading screen
            // =============================================

            token.ThrowIfCancellationRequested();

            if (UI == null || loadingScreen == null)
            {
                throw new InvalidOperationException(
                    "[Bootloader] Loading screen is unavailable.");
            }

            if (!UI.Show(LoadingScreenID, out string loadingError))
            {
                throw new InvalidOperationException(
                    $"[Bootloader] Could not display Loading screen. " +
                    loadingError);
            }

            SetLoadingStatus("Preparing world...");

            // =============================================
            // 3. Load WorldScene
            // =============================================

            if (SceneTransitions == null)
            {
                throw new InvalidOperationException(
                    "[Bootloader] SceneTransitionManager is unavailable.");
            }


            SceneTransitionRequest request =
                new SceneTransitionRequest(
                    WorldSceneName,
                    LoadSceneMode.Additive,
                    true);

            bool transitionSucceeded =
                await SceneTransitions.TransitionAsync(
                    request,
                    progress =>
                    {
                        // WorldScene occupies 10% to 70%.
                        float mappedProgress = Mathf.Lerp(
                            0.10f,
                            0.70f,
                            progress);

                        SetLoadingProgress(mappedProgress);
                        SetLoadingStatus("Loading world...");
                    });

            token.ThrowIfCancellationRequested();

            if (this == null || isDestroying || shutdownStarted)
                return;

            if (!transitionSucceeded)
            {
                throw new InvalidOperationException(
                    "[Bootloader] WorldScene transition failed.");
            }

            SetLoadingProgress(0.70f);
            SetLoadingStatus("World loaded. Initializing gameplay...");

            // =============================================
            // 4. Validate WorldScene
            // =============================================

            Scene worldScene =
                SceneManager.GetSceneByName(WorldSceneName);

            if (!worldScene.IsValid() || !worldScene.isLoaded)
            {
                throw new InvalidOperationException(
                    "[Bootloader] WorldScene transition succeeded, " +
                    "but WorldScene is not loaded.");
            }


            // =============================================
            // 5. Find GameplayBootstrap
            // =============================================

            GameplayBootstrap gameplayBootstrap =
                RequireGameplayBootstrap(worldScene);

            await PlayerSetupFlow.PrepareAsync(
                PlayerProfiles.Selection, UI, LoadingScreenID, token);
            token.ThrowIfCancellationRequested();


            // =============================================
            // 6. Wait for gameplay initialization
            // =============================================


            SetLoadingStatus("Initializing gameplay...");

            Task<bool> gameplayTask =
                gameplayBootstrap.InitializeAsync(token);

            // Gameplay occupies 70% to 95%.
            // Read the actual completed initializer progress.
            while (!gameplayTask.IsCompleted)
            {
                token.ThrowIfCancellationRequested();

                if (this == null || isDestroying || shutdownStarted)
                    return;

                float gameplayProgress =
                    gameplayBootstrap.InitializationProgress;

                float mappedProgress = Mathf.Lerp(
                    0.70f,
                    0.95f,
                    gameplayProgress);

                SetLoadingProgress(mappedProgress);

                SetLoadingStatus(
                    $"Initializing gameplay... " +
                    $"{Mathf.RoundToInt(gameplayProgress * 100f)}%");

                await Task.Yield();
            }

            bool gameplayInitialized = await gameplayTask;

            token.ThrowIfCancellationRequested();

            if (this == null || isDestroying || shutdownStarted)
                return;

            if (!gameplayInitialized)
            {
                throw new InvalidOperationException(
                    "[Bootloader] World initialization failed.",
                    gameplayBootstrap.InitializationError);
            }

            if (!gameplayBootstrap.IsInitialized)
            {
                throw new InvalidOperationException(
                    "[Bootloader] GameplayBootstrap reported success, " +
                    "but IsInitialized is false.");
            }

            SetLoadingProgress(0.95f);


            // =============================================
            // 7. Verify final gameplay state
            // =============================================

            if (StateMachine.CurrentState != GameState.Ready)
            {
                throw new InvalidOperationException(
                    "[Bootloader] Expected Ready state after world " +
                    "initialization. Current state: " +
                    StateMachine.CurrentState);
            }

            // =============================================
            // 8. Prepare MainMenu
            // =============================================

            SetLoadingStatus("Preparing main menu...");

            if (UI == null)
            {
                throw new InvalidOperationException(
                    "[Bootloader] UIScreenRegistry became unavailable.");
            }

            // MainMenu is synchronously displayed by UIScreenRegistry.
            // Set the final progress before switching screens.
            SetLoadingProgress(1f);

            await WaitForPlayerEnterAsync(token);
            token.ThrowIfCancellationRequested();

            if (!UI.Show(MainMenuScreenID, out string menuError))
            {
                throw new InvalidOperationException(
                    $"[Bootloader] Could not display MainMenu. " +
                    menuError);
            }


            // =============================================
            // 9. Complete application startup
            // =============================================

            IsInitialized = true;

        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"[Bootloader] Startup failed:\n{exception}");

            if (this != null && !isDestroying && !shutdownStarted)
            {
                await ShutdownAsync();
                if (this != null && !isDestroying)
                    ShowStartupFailure(exception);
            }
        }
        finally
        {
            startupCancellation?.Dispose();
            startupCancellation = null;
        }
    }

    private void SetLoadingProgress(float progress)
    {
        if (loadingScreen == null)
            return;

        loadingScreen.SetProgress(
            Mathf.Clamp01(progress));
    }

    private void SetLoadingStatus(string status)
    {
        if (loadingScreen == null)
            return;

        loadingScreen.SetStatus(status);
    }

    private async Task WaitForPlayerEnterAsync(CancellationToken token)
    {
        while (loadingScreen != null &&
               !loadingScreen.IsProgressDisplayComplete)
        {
            token.ThrowIfCancellationRequested();
            await Task.Yield();
        }

        token.ThrowIfCancellationRequested();

        if (loadingScreen == null)
            throw new InvalidOperationException(
                "[Bootloader] Loading screen is unavailable.");

        var completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        Action onEnter = () => completion.TrySetResult(true);
        loadingScreen.EnterRequested += onEnter;

        try
        {
            loadingScreen.SetStatus(
                "Loading complete. Touch or click anywhere to enter.");
            loadingScreen.SetReadyToEnter(true);

            using (token.Register(() => completion.TrySetCanceled()))
                await completion.Task;
        }
        finally
        {
            loadingScreen.EnterRequested -= onEnter;
            loadingScreen.SetReadyToEnter(false);
        }
    }

    private static GameplayBootstrap RequireGameplayBootstrap(Scene scene)
    {
        GameplayBootstrap result = null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (GameplayBootstrap bootstrap in root.GetComponentsInChildren<GameplayBootstrap>())
            {
                if (!bootstrap.isActiveAndEnabled)
                    continue;
                if (result != null)
                    throw new InvalidOperationException(
                        $"Scene '{scene.name}' has more than one active GameplayBootstrap.");
                result = bootstrap;
            }
        }

        if (result == null)
            throw new InvalidOperationException(
                $"Scene '{scene.name}' requires an active GameplayBootstrap.");
        return result;
    }

    #endregion

    #region Shutdown

    public Task ShutdownAsync()
    {
        if (shutdownTask != null)
            return shutdownTask;

        var completion = new TaskCompletionSource<bool>();

        // Publish the task before executing shutdown logic.
        shutdownTask = completion.Task;

        _ = RunShutdownAsync(completion);
        return shutdownTask;
    }

    private async Task RunShutdownAsync(
        TaskCompletionSource<bool> completion)
    {
        shutdownStarted = true;
        IsInitialized = false;

        try
        {
            EnterShuttingDownState();

            startupCancellation?.Cancel();

            Lifecycle?.HandleShutdown();

            if (ShutdownCoordinator != null &&
                shutdownCancellation != null)
            {
                await ShutdownCoordinator.ShutdownAsync(
                    shutdownCancellation.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected shutdown cancellation.
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            Services?.ShutdownAll();

            EnterShutdownState();

            shutdownCancellation?.Dispose();
            shutdownCancellation = null;

            completion.TrySetResult(true);
        }
    }

    private void EnterShuttingDownState()
    {
        if (StateMachine != null &&
            StateMachine.CurrentState != GameState.Shutdown &&
            StateMachine.CurrentState != GameState.ShuttingDown)
        {
            StateMachine.TryChangeState(GameState.ShuttingDown);
        }
    }

    private void EnterShutdownState()
    {
        if (StateMachine != null &&
            StateMachine.CurrentState == GameState.ShuttingDown)
        {
            StateMachine.TryChangeState(GameState.Shutdown);
        }
    }

    private void HandleInitializationFailure(Exception exception)
    {
        IsInitialized = false;

        startupCancellation?.Cancel();

        EnterShuttingDownState();

        Services?.ShutdownAll();

        EnterShutdownState();

        startupCancellation?.Dispose();
        startupCancellation = null;

        shutdownCancellation?.Dispose();
        shutdownCancellation = null;

        UI = null;
        Save = null;
        Lifecycle = null;
        PlayerProfiles = null;
        Debug.LogError(
            $"[Bootloader] INITIALIZATION FAILURE:\n{exception}");
        ShowStartupFailure(exception);
    }

    private void ShowStartupFailure(Exception exception)
    {
        if (uiCatalog == null || startupFailureUI != null)
            return;

        // The normal services have shut down. This registry owns only the
        // terminal error view and has no profile or world dependencies.
        var failureUI = new UIScreenRegistry(uiCatalog);
        try
        {
            failureUI.Initialize();
            if (!failureUI.Show(LoadingScreenID, out string error))
                throw new InvalidOperationException(error);
            if (!failureUI.TryGetController<LoadingScreenController>(
                    LoadingScreenID, out var failureScreen))
                throw new InvalidOperationException("Startup error screen is unavailable.");

            failureScreen.ShowError(exception.GetBaseException().Message, false, "QUIT");
            failureScreen.ReturnRequested += QuitAfterStartupFailure;
            startupFailureUI = failureUI;
        }
        catch (Exception displayError)
        {
            failureUI.Shutdown();
            Debug.LogException(displayError, this);
        }
    }

    private static void QuitAfterStartupFailure() => Application.Quit();

    #endregion

    #region Game State

    public bool ChangeState(GameState state)
    {
        if (!IsInitialized ||
            shutdownStarted ||
            StateMachine == null)
        {
            return false;
        }

        return StateMachine.TryChangeState(state);
    }

    #endregion

    #region Application Lifecycle

    private void OnApplicationPause(bool paused)
    {
        Lifecycle?.HandlePause(paused);

        if (paused && PlayerProfiles != null &&
            PlayerProfiles.IsInitialized &&
            !PlayerProfiles.Save(out string error))
        {
            Debug.LogError(
                $"[Bootloader] Could not save player profile on pause: {error}");
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        Lifecycle?.HandleFocus(hasFocus);
    }

    private void OnApplicationQuit()
    {
        Lifecycle?.HandleShutdown();
    }

    #endregion

    #region Destruction

    private void OnDestroy()
    {
        if (instance != this)
            return;

        isDestroying = true;
        IsInitialized = false;

        // Unity cannot await asynchronous cleanup from OnDestroy.
        startupCancellation?.Cancel();
        shutdownCancellation?.Cancel();

        Lifecycle?.HandleShutdown();

        Services?.ShutdownAll();
        Services?.Clear();
        startupFailureUI?.Shutdown();
        startupFailureUI = null;

        installers?.Clear();

        // Do not clear a pipeline that may still be executing.
        startupTaskRegistry?.Clear();

        StateMachine?.Clear();
        Events?.Clear();

        ClearReferences();

        instance = null;
    }

    private void ClearReferences()
    {
        startupPipeline = null;
        startupTaskRegistry = null;
        installers = null;
        uiCatalog = null;

        Lifecycle = null;
        Save = null;
        UI = null;
        PlayerProfiles = null;

        SceneTransitions = null;
        ShutdownCoordinator = null;

        Data = null;
        Services = null;
        StateMachine = null;
        Events = null;

        startupCancellation = null;
        shutdownCancellation = null;
    }

    #endregion
}
