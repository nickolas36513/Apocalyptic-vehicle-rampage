
using UnityEngine;

public enum ItemCategory
{
    Vehicle,
    Weapon,
    Upgrade
}

public abstract class ItemDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string definitionId;
    [SerializeField] private string displayName;

    [TextArea(2, 5)]
    [SerializeField] private string description;

    [Header("Presentation")]
    [SerializeField] private Sprite icon;

    public string DefinitionId => definitionId;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;

    public abstract ItemCategory Category { get; }

    // Negative means purchasing is not configured for this definition.
    public virtual int PurchasePrice => -1;

#if UNITY_EDITOR
    protected virtual void OnValidate()
    {
        definitionId = definitionId != null
            ? definitionId.Trim()
            : string.Empty;

        displayName = displayName != null
            ? displayName.Trim()
            : string.Empty;
    }
#endif
}
