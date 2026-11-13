using System;
using UnityEngine;

public sealed class PlayerProfileRuntimeService : IGameService
{
    private readonly GameDefinitionRegistry definitions;
    private readonly int startingCurrency;

    private bool hasShutDown;

    public PlayerProfileStore ProfileStore { get; private set; }

    /// <summary>
    /// The live profile object. Persist direct field changes with Save;
    /// inventory and purchase operations persist through their services.
    /// </summary>
    public PlayerProfileData Profile => ProfileStore?.CurrentProfile;
    public PlayerInventoryService Inventory { get; private set; }
    public PurchaseService Purchases { get; private set; }
    public PlayerProfileSelectionService Selection { get; private set; }
    public GameDefinitionRegistry Definitions => definitions;
    public bool IsInitialized { get; private set; }

    public PlayerProfileRuntimeService(
        GameDefinitionRegistry definitions,
        int startingCurrency)
    {
        this.definitions = definitions;
        this.startingCurrency = Mathf.Max(0, startingCurrency);
    }

    public void Initialize()
    {
        if (IsInitialized)
            return;

        if (hasShutDown)
        {
            throw new InvalidOperationException(
                "PlayerProfileRuntimeService cannot be initialized after shutdown.");
        }

        try
        {
            if (definitions == null)
            {
                throw new InvalidOperationException(
                    "PlayerProfileRuntimeService requires a GameDefinitionRegistry.");
            }

            if (!definitions.ValidateStartupCatalog(out string catalogReport))
            {
                throw new InvalidOperationException(
                    "GameDefinitionRegistry is invalid:\n" + catalogReport);
            }

            // Construct the store once. It is the only persistent profile owner.
            ProfileStore = new PlayerProfileStore();

            if (!ProfileStore.LoadOrCreate(
                    startingCurrency,
                    out string loadError))
            {
                throw new InvalidOperationException(
                    "Could not load or create the player profile. " +
                    loadError);
            }

            if (ProfileStore.CurrentProfile == null)
            {
                throw new InvalidOperationException(
                    "PlayerProfileStore completed loading without a profile.");
            }

            string selectedVehicleBeforeValidation =
                ProfileStore.CurrentProfile.selectedVehicleId;

            Inventory = PlayerInventoryService.CreateForStore(
                definitions, ProfileStore);

            if (ProfileStore.CurrentProfile.selectedVehicleId !=
                selectedVehicleBeforeValidation &&
                !ProfileStore.Save(out string normalizationError))
            {
                throw new InvalidOperationException(
                    "Could not persist the corrected selected vehicle. " +
                    normalizationError);
            }

            Purchases = new PurchaseService(
                definitions, ProfileStore, Inventory);
            Selection = new PlayerProfileSelectionService(
                definitions, ProfileStore, Inventory);

            IsInitialized = true;
        }
        catch
        {
            Purchases = null;
            Selection = null;
            Inventory = null;
            ProfileStore = null;
            IsInitialized = false;
            throw;
        }
    }

    public bool Save(out string error)
    {
        error = null;

        if (!IsInitialized || ProfileStore == null || Profile == null)
        {
            error = "Player profile runtime service is not initialized.";
            return false;
        }

        try
        {
            return ProfileStore.Save(out error);
        }
        catch (Exception exception)
        {
            error = $"Failed to save the player profile: {exception.Message}";
            return false;
        }
    }

    public void Shutdown()
    {
        if (hasShutDown)
            return;

        hasShutDown = true;

        // A failed or partial Initialize must never save an invalid profile.
        if (!IsInitialized || ProfileStore == null || Profile == null)
        {
            IsInitialized = false;
            return;
        }

        try
        {
            if (!ProfileStore.Save(out string error))
            {
                Debug.LogError(
                    "PlayerProfileRuntimeService could not save the profile " +
                    $"during shutdown: {error}");
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            IsInitialized = false;
            Purchases = null;
            Selection = null;
            Inventory = null;
            ProfileStore = null;
        }
    }
}
