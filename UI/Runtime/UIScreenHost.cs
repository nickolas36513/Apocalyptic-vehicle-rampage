using System;
using UnityEngine;
using UnityEngine.UIElements;

[DisallowMultipleComponent]
[RequireComponent(typeof(UIDocument))]
public sealed class UIScreenHost : MonoBehaviour
{
    private UIDocument document;
    private VisualElement screenRoot;

    private bool initialized;
    private bool visible;

    public string ScreenID { get; private set; }

    public UIDocument Document => document;

    public VisualElement Root => screenRoot;

    public bool IsInitialized => initialized;

    public bool IsVisible => initialized && visible;

    public event Action<UIScreenHost> RootReady;
    public event Action<UIScreenHost> RootReloaded;
    public event Action<UIScreenHost> Destroying;

    private void Awake()
    {
        document = GetComponent<UIDocument>();
    }

    public bool Configure(string screenID, out string error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(screenID))
        {
            error = "Screen ID cannot be empty.";
            return false;
        }

        if (document == null)
        {
            error = "UIDocument component is missing.";
            return false;
        }

        ScreenID = screenID;

        return BuildScreenRoot(out error);
    }

    private bool BuildScreenRoot(out string error)
    {
        error = null;

        if (document == null)
        {
            error = "UIDocument is missing.";
            return false;
        }

        VisualElement root = document.rootVisualElement;

        if (root == null)
        {
            error = $"UIDocument root is not available for '{ScreenID}'.";
            return false;
        }

        bool wasInitialized = initialized;

        screenRoot = root;
        initialized = true;

        ApplyVisibility();

        if (wasInitialized)
            RootReloaded?.Invoke(this);
        else
            RootReady?.Invoke(this);

        return true;
    }

    public bool SetVisible(bool state)
    {
        if (!initialized || screenRoot == null)
            return false;

        visible = state;
        ApplyVisibility();

        return true;
    }

    private void ApplyVisibility()
    {
        if (screenRoot != null)
        {
            screenRoot.style.display =
                visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    public bool TryGetElement<T>(
        string elementName,
        out T element)
        where T : VisualElement
    {
        element = null;

        if (!initialized ||
            screenRoot == null ||
            string.IsNullOrWhiteSpace(elementName))
        {
            return false;
        }

        element = screenRoot.Q<T>(elementName);

        return element != null;
    }

    public void Shutdown()
    {
        visible = false;
        initialized = false;

        screenRoot = null;
        document = null;

        RootReady = null;
        RootReloaded = null;
        Destroying = null;
    }

    private void OnDestroy()
    {
        Destroying?.Invoke(this);
        Shutdown();
    }
}