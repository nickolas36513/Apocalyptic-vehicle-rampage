UNITY CORE ARCHITECTURE
Complete Usage Guide
Version: Working architecture assembled in this project

============================================================
1. PURPOSE AND DESIGN
============================================================

This architecture separates application-wide responsibilities into small systems:

- Bootloader: creates and owns the persistent application-level systems.
- GameStateMachine: tracks the application's high-level state.
- GameEventBus: publishes typed events without direct references between publishers and listeners.
- GameServiceRegistry: stores services and initializes/shuts down managed services.
- IGameInstaller / GameInstallerRegistry: compose services and startup tasks.
- GameStartupTaskRegistry / GameStartupPipeline: collect and execute asynchronous startup tasks in order.
- GameDataStore: stores runtime values in memory and publishes change events.
- SaveSystem / ISaveProvider / JsonSaveProvider: serialize supported save values and persist them to disk.
- SceneTransitionManager: loads scenes and optionally unloads other scenes.
- ApplicationLifecycleManager: forwards pause, focus, and shutdown notifications.
- GameShutdownCoordinator / IGameShutdownHandler: run asynchronous shutdown handlers in order.
- ArchitectureValidator: checks whether core systems exist and are in expected states.

The goal is to keep gameplay-specific code out of Bootloader and to avoid systems depending directly on unrelated systems.

Current integration and reuse boundaries

- Foundation: the event bus, state machine, service registry, startup/shutdown
  contracts, scene transition manager and generic save provider can be reused.
  Bootloader and CoreGameInstaller are The Last Ride's composition code. Replace
  their game-specific resource and service wiring when taking the foundation
  into another project.
- UI Toolkit: reuse UIScreenCatalog, UIScreenRegistry, UIScreenHost,
  UIScreenController and the validator. A registry creates a screen when it
  is first requested. Project controllers and catalog assets supply the screens.
- World: Bootloader explicitly initializes the one active GameplayBootstrap
  after scene activation. Add scene modules through IGameplayInitializable and
  InitializationOrder. Profile setup is coordinated by PlayerSetupFlow before
  world initialization, so the vehicle initializer only prepares the vehicle.
- Camera: WorldScene owns the existing camera and listener. PlayerManager
  binds its target and Garage pivot. Its Inspector camera actions validate the
  scene and repair a missing reference when there is one eligible rig.
- Session: a prepared world remains Ready through the entry prompt and menus.
  Play unlocks the existing vehicle and enters Running. Garage reuses that same
  vehicle. When parking, PlayerManager remembers the original driving state.
- Persistence: PlayerProfileStore owns the profile file. The generic SaveSystem
  is a separate service and persists assembly-qualified type names. Check save
  compatibility before moving a saved type into another assembly or package.

IMPORTANT:
This project architecture has been assembled incrementally. Before treating it as production-ready, compile every script together and resolve any signature differences from earlier iterations. In particular, verify the current signatures of IGameInstaller.Install, SceneTransitionManager's constructor, GameServiceRegistry.Register, and Bootloader shutdown methods. This guide describes the latest intended integration discussed in the project, not a guarantee that every locally edited file currently matches it.

============================================================
2. RECOMMENDED PROJECT FOLDER STRUCTURE
============================================================

Scripts/
  Core/
    Bootloader.cs
    GameState.cs
    GameStateMachine.cs
    GameStateChangedEvent.cs
    IGameStateHandler.cs
    IGameService.cs
    GameEventBus.cs
    GameServiceRegistry.cs
    GameDataStore.cs
    GameDataChangedEvent.cs
    IGameInstaller.cs
    GameInstallerRegistry.cs
    CoreGameInstaller.cs
    GameStartupTaskRegistry.cs
    GameStartupPipeline.cs
    IGameStartupTask.cs
    GameShutdownCoordinator.cs
    IGameShutdownHandler.cs
    ApplicationLifecycleManager.cs
    ApplicationLifecycleEvents.cs
    ArchitectureValidator.cs

  Saving/
    SaveData.cs
    ISaveProvider.cs
    JsonSaveProvider.cs
    SaveSystem.cs
    SaveEvents.cs

  Scenes/
    SceneTransitionManager.cs
    SceneTransitionRequest.cs
    SceneTransitionEvents.cs

