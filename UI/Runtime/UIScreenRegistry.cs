using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class UIScreenRegistry : IGameService
{
    private sealed class ScreenInstance
    {
        public string ID;
        public string GroupID;

        public UIDocument Document;
        public UIScreenHost Host;
        public UIScreenController Controller;
    }

    private readonly Dictionary<string, ScreenInstance> screens =
        new Dictionary<string, ScreenInstance>(StringComparer.Ordinal);

    private readonly HashSet<string> creatingScreens = new HashSet<string>(StringComparer.Ordinal);

    private readonly Dictionary<string, string> activeScreens =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private readonly UIScreenCatalog catalog;

    private GameObject runtimeRoot;

    private Transform documentParent;
    private Transform controllerParent;

    private bool initialized;

    public bool IsInitialized => initialized;

    public int ScreenCount => initialized ? catalog.Count : 0;

    public UIScreenRegistry(UIScreenCatalog catalog)
    {
        this.catalog = catalog != null
            ? catalog
            : throw new ArgumentNullException(nameof(catalog));
    }

    public void Initialize()
    {
        if (initialized)
            return;

        ValidateCatalog();

        try
        {
            CreateRuntimeParents();

            initialized = true;

        }
        catch
        {
            DestroyAllScreens();
            DestroyRuntimeParents();

            throw;
        }
    }


    private void ValidateCatalog()
    {
        if (!UIScreenCatalogValidator.ValidateStructure(
                catalog,
                out string report))
        {
            throw new InvalidOperationException(
                "[UIScreenRegistry] Catalog validation failed:\n" +
                report);
        }

    }


    private void CreateRuntimeParents()
    {
        runtimeRoot = new GameObject("[UI Runtime]");

        UnityEngine.Object.DontDestroyOnLoad(runtimeRoot);

        GameObject documents = new GameObject("Documents");
        GameObject controllers = new GameObject("Controllers");

        documents.transform.SetParent(runtimeRoot.transform, false);
        controllers.transform.SetParent(runtimeRoot.transform, false);

        documentParent = documents.transform;
        controllerParent = controllers.transform;
    }

    private ScreenInstance CreateScreen(UIScreenDefinition definition)
    {
        GameObject documentObject = null;
        UIDocument document = null;
        UIScreenHost host = null;
        UIScreenController controller = null;

        try
        {
            // Create the runtime document GameObject.
            documentObject = new GameObject(
                $"{definition.ID}_Document");

            documentObject.transform.SetParent(
                documentParent,
                false);

            // Add and configure UIDocument.
            document = documentObject.AddComponent<UIDocument>();

            document.panelSettings = catalog.PanelSettings;
            document.visualTreeAsset = definition.SourceAsset;

            // Add the host after assigning the visual tree.
            host = documentObject.AddComponent<UIScreenHost>();

            if (!host.Configure(definition.ID, out string hostError))
            {
                throw new InvalidOperationException(
                    $"Could not configure screen '{definition.ID}': " +
                    hostError);
            }

            // Create the screen controller.
            controller = UnityEngine.Object.Instantiate(
                definition.ControllerPrefab,
                controllerParent,
                false);

            controller.name = $"{definition.ID}_Controller";

            controller.Bind(host);

            if (!host.SetVisible(false))
                throw new InvalidOperationException($"Could not hide screen '{definition.ID}'.");

            return new ScreenInstance
            {
                ID = definition.ID,
                GroupID = definition.GroupID,
                Document = document,
                Host = host,
                Controller = controller
            };
        }
        catch
        {
            if (controller != null)
            {
                controller.Unbind();
                DestroyObject(controller.gameObject);
            }

            if (host != null)
                host.Shutdown();

            if (documentObject != null)
                DestroyObject(documentObject);

            throw;
        }
    }

    public bool Show(string screenID)
    {
        return Show(screenID, out _);
    }


    public bool Show(string screenID, out string error)
    {
        error = null;

        // Bind the target while the current screen is still visible.
        if (!TryGetOrCreateInstance(screenID, out ScreenInstance target, out error))
            return false;

        if (!target.Host.IsInitialized)
        {
            error = $"Screen '{screenID}' is not ready.";
            return false;
        }

        string groupID = target.GroupID;

        ScreenInstance previous = null;

        if (activeScreens.TryGetValue(groupID, out string previousID))
        {
            if (previousID == screenID)
            {
                // Repair stale visibility if something changed externally.
                return TryShow(target, out error);
            }

            if (TryGetInstance(previousID, out previous))
            {
                if (!previous.Controller.Hide())
                {
                    error = $"Could not hide previous screen '{previousID}'.";
                    return false;
                }
            }
            else
            {
                // Remove an invalid active-screen reference.
                activeScreens.Remove(groupID);
            }
        }

        if (!TryShow(target, out error))
        {
            // Restore the previous screen if target activation fails.
            if (previous != null)
            {
                if (TryShow(previous, out string rollbackError))
                {
                    activeScreens[groupID] = previous.ID;
                }
                else
                {
                    activeScreens.Remove(groupID);

                    Debug.LogError(
                        $"[UIScreenRegistry] Transition rollback failed. " +
                        $"Screen: '{previous.ID}'. Error: {rollbackError}");
                }
            }
            else
            {
                activeScreens.Remove(groupID);
            }

            return false;
        }

        activeScreens[groupID] = screenID;

        return true;
    }

    private static bool TryShow(
        ScreenInstance instance,
        out string error)
    {
        error = null;

        try
        {
            if (instance == null ||
                instance.Controller == null ||
                instance.Host == null ||
                !instance.Host.IsInitialized)
            {
                error = "Screen instance is not ready.";
                return false;
            }

            if (!instance.Controller.Show())
            {
                error = $"Controller failed to show '{instance.ID}'.";
                return false;
            }

            return true;
        }
        catch (System.Exception exception)
        {
            error = exception.Message;

            Debug.LogException(exception);

            return false;
        }
    }

    public bool Hide(string screenID)
    {
        if (!TryGetInstance(screenID, out ScreenInstance instance))
            return Contains(screenID);

        bool result;

        try
        {
            result = instance.Controller.Hide();
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            return false;
        }

        if (!result)
            return false;

        if (activeScreens.TryGetValue(
                instance.GroupID,
                out string activeID) &&
            activeID == screenID)
        {
            activeScreens.Remove(instance.GroupID);
        }

        return true;
    }


    public void HideGroup(string groupID)
    {
        if (string.IsNullOrWhiteSpace(groupID))
            return;

        if (activeScreens.TryGetValue(groupID, out string screenID))
            Hide(screenID);
    }

    public void HideAll()
    {
        foreach (ScreenInstance instance in screens.Values)
            instance.Controller.Hide();

        activeScreens.Clear();
    }

    public bool IsVisible(string screenID)
    {
        return TryGetInstance(screenID, out ScreenInstance instance) &&
               instance.Host.IsVisible;
    }

    public bool Contains(string screenID)
    {
        return initialized && !string.IsNullOrWhiteSpace(screenID) &&
               catalog.TryGetScreen(screenID, out _);
    }

    public bool TryGetController<T>(
        string screenID,
        out T controller)
        where T : UIScreenController
    {
        controller = null;

        if (!TryGetOrCreateInstance(screenID, out ScreenInstance instance, out _))
            return false;

        controller = instance.Controller as T;

        return controller != null;
    }

    private bool TryGetInstance(
        string screenID,
        out ScreenInstance instance)
    {
        instance = null;

        if (!initialized || string.IsNullOrWhiteSpace(screenID))
            return false;

        return screens.TryGetValue(screenID, out instance);
    }

    private bool TryGetOrCreateInstance(
        string screenID,
        out ScreenInstance instance,
        out string error)
    {
        error = null;

        if (TryGetInstance(screenID, out instance))
            return true;

        if (!initialized || string.IsNullOrWhiteSpace(screenID) ||
            !catalog.TryGetScreen(screenID, out UIScreenDefinition definition))
        {
            error = $"Screen '{screenID}' is not registered.";
            return false;
        }

        if (definition.SourceAsset == null || definition.ControllerPrefab == null)
        {
            error = $"Screen '{screenID}' requires a visual tree and a controller prefab.";
            return false;
        }

        // A binding callback can request a controller before its own binding has finished.
        if (!creatingScreens.Add(screenID))
        {
            error = $"Screen '{screenID}' is already being prepared.";
            return false;
        }

        try
        {
            instance = CreateScreen(definition);
            screens.Add(screenID, instance);
            return true;
        }
        catch (Exception exception)
        {
            error = $"Could not prepare screen '{screenID}': {exception.Message}";
            Debug.LogException(exception);
            return false;
        }
        finally
        {
            creatingScreens.Remove(screenID);
        }
    }

    public void Shutdown()
    {
        if (!initialized &&
            screens.Count == 0 &&
            runtimeRoot == null)
        {
            return;
        }

        initialized = false;

        HideAll();
        DestroyAllScreens();
        DestroyRuntimeParents();

    }

    private void DestroyAllScreens()
    {
        foreach (ScreenInstance instance in screens.Values)
        {
            if (instance.Controller != null)
            {
                instance.Controller.Unbind();
                DestroyObject(instance.Controller.gameObject);
            }

            if (instance.Host != null)
                instance.Host.Shutdown();

            if (instance.Document != null)
                DestroyObject(instance.Document.gameObject);
        }

        screens.Clear();
        activeScreens.Clear();
    }

    private void DestroyRuntimeParents()
    {
        if (runtimeRoot != null)
            DestroyObject(runtimeRoot);

        runtimeRoot = null;
        documentParent = null;
        controllerParent = null;
    }

    private static void DestroyObject(GameObject target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            UnityEngine.Object.Destroy(target);
        else
            UnityEngine.Object.DestroyImmediate(target);
    }
}
