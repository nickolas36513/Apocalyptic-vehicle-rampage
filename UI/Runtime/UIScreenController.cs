using UnityEngine;

public abstract class UIScreenController : MonoBehaviour
{
    private UIScreenHost host;

    private bool bound;
    private bool uiBound;

    public UIScreenHost Host => host;

    public bool IsBound => bound;

    public bool IsVisible => host != null && host.IsVisible;

    public void Bind(UIScreenHost screenHost)
    {
        if (screenHost == null)
        {
            Debug.LogError(
                $"{GetType().Name}: Cannot bind to a null UIScreenHost.",
                this);

            return;
        }

        if (bound)
            Unbind();

        host = screenHost;

        host.RootReady += OnRootReady;
        host.RootReloaded += OnRootReloaded;
        host.Destroying += OnHostDestroying;

        bound = true;

        if (host.IsInitialized)
            BindUI();
    }

    private void OnRootReady(UIScreenHost screenHost)
    {
        if (!bound || screenHost != host)
            return;

        BindUI();
    }

    private void OnRootReloaded(UIScreenHost screenHost)
    {
        if (!bound || screenHost != host)
            return;

        UnbindUI();
        BindUI();
    }

    private void OnHostDestroying(UIScreenHost screenHost)
    {
        if (screenHost != host)
            return;

        Unbind();
    }


    private void BindUI()
    {
        if (!bound || uiBound || host == null || !host.IsInitialized)
            return;

        // Mark bound first so partial initialization can be cleaned up.
        uiBound = true;

        try
        {
            OnBindUI();
        }
        catch
        {
            try
            {
                OnUnbindUI();
            }
            finally
            {
                uiBound = false;
            }

            throw;
        }
    }

    public bool Show()
    {
        if (!bound || host == null)
            return false;

        if (host.IsVisible)
            return true;

        if (!host.SetVisible(true))
            return false;

        try
        {
            OnShown();
            return true;
        }
        catch
        {
            host.SetVisible(false);
            throw;
        }
    }

    public bool Hide()
    {
        if (!bound || host == null)
            return false;

        if (!host.IsVisible)
            return true;

        if (!host.SetVisible(false))
            return false;

        OnHidden();

        return true;
    }


    private void UnbindUI()
    {
        if (!uiBound)
            return;

        OnUnbindUI();

        uiBound = false;
    }


    public bool TryGetElement<T>(
        string elementName,
        out T element)
        where T : UnityEngine.UIElements.VisualElement
    {
        element = null;

        return host != null &&
               host.TryGetElement(elementName, out element);
    }

    public void Unbind()
    {
        if (!bound)
            return;

        UnbindUI();

        if (host != null)
        {
            host.RootReady -= OnRootReady;
            host.RootReloaded -= OnRootReloaded;
            host.Destroying -= OnHostDestroying;
        }

        host = null;
        bound = false;
    }

    protected abstract void OnBindUI();

    protected virtual void OnUnbindUI() { }

    protected virtual void OnShown() { }

    protected virtual void OnHidden() { }

    protected virtual void OnDestroy()
    {
        Unbind();
    }
}