These are organizational suggestions. Unity does not require this exact folder structure.

============================================================
3. BOOTLOADER
============================================================

Bootloader is the persistent entry point. It is created before the first scene through RuntimeInitializeOnLoadMethod and survives scene changes through DontDestroyOnLoad.

Access shared systems through Bootloader.Instance after initialization:

    Bootloader boot = Bootloader.Instance;

    GameEventBus events = boot.Events;
    GameStateMachine states = boot.StateMachine;
    GameServiceRegistry services = boot.Services;
    GameDataStore data = boot.Data;
    SaveSystem save = boot.Save;
    SceneTransitionManager scenes = boot.SceneTransitions;

Do not create another Bootloader manually. If a Bootloader exists in a scene as well as the automatically created one, its duplicate protection should destroy the duplicate, but avoid relying on duplicate setup.

Bootloader should coordinate construction and lifetime only. Avoid putting gameplay rules, UI behavior, mission logic, or weapon logic into it.

Initialization should follow this broad order:
1. Create the event bus and state machine.
2. Create registries and foundational objects.
3. Register installers.
4. Install services and startup tasks.
5. Initialize managed services.
6. Configure and execute startup tasks.
7. Enter Ready after startup tasks complete.

Do not enter Ready before asynchronous startup has completed.

============================================================
4. APPLICATION STATES
============================================================

GameState currently includes:

    Uninitialized
    Initializing
    Ready
    Running
    Suspended
    Transitioning
    ShuttingDown
    Shutdown

Change state through Bootloader when possible:

    Bootloader.Instance.ChangeState(GameState.Running);
    Bootloader.Instance.ChangeState(GameState.Suspended);

The call returns true if the state machine accepted the transition, false otherwise.

Meaning:
- Uninitialized: core application objects have not been created.
- Initializing: foundational systems and startup tasks are being initialized.
- Ready: startup completed; application can begin its normal flow.
- Running: application work is active.
- Suspended: application-level work is temporarily paused.
- Transitioning: a scene transition is in progress.
- ShuttingDown: shutdown is underway.
- Shutdown: shutdown is complete.

The current state machine's transition rules are intentionally broad beyond its protected lifecycle transitions. If the project requires stricter rules, implement an explicit allowed-transition table and test every permitted transition.

IGameStateHandler can be implemented by a system that needs callbacks:

    public sealed class ExampleStateObserver : IGameStateHandler
    {
        public void OnStateExit(GameState previous, GameState next)
        {
            // Stop or release behavior associated with the previous state.
        }

        public void OnStateEnter(GameState previous, GameState next)
        {
            // Start or enable behavior associated with the new state.
        }
    }

Register the handler with:

    Bootloader.Instance.StateMachine.RegisterHandler(observer);

Unregister it when the observer no longer needs notifications. Avoid registering the same handler multiple times.

============================================================
5. TYPED EVENT BUS
============================================================

GameEventBus decouples a publisher from the code responding to the event.

Define a small event type:

    public readonly struct InventoryChangedEvent
    {
        public readonly int ItemId;
        public readonly int Quantity;

        public InventoryChangedEvent(int itemId, int quantity)
        {
            ItemId = itemId;
            Quantity = quantity;
        }
    }

Subscribe:

    private void OnEnable()
    {
        Bootloader.Instance.Events.Subscribe<InventoryChangedEvent>(OnInventoryChanged);
    }

    private void OnDisable()
    {
        if (Bootloader.Instance != null &&
            Bootloader.Instance.Events != null)
        {
            Bootloader.Instance.Events.Unsubscribe<InventoryChangedEvent>(OnInventoryChanged);
        }
    }

    private void OnInventoryChanged(InventoryChangedEvent evt)
    {
        Debug.Log($"Item {evt.ItemId} quantity: {evt.Quantity}");
    }

Publish from the system that owns the change:

    Bootloader.Instance.Events.Publish(
        new InventoryChangedEvent(itemId, newQuantity));

