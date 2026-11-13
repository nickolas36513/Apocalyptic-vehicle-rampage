using UnityEngine.SceneManagement;

public readonly struct SceneTransitionStartedEvent
{
    public readonly SceneTransitionRequest Request;

    public SceneTransitionStartedEvent(SceneTransitionRequest request)
    {
        Request = request;
    }
}

public readonly struct SceneTransitionCompletedEvent
{
    public readonly string SceneName;
    public readonly Scene LoadedScene;

    public SceneTransitionCompletedEvent(
        string sceneName,
        Scene loadedScene)
    {
        SceneName = sceneName;
        LoadedScene = loadedScene;
    }
}

public readonly struct SceneTransitionFailedEvent
{
    public readonly string SceneName;
    public readonly string Reason;

    public SceneTransitionFailedEvent(
        string sceneName,
        string reason)
    {
        SceneName = sceneName;
        Reason = reason;
    }
}