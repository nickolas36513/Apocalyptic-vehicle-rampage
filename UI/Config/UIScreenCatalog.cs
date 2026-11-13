using UnityEngine;
using UnityEngine.UIElements;

[CreateAssetMenu(
    fileName = "UIScreenCatalog",
    menuName = "Game/UI/Screen Catalog")]
public sealed class UIScreenCatalog : ScriptableObject
{
    [SerializeField] private PanelSettings panelSettings;
    [SerializeField] private UIScreenDefinition[] screens;

    public PanelSettings PanelSettings => panelSettings;

    public int Count => screens == null ? 0 : screens.Length;

    public UIScreenDefinition GetScreen(int index)
    {
        return screens[index];
    }

    public bool TryGetScreen(
        string id,
        out UIScreenDefinition definition)
    {
        if (screens != null)
        {
            for (int i = 0; i < screens.Length; i++)
            {
                if (screens[i].ID == id)
                {
                    definition = screens[i];
                    return true;
                }
            }
        }

        definition = default;
        return false;
    }
}