Best practices:
- Events should describe something that happened, such as InventoryChanged or SaveCompleted.
- Use a command or direct method call when you need a return value or an immediate decision.
- Subscribe and unsubscribe symmetrically. Persistent subscriptions can retain objects or invoke destroyed components.
- Keep event payloads small and preferably immutable.
- This event bus is intended for main-thread use; it is not thread-safe.
- Event delivery is synchronous. A slow subscriber can delay the publisher.
- Subscriber exceptions are logged and isolated by the current bus implementation.

The project already has events for state changes, data changes, save operations, lifecycle notifications, and scene transitions. Reuse those rather than creating parallel notification mechanisms.

============================================================
6. SERVICES AND DEPENDENCIES
============================================================

IGameService defines:

    void Initialize();
    void Shutdown();

A managed service is initialized by GameServiceRegistry.InitializeAll() and shut down in reverse initialization order.

Example service:

    using UnityEngine;

    public sealed class AudioService : IGameService
    {
        public bool IsInitialized { get; private set; }

        public void Initialize()
        {
            IsInitialized = true;
        }

        public void Shutdown()
        {
            IsInitialized = false;
        }
    }

Register it during installer setup:

    services.Register<AudioService>(new AudioService());

Resolve it later:

    AudioService audio = Bootloader.Instance.Services.Get<AudioService>();

Or use TryGet if the service may be absent:

    if (Bootloader.Instance.Services.TryGet<AudioService>(out var audio))
    {
        // Use audio.
    }

Dependencies:
The current registry supports dependencies through registered service types. For example, if ServiceB requires ServiceA, register both services and register ServiceB with typeof(ServiceA) as a dependency:

    services.Register<ServiceA>(serviceA);
    services.Register<ServiceB>(serviceB, typeof(ServiceA));

The registry resolves initialization order from the dependency graph, detects missing dependencies and circular dependencies, and shuts down initialized services in reverse order.

Use dependencies only for services that implement IGameService and are registered with the exact service type used by the dependency declaration. Avoid depending on registration order alone when a real dependency exists.

Do not register the same service type twice. Do not initialize a managed service manually as well as through the registry; that can cause duplicate initialization.

============================================================
7. INSTALLERS
============================================================

Installers compose a part of the application. They should construct and register services and contribute startup tasks, not start the entire application themselves.

The latest intended installer interface is:

    public interface IGameInstaller
    {
        void Install(
            GameServiceRegistry services,
            GameStartupTaskRegistry startupTasks);
    }

A core installer can register services:

    public sealed class CoreGameInstaller : IGameInstaller
    {
        private readonly GameEventBus eventBus;

        public CoreGameInstaller(GameEventBus eventBus)
        {
            this.eventBus = eventBus;
        }

        public void Install(
            GameServiceRegistry services,
            GameStartupTaskRegistry startupTasks)
        {
            var save = new SaveSystem(new JsonSaveProvider(), eventBus);
            var lifecycle = new ApplicationLifecycleManager(eventBus);

            services.Register<SaveSystem>(save);
            services.Register<ApplicationLifecycleManager>(lifecycle);
        }
    }

Register installers before InstallAll:

    installers.Register(new CoreGameInstaller(events));
    installers.InstallAll(services, startupTaskRegistry);

Installers are run once by GameInstallerRegistry. Register all installers before installation. If an installer fails, startup should fail and already registered resources should be cleaned up. Keep installer code deterministic and avoid scene-specific assumptions.

============================================================
8. ASYNCHRONOUS STARTUP TASKS
============================================================

Use IGameStartupTask for asynchronous work that must finish before the application enters Ready.

    using System.Threading;
    using System.Threading.Tasks;

    public interface IGameStartupTask
    {
        int Order { get; }
        Task ExecuteAsync(CancellationToken cancellationToken);
    }

Example task:

    using System.Threading;
    using System.Threading.Tasks;
    using UnityEngine;

    public sealed class ConfigurationStartupTask : IGameStartupTask
    {
        public int Order => 100;

        public Task ExecuteAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Validate or prepare configuration here.
            Debug.Log("Configuration prepared.");

            return Task.CompletedTask;
        }
    }

