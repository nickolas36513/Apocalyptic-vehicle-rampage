
using System;
using System.Collections.Generic;
using UnityEngine;

public enum WeaponType
{
    Primary = 0,
    Secondary = 1,
    Melee = 2
}

[CreateAssetMenu(
    fileName = "WeaponDefinition",
    menuName = "Game/Definitions/Weapon")]
public sealed class WeaponDefinition : ItemDefinition
{
    [Header("Classification")]
    [SerializeField] private WeaponType slotType;

    [Header("Prefab")]
    [SerializeField] private GameObject weaponPrefab;

    [Header("Economy")]
    [SerializeField, Min(0)] private int purchasePrice;

    [Header("Combat Statistics")]
    [SerializeField, Min(0f)]
    private float damage = 10f;

    [SerializeField, Min(0f)]
    private float fireRate = 2f;

    [SerializeField, Min(0f)]
    private float range = 50f;

    [Header("Optional Vehicle Restrictions")]
    [Tooltip("Empty means compatible with all vehicles.")]
    [SerializeField]
    private List<string> compatibleVehicleIds =
        new List<string>();

    public override ItemCategory Category => ItemCategory.Weapon;
    public WeaponType SlotType => slotType;

    public GameObject WeaponPrefab => weaponPrefab;
    public override int PurchasePrice => purchasePrice;

    public float Damage => damage;
    public float FireRate => fireRate;
    public float Range => range;

    public IReadOnlyList<string> CompatibleVehicleIds =>
        compatibleVehicleIds;

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(DefinitionId) &&
        weaponPrefab != null;

    public bool IsCompatibleWith(VehicleDefinition vehicle)
    {
        if (vehicle == null)
            return false;

        if (compatibleVehicleIds == null ||
            compatibleVehicleIds.Count == 0)
            return true;

        string vehicleId = vehicle.DefinitionId;

        if (string.IsNullOrWhiteSpace(vehicleId))
            return false;

        for (int i = 0; i < compatibleVehicleIds.Count; i++)
        {
            if (string.Equals(
                compatibleVehicleIds[i],
                vehicleId,
                StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        purchasePrice = Mathf.Max(0, purchasePrice);
        damage = Mathf.Max(0f, damage);
        fireRate = Mathf.Max(0f, fireRate);
        range = Mathf.Max(0f, range);

        if (compatibleVehicleIds == null)
            compatibleVehicleIds = new List<string>();

        if (string.IsNullOrWhiteSpace(DefinitionId))
        {
            Debug.LogWarning(
                $"Weapon '{name}' has no DefinitionId.",
                this);
        }

        if (weaponPrefab == null)
        {
            Debug.LogWarning(
                $"Weapon '{name}' has no prefab.",
                this);
        }
    }
#endif
}
