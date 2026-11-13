using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Validates the single gameplay camera rig owned by a loaded scene.</summary>
public static class SceneCameraValidator
{
    public static bool TryResolve(
        Scene scene,
        out CameraController controller,
        out string error)
    {
        controller = null;
        error = null;

        if (!scene.IsValid() || !scene.isLoaded)
        {
            error = "The camera's owning scene must be loaded.";
            return false;
        }

        Camera sceneCamera = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Camera candidate in root.GetComponentsInChildren<Camera>())
            {
                if (!candidate.isActiveAndEnabled)
                    continue;

                if (sceneCamera != null)
                {
                    error = $"Scene '{scene.name}' has more than one active Camera. " +
                            "Use a single Camera for gameplay and Garage.";
                    return false;
                }

                sceneCamera = candidate;
            }
        }

        if (sceneCamera == null)
        {
            error = $"Scene '{scene.name}' has no active Camera. " +
                    "Add the gameplay camera rig to this scene.";
            return false;
        }

        CameraController candidateController = sceneCamera.GetComponent<CameraController>();
        if (candidateController == null || !candidateController.isActiveAndEnabled)
        {
            error = $"Camera '{sceneCamera.name}' in scene '{scene.name}' " +
                    "requires an enabled CameraController on the same GameObject.";
            return false;
        }

        controller = candidateController;
        return true;
    }
}
