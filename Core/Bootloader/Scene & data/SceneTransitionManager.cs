
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SceneTransitionManager
{
    private readonly GameEventBus eventBus;
    private readonly GameStateMachine stateMachine;

    public bool IsTransitioning { get; private set; }

    public string ActiveSceneName =>
        SceneManager.GetActiveScene().name;

    public SceneTransitionManager(
        GameEventBus eventBus,
        GameStateMachine stateMachine)
    {
        this.eventBus = eventBus ??
            throw new ArgumentNullException(nameof(eventBus));

        this.stateMachine = stateMachine ??
            throw new ArgumentNullException(nameof(stateMachine));
    }

    public async Task<bool> TransitionAsync(
        SceneTransitionRequest request,
        Action<float> onProgress = null)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.SceneName))
            return Fail(request.SceneName, "Scene name is empty.");

        if (IsTransitioning)
            return Fail(
                request.SceneName,
                "Another scene transition is already running.");

        GameState previousState = stateMachine.CurrentState;

        if (previousState == GameState.ShuttingDown ||
            previousState == GameState.Shutdown ||
            previousState == GameState.Uninitialized ||
            previousState == GameState.Initializing ||
            previousState == GameState.Transitioning)
        {
            return Fail(
                request.SceneName,
                $"Cannot transition from state {previousState}.");
        }

        if (!Application.CanStreamedLevelBeLoaded(request.SceneName))
        {
            return Fail(
                request.SceneName,
                $"Scene '{request.SceneName}' is not available to load.");
        }

        IsTransitioning = true;

        if (!stateMachine.TryChangeState(GameState.Transitioning))
        {
            IsTransitioning = false;

            return Fail(
                request.SceneName,
                "Could not enter Transitioning state.");
        }

        try
        {
            PublishStarted(request);

            // Find an existing loaded scene first.
            Scene targetScene = FindLoadedScene(request.SceneName);

            // Load the scene only if it isn't already loaded.
            if (!targetScene.IsValid() || !targetScene.isLoaded)
            {
                AsyncOperation loadOperation =
                    SceneManager.LoadSceneAsync(
                        request.SceneName,
                        request.LoadMode);

                if (loadOperation == null)
                {
                    throw new InvalidOperationException(
                        $"Could not start loading '{request.SceneName}'.");
                }

                while (!loadOperation.isDone)
                {
                    ReportProgress(
                        onProgress,
                        Mathf.Clamp01(loadOperation.progress));

                    await Task.Yield();
                }

                ReportProgress(onProgress, 1f);

                targetScene = FindLoadedScene(request.SceneName);
            }
            else
            {
                // Scene was already loaded.
                ReportProgress(onProgress, 1f);
            }

            // Validate the loaded scene.
            if (!targetScene.IsValid() || !targetScene.isLoaded)
            {
                throw new InvalidOperationException(
                    $"Scene '{request.SceneName}' did not load correctly.");
            }

            // Don't continue if another operation changed the game state.
            if (stateMachine.CurrentState != GameState.Transitioning)
            {
                throw new InvalidOperationException(
                    "Application state changed during scene transition. " +
                    $"Current state: {stateMachine.CurrentState}");
            }

            // Set the active scene only when necessary.
            if (request.SetActiveScene)
            {
                Scene activeScene = SceneManager.GetActiveScene();

                if (activeScene.handle != targetScene.handle)
                {
                    bool activated =
                        SceneManager.SetActiveScene(targetScene);

                    if (!activated)
                    {
                        throw new InvalidOperationException(
                            $"Could not activate scene '{request.SceneName}'.\n" +
                            $"Target valid: {targetScene.IsValid()}\n" +
                            $"Target loaded: {targetScene.isLoaded}\n" +
                            $"Target handle: {targetScene.handle}\n" +
                            $"Active scene: {activeScene.name}\n" +
                            $"Loaded scene count: {SceneManager.sceneCount}");
                    }
                }
            }

            // Unload requested scenes after the target is ready.
            foreach (string sceneName in request.ScenesToUnload)
            {
                if (string.IsNullOrWhiteSpace(sceneName) ||
                    sceneName == request.SceneName)
                {
                    continue;
                }

                Scene sceneToUnload = FindLoadedScene(sceneName);

                if (!sceneToUnload.IsValid() ||
                    !sceneToUnload.isLoaded)
                {
                    continue;
                }

                // Never unload the currently active scene.
                if (SceneManager.GetActiveScene().handle ==
                    sceneToUnload.handle)
                {
                    Debug.LogWarning(
                        $"[SceneTransitionManager] Skipping unload of " +
                        $"active scene '{sceneName}'.");

                    continue;
                }

                AsyncOperation unloadOperation =
                    SceneManager.UnloadSceneAsync(sceneToUnload);

                if (unloadOperation == null)
                {
                    throw new InvalidOperationException(
                        $"Could not unload scene '{sceneName}'.");
                }

                while (!unloadOperation.isDone)
                    await Task.Yield();
            }

            PublishCompleted(request.SceneName, targetScene);

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"[SceneTransitionManager] Transition failed.\n" +
                $"Target Scene: {request.SceneName}\n" +
                $"Exception: {exception}");

            PublishFailed(request.SceneName, exception.Message);

            return false;
        }
        finally
        {
            IsTransitioning = false;

            // Never overwrite a shutdown or another state transition.
            if (stateMachine.CurrentState == GameState.Transitioning)
            {
                if (!stateMachine.TryChangeState(previousState))
                {
                    Debug.LogError(
                        "[SceneTransitionManager] Could not restore " +
                        $"previous state: {previousState}");
                }
            }
        }
    }

    private static void ReportProgress(
        Action<float> callback,
        float progress)
    {
        if (callback == null)
            return;

        try
        {
            callback(Mathf.Clamp01(progress));
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static Scene FindLoadedScene(string sceneName)
    {
        // Prefer the active scene if its name matches.
        Scene activeScene = SceneManager.GetActiveScene();

        if (activeScene.IsValid() &&
            activeScene.isLoaded &&
            activeScene.name == sceneName)
        {
            return activeScene;
        }

        // Otherwise search all loaded scenes.
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);

            if (scene.IsValid() &&
                scene.isLoaded &&
                scene.name == sceneName)
            {
                return scene;
            }
        }

        return default;
    }

    private void PublishStarted(SceneTransitionRequest request)
    {
        try
        {
            eventBus.Publish(
                new SceneTransitionStartedEvent(request));
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private void PublishCompleted(
        string sceneName,
        Scene scene)
    {
        try
        {
            eventBus.Publish(
                new SceneTransitionCompletedEvent(
                    sceneName,
                    scene));
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private void PublishFailed(
        string sceneName,
        string reason)
    {
        try
        {
            eventBus.Publish(
                new SceneTransitionFailedEvent(
                    sceneName,
                    reason));
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private bool Fail(string sceneName, string reason)
    {
        PublishFailed(sceneName, reason);
        return false;
    }
}
