
using UnityEngine;

public enum CharacterArchetype
{
    // Values match the existing serialized assets: Mechanic=0,
    // Carnage=1, Recon=2. Keep numeric values stable for Unity YAML.
    Mechanic = 0,
    Recon = 2,
    Carnage = 1
}

[CreateAssetMenu(
    fileName = "CharacterDefinition",
    menuName = "Game/Definitions/Character")]
public sealed class CharacterDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string definitionId;
    [SerializeField] private string displayName;

    [TextArea(2, 5)]
    [SerializeField] private string description;

    [Header("Presentation")]
    [SerializeField] private Sprite portrait;

    [Header("Gameplay")]
    [SerializeField] private CharacterArchetype archetype;
    [SerializeField] private string startingVehicleId;

    public string DefinitionId => definitionId;
    public string DisplayName => displayName;
    public string Description => description;

    public Sprite Portrait => portrait;

    public CharacterArchetype Archetype => archetype;
    public string StartingVehicleId => startingVehicleId;

    public string ArchetypeLabel => archetype.ToString();

#if UNITY_EDITOR
    private void OnValidate()
    {
        definitionId = definitionId?.Trim() ?? string.Empty;
        displayName = displayName?.Trim() ?? string.Empty;
        startingVehicleId = startingVehicleId?.Trim() ?? string.Empty;
    }
#endif
}