Register during installer installation:

    startupTasks.Register(new ConfigurationStartupTask());

The registry is sealed before execution. Once sealed, tasks cannot be added. The pipeline sorts tasks by Order and awaits them sequentially. Lower Order values run first. Avoid assigning the same order to tasks whose relative ordering matters.

Use CancellationToken-aware operations. A startup task should throw OperationCanceledException when cancellation is requested, and should not swallow cancellation accidentally.

The Bootloader should only transition to Ready after the pipeline succeeds. If a task fails, the application should remain out of Ready and perform controlled cleanup.

Do not use Task.Run for Unity API operations. Unity APIs generally must be called on Unity's main thread. For genuinely CPU-heavy pure C# work, use background processing only when safe and return to the main thread before touching Unity objects.

============================================================
9. RUNTIME DATA STORE
============================================================

GameDataStore stores in-memory runtime values by string key and publishes GameDataChangedEvent when values change.

Set:

    Bootloader.Instance.Data.Set<int>("session.score", 250);

Read safely:

    if (Bootloader.Instance.Data.TryGet<int>("session.score", out int score))
    {
        Debug.Log(score);
    }

Read with required-key behavior:

    int score = Bootloader.Instance.Data.Get<int>("session.score");

Check or remove:

    bool exists = Bootloader.Instance.Data.Contains("session.score");
    Bootloader.Instance.Data.Remove("session.score");

Subscribe to changes:

    Bootloader.Instance.Events.Subscribe<GameDataChangedEvent>(OnDataChanged);

    private void OnDataChanged(GameDataChangedEvent evt)
    {
        Debug.Log($"Data key changed: {evt.Key}");
    }

Important:
- This store is runtime memory, not permanent save storage.
- Keys must not be null, empty, or whitespace.
- Use consistent key naming, such as "profile.level" or "session.score".
- The store uses object values. Keep types consistent when reading a key.
- A stored reference type may be mutable; changing its internals does not automatically publish a change event. Call Set again or use immutable data if observers need notifications.
- Remove publishes a change event with a null current value.
- Clear removes entries and publishes change notifications for them.

============================================================
10. SAVE AND LOAD
============================================================

SaveSystem is separate from GameDataStore. Use it for data that must survive application restarts.

Example:

    SaveSystem save = Bootloader.Instance.Save;

    save.Set<string>("profile.playerName", "Player");
    save.Set<int>("profile.level", 4);
    save.Set<float>("settings.musicVolume", 0.75f);

    bool saved = save.Save("profile");

Load a slot:

    bool loaded = save.Load("profile");

    if (loaded &&
        save.TryGet<int>("profile.level", out int level))
    {
        Debug.Log($"Loaded level: {level}");
    }

Check, remove, or delete:

    bool exists = save.Exists("profile");
    save.Remove("profile.level");
    save.Delete("profile");

Slot names should be stable and should not include path separators. JsonSaveProvider writes save files under Application.persistentDataPath/Saves by default.

Save limitations:
- Unity JsonUtility does not support every C# type. Dictionaries and arbitrary object graphs are not automatically supported.
- Save simple serializable classes, structs, primitives, strings, and lists that Unity's serializer supports.
- Avoid serializing UnityEngine.Object references directly. Save stable IDs and resolve those IDs at runtime.
- Keep save keys and data types stable. Changing a saved type or its assembly-qualified name can make older records incompatible.
- Version migration is not fully implemented merely because SaveData has a Version field. Add explicit migration code before changing the save schema.
- Test save/load on every target platform. File.Replace and filesystem semantics can differ by platform.
- Do not assume Save() succeeded until it returns true; inspect SaveFailedEvent and logs when it fails.
- Consider saving on explicit checkpoints and on relevant pause/shutdown events, but do not rely only on OnApplicationQuit on mobile platforms.

Save/load events can be subscribed to through GameEventBus, for example SaveCompletedEvent, SaveFailedEvent, LoadCompletedEvent, and LoadFailedEvent.

============================================================
11. SCENE TRANSITIONS
============================================================

SceneTransitionManager uses Unity SceneManager APIs and publishes transition events.

