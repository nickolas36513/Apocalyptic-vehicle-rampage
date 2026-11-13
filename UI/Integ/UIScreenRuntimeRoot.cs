using UnityEngine;

[DisallowMultipleComponent]
public sealed class UIScreenRuntimeRoot : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private UIScreenCatalog catalog;

    [Header("Runtime Parents")]
    [SerializeField] private Transform documentParent;
    [SerializeField] private Transform controllerParent;

    public UIScreenCatalog Catalog => catalog;

    public Transform DocumentParent => documentParent;

    public Transform ControllerParent =>
        controllerParent != null
            ? controllerParent
            : documentParent;

    public bool IsConfigured =>
        catalog != null &&
        documentParent != null;
}