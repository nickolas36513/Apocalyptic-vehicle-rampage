
using System.Collections.Generic;
using UnityEngine;

public enum VehicleStatType
{
    MaxHealth,
    Acceleration,
    MaxSpeed,
    Handling,
    MaxFuel,
    FuelConsumption
}

public enum StatModifierType
{
    Flat,
    Percentage
}

[CreateAssetMenu(
    fileName = "UpgradeDefinition",
    menuName = "Game/Definitions/Upgrade")]
public sealed class UpgradeDefinition : ItemDefinition
{
    [Min(-1)] [SerializeField] private int purchasePrice = -1;

    [Header("Upgrade")]
    [SerializeField] private UpgradeSlotType slotType;

    [Header("Stat Modifier")]
    [SerializeField] private VehicleStatType affectedStat;
    [SerializeField] private StatModifierType modifierType;

    [SerializeField] private float modifierValue = 10f;

    [Header("Progression")]
    [Min(1)] [SerializeField] private int maxLevel = 1;

    [Header("Compatibility")]
    [SerializeField]
    private List<string> compatibleVehicleIds =
        new List<string>();

    public override ItemCategory Category => ItemCategory.Upgrade;
    public override int PurchasePrice => purchasePrice;

    public UpgradeSlotType SlotType => slotType;
    public VehicleStatType AffectedStat => affectedStat;
    public StatModifierType ModifierType => modifierType;
    public float ModifierValue => modifierValue;
    public int MaxLevel => maxLevel;

    public IReadOnlyList<string> CompatibleVehicleIds =>
        compatibleVehicleIds;

    public bool IsCompatibleWith(VehicleDefinition vehicle)
    {
        if (vehicle == null)
            return false;

        if (compatibleVehicleIds == null ||
            compatibleVehicleIds.Count == 0)
        {
            return true;
        }

        for (int i = 0; i < compatibleVehicleIds.Count; i++)
        {
            if (compatibleVehicleIds[i] ==
                vehicle.DefinitionId)
            {
                return true;
            }
        }

        return false;
    }
}