Example additive load:

    var request = new SceneTransitionRequest(
        sceneName: "SettingsScene",
        loadMode: UnityEngine.SceneManagement.LoadSceneMode.Additive,
        setActiveScene: true);

    bool success = await Bootloader.Instance.SceneTransitions.TransitionAsync(request);

Example load plus unload:

    var request = new SceneTransitionRequest(
        "GameplayScene",
        UnityEngine.SceneManagement.LoadSceneMode.Additive,
        true,
        "MenuScene");

    bool success = await Bootloader.Instance.SceneTransitions.TransitionAsync(request);

Ensure scene names are included in Build Settings or the project's applicable scene-loading configuration. Scene names are case-sensitive in practical project workflows; use the exact configured name.

Recommended practices:
- Use additive loading when the persistent Bootloader must remain alive.
- Avoid loading the Bootloader scene again.
- Don't start multiple transitions at the same time.
- Subscribe to SceneTransitionStartedEvent, SceneTransitionCompletedEvent, and SceneTransitionFailedEvent for UI or logging.
- Unloading is not transactional. If loading succeeds but a later unload fails, the newly loaded scene may remain loaded.
- The current manager temporarily enters GameState.Transitioning and restores the prior state if the state remains Transitioning.
- Test what happens when a target scene is already loaded and when a scene load or unload fails.
- Avoid relying on scene transition code from an object that is destroyed as part of the scene change.

The manager currently awaits Unity AsyncOperation with Task.Yield. Verify this approach on the exact Unity version and platform being targeted, particularly around cancellation and application shutdown.

============================================================
12. APPLICATION LIFECYCLE
============================================================

ApplicationLifecycleManager receives events forwarded by Bootloader's Unity callbacks:

- HandlePause(bool)
- HandleFocus(bool)
- HandleShutdown()

Subscribe to lifecycle events if a service needs to react:

    Bootloader.Instance.Events.Subscribe<ApplicationPausedEvent>(OnPaused);
    Bootloader.Instance.Events.Subscribe<ApplicationResumedEvent>(OnResumed);

Unsubscribe when the listener is no longer active.

Use pause events for work such as saving small critical state, pausing audio, or suspending expensive operations. Focus events are not identical to pause events; a window can lose focus without the application being fully paused.

Do not perform long blocking work inside Unity lifecycle callbacks. Mobile operating systems may terminate the application without a graceful quit callback.

============================================================
13. SHUTDOWN COORDINATION
============================================================

IGameShutdownHandler supports asynchronous cleanup:

    using System.Threading;
    using System.Threading.Tasks;

    public interface IGameShutdownHandler
    {
        int ShutdownOrder { get; }
        Task ShutdownAsync(CancellationToken cancellationToken);
    }

A shutdown handler might flush a small queue, close a network session, or release a resource that requires asynchronous cleanup.

Register handlers before shutdown begins:

    Bootloader.Instance.ShutdownCoordinator.Register(handler);

Handlers execute in descending ShutdownOrder in the current coordinator. Assign explicit values where ordering matters.

Important:
- Unity does not await an async OnApplicationQuit callback. Call and await Bootloader.ShutdownAsync() from an application-controlled shutdown flow before quitting if asynchronous cleanup must complete.
- OnDestroy is not a reliable place to wait for asynchronous operations.
- Shutdown cancellation policy needs to be intentional. If you cancel the same token passed to every handler immediately, handlers may be unable to finish cleanup.
- Ensure shutdown is idempotent; repeated requests should not shut services down multiple times.
- Avoid running Lifecycle.HandleShutdown independently in multiple code paths unless the lifecycle manager's idempotency is relied upon deliberately.
- The shutdown coordinator and Bootloader integration were added incrementally. Verify that there is only one authoritative shutdown path in the final implementation.

============================================================
14. ARCHITECTURE VALIDATOR
============================================================

Attach ArchitectureValidator to a scene GameObject. In Play mode, it checks whether expected systems exist and are in plausible states.

Run it from the component context menu using "Validate Architecture", or enable validateOnStart.

