
using System;
using UnityEngine;

public sealed class PurchaseService
{
    private readonly GameDefinitionRegistry definitions;
    private readonly PlayerProfileStore profileStore;
    private readonly PlayerInventoryService sharedInventory;

    public PlayerProfileData Profile =>
        profileStore != null ? profileStore.CurrentProfile : null;

    public int Currency =>
        Profile != null ? Profile.currency : 0;

    public PurchaseService(
        GameDefinitionRegistry definitions,
        PlayerProfileStore profileStore)
        : this(definitions, profileStore, null)
    {
    }

    public PurchaseService(
        GameDefinitionRegistry definitions,
        PlayerProfileStore profileStore,
        PlayerInventoryService sharedInventory)
    {
        this.definitions = definitions;
        this.profileStore = profileStore;
        this.sharedInventory = sharedInventory;

        if (sharedInventory != null &&
            profileStore != null &&
            profileStore.CurrentProfile != null &&
            sharedInventory.Profile != profileStore.CurrentProfile)
        {
            throw new ArgumentException(
                "The shared inventory must use the store's current profile.",
                nameof(sharedInventory));
        }
    }

    public PurchaseResult TryPurchase(string definitionId)
    {
        if (Profile == null)
        {
            return PurchaseResult.Failed(
                PurchaseStatus.ProfileNotLoaded,
                "Player profile has not been loaded.");
        }

        if (definitions == null ||
            string.IsNullOrWhiteSpace(definitionId))
        {
            return PurchaseResult.Failed(
                PurchaseStatus.InvalidItem,
                "Invalid item ID or missing definition registry.");
        }

        if (!definitions.TryGet(definitionId, out ItemDefinition definition) ||
            definition == null)
        {
            return PurchaseResult.Failed(
                PurchaseStatus.InvalidItem,
                $"Item '{definitionId}' was not found.");
        }

        int price = definition.PurchasePrice;

        if (price < 0)
        {
            return PurchaseResult.Failed(
                PurchaseStatus.NotPurchasable,
                $"'{definition.DisplayName}' has no configured purchase price.");
        }

        PlayerInventoryService currentInventory = GetCurrentInventory();

        if (IsOwned(currentInventory, definition))
        {
            return PurchaseResult.Failed(
                PurchaseStatus.AlreadyOwned,
                "You already own this item.");
        }

        if (Profile.currency < price)
        {
            return PurchaseResult.Failed(
                PurchaseStatus.InsufficientFunds,
                "You do not have enough currency.");
        }

        bool saved = profileStore.TryCommit(profile =>
        {
            // Work only on the transaction candidate, never on the live
            // profile. The store publishes it after the file is saved.
            PlayerInventoryService inventory =
                new PlayerInventoryService(definitions, profile);

            if (IsOwned(inventory, definition))
            {
                throw new InvalidOperationException(
                    "You already own this item.");
            }

            if (profile.currency < price)
            {
                throw new InvalidOperationException(
                    "You do not have enough currency.");
            }

            bool granted;
            string grantError;

            if (definition is VehicleDefinition)
            {
                granted = inventory.OwnVehicle(
                    definitionId, out grantError);
            }
            else if (definition is WeaponDefinition)
            {
                granted = inventory.UnlockWeapon(
                    definitionId, out grantError);
            }
            else if (definition is UpgradeDefinition)
            {
                granted = inventory.OwnUpgrade(
                    definitionId, out grantError);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unsupported purchasable item type: {definition.GetType().Name}");
            }

            if (!granted)
            {
                throw new InvalidOperationException(
                    grantError ?? "Could not grant the purchased item.");
            }

            profile.currency -= price;
        }, out string error);

        if (!saved)
        {
            return PurchaseResult.Failed(
                PurchaseStatus.SaveFailed,
                error);
        }

        RebindDetachedSharedInventory();

        return PurchaseResult.Succeeded(
            $"Purchased {definition.DisplayName} for {price} coins.");
    }

    public PurchaseResult TrySelectOwnedVehicle(string definitionId)
    {
        if (Profile == null)
        {
            return PurchaseResult.Failed(
                PurchaseStatus.ProfileNotLoaded,
                "Player profile has not been loaded.");
        }

        if (definitions == null ||
            !definitions.TryGet(definitionId, out ItemDefinition definition) ||
            definition == null)
        {
            return PurchaseResult.Failed(
                PurchaseStatus.InvalidItem,
                "Vehicle definition was not found.");
        }

        if (!(definition is VehicleDefinition))
        {
            return PurchaseResult.Failed(
                PurchaseStatus.NotAVehicle,
                "The selected item is not a vehicle.");
        }

        PlayerInventoryService currentInventory = GetCurrentInventory();

        if (!currentInventory.OwnsVehicle(definitionId))
        {
            return PurchaseResult.Failed(
                PurchaseStatus.NotOwned,
                "You must purchase this vehicle first.");
        }

        bool saved = profileStore.TryCommit(profile =>
        {
            PlayerInventoryService inventory =
                new PlayerInventoryService(definitions, profile);

            if (!inventory.SelectVehicle(definitionId, out string selectError))
            {
                throw new InvalidOperationException(
                    selectError ?? "Could not select this vehicle.");
            }
        }, out string error);

        if (!saved)
        {
            return PurchaseResult.Failed(
                PurchaseStatus.SaveFailed,
                error);
        }

        RebindDetachedSharedInventory();

        return PurchaseResult.Succeeded(
            $"Selected {definition.DisplayName}.");
    }

    public bool Owns(string definitionId)
    {
        if (Profile == null || definitions == null ||
            !definitions.TryGet(definitionId, out ItemDefinition definition) ||
            definition == null)
        {
            return false;
        }

        PlayerInventoryService inventory = GetCurrentInventory();

        return IsOwned(inventory, definition);
    }

    public int GetOwnedQuantity(string definitionId)
    {
        return Owns(definitionId) ? 1 : 0;
    }

    /// <summary>
    /// Returns a mutable record from the current profile. Reacquire it after
    /// any profile commit instead of caching it across purchases or selection.
    /// </summary>
    public OwnedVehicleData GetVehicleState(string definitionId)
    {
        if (Profile == null || definitions == null)
            return null;

        PlayerInventoryService inventory = GetCurrentInventory();

        return inventory.GetOwnedVehicle(definitionId);
    }

    private static bool IsOwned(
        PlayerInventoryService inventory,
        ItemDefinition definition)
    {
        if (inventory == null || definition == null)
            return false;

        if (definition is VehicleDefinition)
            return inventory.OwnsVehicle(definition.DefinitionId);

        if (definition is WeaponDefinition)
            return inventory.HasWeaponUnlocked(definition.DefinitionId);

        if (definition is UpgradeDefinition)
            return inventory.HasUpgrade(definition.DefinitionId);

        return false;
    }

    private PlayerInventoryService GetCurrentInventory()
    {
        if (sharedInventory != null && sharedInventory.Profile == Profile)
            return sharedInventory;

        return new PlayerInventoryService(definitions, Profile);
    }

    private void RebindDetachedSharedInventory()
    {
        if (sharedInventory != null &&
            !sharedInventory.IsBoundToProfileStore &&
            Profile != null)
        {
            sharedInventory.RebindProfile(Profile);
        }
    }

}
