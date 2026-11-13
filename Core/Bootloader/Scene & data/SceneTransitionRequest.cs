using UnityEngine.SceneManagement;

public sealed class SceneTransitionRequest
{
    public string SceneName { get; }
    public LoadSceneMode LoadMode { get; }
    public bool SetActiveScene { get; }
    public string[] ScenesToUnload { get; }

    public SceneTransitionRequest(
        string sceneName,
        LoadSceneMode loadMode = LoadSceneMode.Additive,
        bool setActiveScene = true,
        params string[] scenesToUnload)
    {
        SceneName = sceneName;
        LoadMode = loadMode;
        SetActiveScene = setActiveScene;

        ScenesToUnload = scenesToUnload != null
            ? (string[])scenesToUnload.Clone()
            : System.Array.Empty<string>();
    }
}