Validation is a smoke test, not a complete test suite. It cannot prove that:
- disk writes work;
- save data survives a restart;
- every event subscription is correct;
- scene unloading works;
- cancellation is race-free;
- all dependency combinations work.

Create separate functional tests for these behaviors.

============================================================
15. COMMON PATTERNS
============================================================

Pattern A: Publish an event after a successful operation

    if (operationSucceeded)
    {
        Bootloader.Instance.Events.Publish(
            new SomeOperationCompletedEvent());
    }

Prefer publishing completion events after the operation actually succeeds.

Pattern B: Use a service rather than a static global for mutable subsystem state

    var service = Bootloader.Instance.Services.Get<SomeService>();

Keep static state to a minimum, especially if it can survive between Play sessions in the Unity Editor or create test isolation problems.

Pattern C: Save stable identifiers

Instead of saving a ScriptableObject, prefab, or scene object reference directly, save a stable ID and resolve it through a data catalog after loading.

Pattern D: Fail early on missing required services

Use Get<T>() for required dependencies. Use TryGet<T>() only when absence is an expected condition.

Pattern E: Avoid managers that merely forward one method

Create a manager when it owns policy, state, ordering, or coordination. Otherwise, a direct method or event may be enough.

============================================================
16. BUILD AND TEST CHECKLIST
============================================================

Before using this architecture as the foundation of a larger project:

[ ] Compile every script together in the target Unity version.
[ ] Resolve duplicate class names and mismatched method signatures.
[ ] Confirm Bootloader is created exactly once and persists across scene changes.
[ ] Confirm initial state progresses from Uninitialized to Initializing to Ready.
[ ] Confirm a startup task runs before Ready.
[ ] Confirm a failing startup task prevents Ready and cleans up initialized services.
[ ] Confirm a cancelled startup task doesn't access destroyed Unity objects.
[ ] Confirm service dependencies initialize in dependency order.
[ ] Confirm services shut down in reverse initialization order.
[ ] Confirm a circular service dependency is rejected.
[ ] Confirm event listeners unsubscribe and aren't invoked after destruction.
[ ] Confirm GameDataStore change events fire for Set and Remove.
[ ] Save data, stop Play mode, start again, and load the saved data.
[ ] Test missing, corrupt, and older-version save files.
[ ] Load an additive scene, set it active, and unload a different scene.
[ ] Test transition failure and repeated transition requests.
[ ] Test pause/resume behavior on target devices.
[ ] Test shutdown on desktop and mobile separately.
[ ] Run ArchitectureValidator and investigate every failed check.

============================================================
17. IMPORTANT CURRENT IMPLEMENTATION CAVEATS
============================================================

This architecture was developed over several iterations. Some earlier snippets used different signatures. Before copying code from older chat messages, check the actual current source files.

In particular:
- IGameInstaller may have changed from Install(GameServiceRegistry) to Install(GameServiceRegistry, GameStartupTaskRegistry).
- CoreGameInstaller must match the current IGameInstaller signature.
- SceneTransitionManager may require both GameEventBus and GameStateMachine in its constructor.
- GameServiceRegistry.Register supports dependency Type parameters in the latest version described here.
- GameStartupPipeline.Configure expects an enumerable of IGameStartupTask.
- Bootloader startup, failure handling, and shutdown must not independently perform the same cleanup twice.
- The validator's expectations must match whether the startup pipeline has actually completed.
- Verify all referenced event types and properties exist in your current files.

Treat compilation and runtime testing as the source of truth. Do not keep adding new architecture layers until this integration pass is clean.

============================================================
18. RECOMMENDED DEVELOPMENT ORDER AFTER THE FOUNDATION
============================================================

1. Fix compile errors and establish a clean baseline.
2. Test the startup pipeline and service dependency ordering.
3. Test save/load persistence and scene transitions.
4. Add a small real subsystem using the service/event patterns.
5. Add automated Edit Mode or Play Mode tests for critical behavior.
6. Only add abstractions when a concrete requirement justifies them.

The architecture should serve the project, not become a project of its own. Prefer the smallest system that provides clear ownership, testability, and predictable lifecycle behavior.
"""
