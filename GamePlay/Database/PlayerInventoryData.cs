
using System;
using System.Collections.Generic;

[Serializable]
public sealed class PlayerInventoryData
{
    public List<OwnedVehicleData> ownedVehicles =
        new List<OwnedVehicleData>();

    public List<string> unlockedWeaponIds =
        new List<string>();

    public List<string> ownedUpgradeIds =
        new List<string>();

    public void EnsureCollections()
    {
        if (ownedVehicles == null)
            ownedVehicles = new List<OwnedVehicleData>();

        if (unlockedWeaponIds == null)
            unlockedWeaponIds = new List<string>();

        if (ownedUpgradeIds == null)
            ownedUpgradeIds = new List<string>();

        foreach (OwnedVehicleData vehicle in ownedVehicles)
        {
            if (vehicle != null)
                vehicle.EnsureCollections();
        }
    }
}