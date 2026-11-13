
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public sealed class PlayerProfileStore
{
    private const int CurrentSchemaVersion = 2;

    private readonly string filePath;

    public PlayerProfileData CurrentProfile { get; private set; }
    public bool WasCreatedDuringLoad { get; private set; }

    internal event Action<PlayerProfileData> ProfileCommitted;

    public PlayerProfileStore(string customFilePath = null)
    {
        filePath = string.IsNullOrWhiteSpace(customFilePath)
            ? Path.Combine(
                Application.persistentDataPath,
                "player-profile.json")
            : customFilePath;
    }

    public bool LoadOrCreate(int startingCurrency, out string error)
    {
        error = null;
        WasCreatedDuringLoad = false;

        if (!TryRecoverBackup(out error))
            return false;

        string backupPath = filePath + ".bak";
        if (File.Exists(filePath) &&
            !IsValidProfileFile(filePath) &&
            File.Exists(backupPath))
        {
            if (!IsValidProfileFile(backupPath, CurrentSchemaVersion))
            {
                error = "The player profile is corrupt and its backup is unreadable or uses an unsupported schema.";
                return false;
            }

            try
            {
                // The main file is unreadable, so restore the preserved
                // previous save before parsing or normalizing anything.
                File.Copy(backupPath, filePath, true);
            }
            catch (Exception exception)
            {
                error = "The player profile is corrupt and its backup could " +
                    $"not be restored: {exception.Message}";
                return false;
            }
        }

        if (File.Exists(filePath))
        {
            try
            {
                string json = File.ReadAllText(filePath);

                PlayerProfileData loaded =
                    JsonUtility.FromJson<PlayerProfileData>(json);

                if (loaded == null)
                {
                    error = "The player profile is empty or invalid.";
                    return false;
                }

                if (loaded.schemaVersion > CurrentSchemaVersion)
                {
                    error = $"Player profile schema {loaded.schemaVersion} is newer than supported schema {CurrentSchemaVersion}.";
                    return false;
                }

                bool needsWrite = loaded.schemaVersion < CurrentSchemaVersion;
                string beforeNormalization = JsonUtility.ToJson(loaded);

                // Preserve inventory data if an earlier build already
                // saved the new structure using schema version 1.
                if (loaded.schemaVersion < CurrentSchemaVersion)
                {
                    LegacyPlayerProfileData legacy =
                        JsonUtility.FromJson<LegacyPlayerProfileData>(json);

                    if (legacy != null &&
                        HasLegacyContent(legacy))
                    {
                        if (HasInventoryContent(loaded))
                            MergeLegacyData(loaded, legacy);
                        else
                            loaded = MigrateLegacyProfile(legacy);

                        needsWrite = true;
                    }
                }

                Normalize(loaded);

                needsWrite |= beforeNormalization != JsonUtility.ToJson(loaded);

                if (needsWrite && !TryWrite(loaded, out error))
                    return false;

                CurrentProfile = loaded;
                TryDeleteFile(filePath + ".tmp");
                return true;
            }
            catch (Exception exception)
            {
                error = $"Failed to load player profile: {exception.Message}";
                return false;
            }
        }

        PlayerProfileData newProfile = new PlayerProfileData
        {
            schemaVersion = CurrentSchemaVersion,
            currency = Mathf.Max(0, startingCurrency)
        };

        Normalize(newProfile);

        if (!TryWrite(newProfile, out error))
            return false;

        CurrentProfile = newProfile;
        WasCreatedDuringLoad = true;
        return true;
    }

    public bool TryCommit(
        Action<PlayerProfileData> mutation,
        out string error)
    {
        error = null;

        if (CurrentProfile == null)
        {
            error = "Player profile has not been loaded.";
            return false;
        }

        if (mutation == null)
        {
            error = "Profile mutation is null.";
            return false;
        }

        PlayerProfileData candidate;

        try
        {
            // Mutate a clone so a failed transaction cannot partially
            // modify the live profile.
            candidate = JsonUtility.FromJson<PlayerProfileData>(
                JsonUtility.ToJson(CurrentProfile));

            if (candidate == null)
            {
                error = "Could not create a profile transaction.";
                return false;
            }

            Normalize(candidate);
            mutation(candidate);
            Normalize(candidate);
        }
        catch (Exception exception)
        {
            error = $"Profile transaction failed: {exception.Message}";
            return false;
        }

        if (!TryWrite(candidate, out error))
            return false;

        // Publish the new in-memory profile only after saving succeeds.
        CurrentProfile = candidate;
        NotifyProfileCommitted(candidate);
        return true;
    }

    public bool TryCommitMutation(
        Func<PlayerProfileData, string> mutation,
        out string error)
    {
        error = null;

        if (CurrentProfile == null)
        {
            error = "Player profile has not been loaded.";
            return false;
        }

        if (mutation == null)
        {
            error = "Profile mutation is null.";
            return false;
        }

        PlayerProfileData candidate;

        try
        {
            candidate = JsonUtility.FromJson<PlayerProfileData>(
                JsonUtility.ToJson(CurrentProfile));

            if (candidate == null)
            {
                error = "Could not create a profile transaction.";
                return false;
            }

            Normalize(candidate);

            string mutationError = mutation(candidate);
            if (!string.IsNullOrEmpty(mutationError))
            {
                error = mutationError;
                return false;
            }

            Normalize(candidate);
        }
        catch (Exception exception)
        {
            error = $"Profile transaction failed: {exception.Message}";
            return false;
        }

        if (!TryWrite(candidate, out error))
            return false;

        CurrentProfile = candidate;
        NotifyProfileCommitted(candidate);
        return true;
    }

    private void NotifyProfileCommitted(PlayerProfileData profile)
    {
        if (ProfileCommitted == null)
            return;

        foreach (Action<PlayerProfileData> handler in
                 ProfileCommitted.GetInvocationList())
        {
            try
            {
                handler(profile);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }

    public bool Save(out string error)
    {
        error = null;

        if (CurrentProfile == null)
        {
            error = "Player profile has not been loaded.";
            return false;
        }

        Normalize(CurrentProfile);
        return TryWrite(CurrentProfile, out error);
    }

    private bool TryWrite(PlayerProfileData profile, out string error)
    {
        error = null;

        string temporaryPath = filePath + ".tmp";
        string backupPath = filePath + ".bak";
        string backupTemporaryPath = backupPath + ".tmp";

        try
        {
            if (!TryRecoverBackup(out error))
                return false;

            string directory = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string json = JsonUtility.ToJson(profile, true);
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);

            using (FileStream stream = new FileStream(
                       temporaryPath, FileMode.CreateNew,
                       FileAccess.Write, FileShare.None))
            using (StreamWriter writer = new StreamWriter(
                       stream, new UTF8Encoding(false)))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush();
            }

            string stagedJson = File.ReadAllText(temporaryPath);
            PlayerProfileData stagedProfile =
                JsonUtility.FromJson<PlayerProfileData>(stagedJson);
            if (stagedProfile == null ||
                stagedProfile.schemaVersion != CurrentSchemaVersion)
            {
                throw new InvalidDataException(
                    "The staged player profile failed validation.");
            }

            if (File.Exists(filePath))
            {
                // Keep a known-good copy before attempting platform-specific
                // replacement. It also permits recovery after interruption.
                TryDeleteFile(backupTemporaryPath);
                File.Copy(filePath, backupTemporaryPath, false);
                if (!IsValidProfileFile(backupTemporaryPath))
                    throw new InvalidDataException(
                        "The current player profile could not be backed up safely.");

                TryDeleteFile(backupPath);
                File.Move(backupTemporaryPath, backupPath);

                try
                {
                    File.Replace(temporaryPath, filePath, null);
                }
                catch (Exception replaceException)
                {
                    // Some target filesystems do not implement Replace.
                    // If it completed before reporting failure, recognize
                    // the committed candidate; otherwise use the backup.
                    if (!IsFileContent(filePath, json))
                    {
                        try
                        {
                            if (File.Exists(filePath))
                                File.Delete(filePath);

                            File.Move(temporaryPath, filePath);
                        }
                        catch (Exception fallbackException)
                        {
                            string restoreError = null;
                            if (!File.Exists(filePath) && File.Exists(backupPath))
                            {
                                try
                                {
                                    File.Copy(backupPath, filePath, false);
                                }
                                catch (Exception restoreException)
                                {
                                    restoreError = " The previous save remains at '" +
                                        backupPath + "' but could not be restored: " +
                                        restoreException.Message;
                                }
                            }

                            throw new IOException(
                                "File.Replace failed (" + replaceException.Message +
                                "); fallback replacement failed (" +
                                fallbackException.Message + ")." + restoreError,
                                fallbackException);
                        }
                    }
                }
            }
            else
            {
                File.Move(temporaryPath, filePath);
            }

            TryDeleteFile(temporaryPath);
            TryDeleteFile(backupTemporaryPath);
            TryDeleteFile(backupPath);

            return true;
        }
        catch (Exception exception)
        {
            error = $"Failed to save player profile: {exception.Message}";

            TryDeleteFile(temporaryPath);
            TryDeleteFile(backupTemporaryPath);

            return false;
        }
    }

    private bool TryRecoverBackup(out string error)
    {
        error = null;
        string backupPath = filePath + ".bak";
        string temporaryPath = filePath + ".tmp";
        if (File.Exists(filePath))
            return true;

        if (File.Exists(backupPath))
        {
            if (!IsValidProfileFile(backupPath, CurrentSchemaVersion))
            {
                error = "The player profile is missing and its backup is unreadable or uses an unsupported schema.";
                return false;
            }

            try
            {
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                File.Move(backupPath, filePath);
                return true;
            }
            catch (Exception moveException)
            {
                try
                {
                    if (!File.Exists(filePath))
                        File.Copy(backupPath, filePath, false);

                    return File.Exists(filePath);
                }
                catch (Exception copyException)
                {
                    error = "Could not recover the previous player profile save. " +
                        $"Move failed: {moveException.Message}; copy failed: {copyException.Message}.";
                    return false;
                }
            }
        }

        // A crash may have happened after the initial profile was fully
        // staged but before it was moved into place. Promote only a readable,
        // supported candidate when no committed save or backup exists.
        if (File.Exists(temporaryPath) && IsValidProfileFile(temporaryPath))
        {
            try
            {
                PlayerProfileData staged = JsonUtility.FromJson<PlayerProfileData>(
                    File.ReadAllText(temporaryPath));
                if (staged != null && staged.schemaVersion > CurrentSchemaVersion)
                {
                    error = $"Staged player profile schema {staged.schemaVersion} is newer than supported schema {CurrentSchemaVersion}.";
                    return false;
                }

                if (staged != null)
                {
                    File.Move(temporaryPath, filePath);
                    return true;
                }
            }
            catch (Exception exception)
            {
                error = $"Could not recover the staged player profile: {exception.Message}";
                return false;
            }
        }

        return true;
    }

    private static bool IsValidProfileFile(string path, int maximumSchemaVersion = int.MaxValue)
    {
        if (!File.Exists(path))
            return false;

        try
        {
            PlayerProfileData profile =
                JsonUtility.FromJson<PlayerProfileData>(File.ReadAllText(path));
            return profile != null && profile.schemaVersion <= maximumSchemaVersion;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsFileContent(string path, string expected)
    {
        try
        {
            return File.Exists(path) && File.ReadAllText(path) == expected;
        }
        catch
        {
            return false;
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // A stale backup or temporary file is safe to clean up later.
        }
    }

    private static void Normalize(PlayerProfileData profile)
    {
        if (profile == null)
            return;

        profile.schemaVersion = CurrentSchemaVersion;
        profile.currency = Mathf.Max(0, profile.currency);

        if (profile.selectedVehicleId == null)
            profile.selectedVehicleId = string.Empty;

        if (profile.playerName == null)
            profile.playerName = string.Empty;

        if (profile.selectedCharacterId == null)
            profile.selectedCharacterId = string.Empty;

        profile.EnsureCollections();

        // Remove invalid or duplicate IDs from ownership collections.
        RemoveInvalidIds(profile.inventory.unlockedWeaponIds);
        RemoveInvalidIds(profile.inventory.ownedUpgradeIds);

        for (int i = profile.inventory.ownedVehicles.Count - 1; i >= 0; i--)
        {
            OwnedVehicleData vehicle = profile.inventory.ownedVehicles[i];

            if (vehicle == null || string.IsNullOrWhiteSpace(vehicle.vehicleId))
            {
                profile.inventory.ownedVehicles.RemoveAt(i);
                continue;
            }

            vehicle.EnsureCollections();

            vehicle.currentFuel = IsFinite(vehicle.currentFuel)
                ? Mathf.Max(0f, vehicle.currentFuel)
                : 0f;

            vehicle.currentHealth = IsFinite(vehicle.currentHealth)
                ? Mathf.Max(0f, vehicle.currentHealth)
                : 0f;
        }
    }

    private static void RemoveInvalidIds(List<string> ids)
    {
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

        for (int i = ids.Count - 1; i >= 0; i--)
        {
            string id = ids[i];

            if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
                ids.RemoveAt(i);
        }
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static bool HasInventoryContent(PlayerProfileData profile)
    {
        if (profile.inventory == null)
            return false;

        return
            (profile.inventory.ownedVehicles != null &&
             profile.inventory.ownedVehicles.Count > 0) ||
            (profile.inventory.unlockedWeaponIds != null &&
             profile.inventory.unlockedWeaponIds.Count > 0) ||
            (profile.inventory.ownedUpgradeIds != null &&
             profile.inventory.ownedUpgradeIds.Count > 0);
    }

    private static bool HasLegacyContent(LegacyPlayerProfileData legacy)
    {
        return
            (legacy.ownedItems != null && legacy.ownedItems.Count > 0) ||
            (legacy.vehicleStates != null && legacy.vehicleStates.Count > 0);
    }

    private static PlayerProfileData MigrateLegacyProfile(
        LegacyPlayerProfileData legacy)
    {
        PlayerProfileData migrated = new PlayerProfileData
        {
            schemaVersion = CurrentSchemaVersion,
            currency = Mathf.Max(0, legacy.currency),
            playerName = legacy.playerName ?? string.Empty,
            selectedCharacterId = legacy.selectedCharacterId ?? string.Empty,
            selectedVehicleId = legacy.selectedVehicleId ?? string.Empty
        };

        migrated.EnsureCollections();

        Dictionary<string, LegacyVehiclePersistentState> oldStates =
            new Dictionary<string, LegacyVehiclePersistentState>(
                StringComparer.Ordinal);

        if (legacy.vehicleStates != null)
        {
            foreach (LegacyVehiclePersistentState state in legacy.vehicleStates)
            {
                if (state == null ||
                    string.IsNullOrWhiteSpace(state.vehicleDefinitionId))
                    continue;

                oldStates[state.vehicleDefinitionId] = state;
            }
        }

        if (legacy.ownedItems != null)
        {
            foreach (LegacyOwnedItemRecord item in legacy.ownedItems)
            {
                if (item == null ||
                    item.quantity <= 0 ||
                    string.IsNullOrWhiteSpace(item.definitionId))
                    continue;

                switch (item.category)
                {
                    case ItemCategory.Vehicle:
                        {
                            if (FindMigratedVehicle(
                                migrated, item.definitionId) != null)
                                break;

                            oldStates.TryGetValue(
                                item.definitionId,
                                out LegacyVehiclePersistentState state);

                            OwnedVehicleData vehicle = new OwnedVehicleData(
                                item.definitionId,
                                state != null ? Mathf.Max(0f, state.fuel) : 0f,
                                state != null ? Mathf.Max(0f, state.health) : 0f);

                            if (state != null)
                            {
                                if (state.equippedWeapons != null)
                                {
                                    foreach (LegacyInstalledItemRecord equipped
                                        in state.equippedWeapons)
                                    {
                                        if (equipped == null ||
                                            string.IsNullOrWhiteSpace(equipped.slotId) ||
                                            string.IsNullOrWhiteSpace(equipped.definitionId))
                                            continue;

                                        vehicle.equippedWeapons.Add(
                                            new EquippedWeaponData
                                            {
                                                slotId = equipped.slotId,
                                                weaponId = equipped.definitionId
                                            });
                                    }
                                }

                                if (state.installedUpgrades != null)
                                {
                                    foreach (LegacyInstalledItemRecord installed
                                        in state.installedUpgrades)
                                    {
                                        if (installed == null ||
                                            string.IsNullOrWhiteSpace(installed.slotId) ||
                                            string.IsNullOrWhiteSpace(installed.definitionId))
                                            continue;

                                        vehicle.installedUpgrades.Add(
                                            new InstalledUpgradeData
                                            {
                                                slotId = installed.slotId,
                                                upgradeId = installed.definitionId,
                                                level = 1
                                            });
                                    }
                                }
                            }

                            migrated.inventory.ownedVehicles.Add(vehicle);
                            break;
                        }

                    case ItemCategory.Weapon:
                        AddUnique(
                            migrated.inventory.unlockedWeaponIds,
                            item.definitionId);
                        break;

                    case ItemCategory.Upgrade:
                        AddUnique(
                            migrated.inventory.ownedUpgradeIds,
                            item.definitionId);
                        break;
                }
            }
        }

        return migrated;
    }

    private static void MergeLegacyData(
        PlayerProfileData current,
        LegacyPlayerProfileData legacy)
    {
        PlayerProfileData migrated = MigrateLegacyProfile(legacy);
        current.EnsureCollections();

        if (string.IsNullOrWhiteSpace(current.playerName))
            current.playerName = migrated.playerName;

        if (string.IsNullOrWhiteSpace(current.selectedCharacterId))
            current.selectedCharacterId = migrated.selectedCharacterId;

        if (string.IsNullOrWhiteSpace(current.selectedVehicleId))
            current.selectedVehicleId = migrated.selectedVehicleId;

        foreach (OwnedVehicleData imported in migrated.inventory.ownedVehicles)
        {
            OwnedVehicleData existing = FindMigratedVehicle(
                current, imported.vehicleId);

            if (existing == null)
            {
                current.inventory.ownedVehicles.Add(imported);
                continue;
            }

            MergeMissingVehicleModules(existing, imported);
        }

        foreach (string weaponId in migrated.inventory.unlockedWeaponIds)
            AddUnique(current.inventory.unlockedWeaponIds, weaponId);

        foreach (string upgradeId in migrated.inventory.ownedUpgradeIds)
            AddUnique(current.inventory.ownedUpgradeIds, upgradeId);
    }

    private static void MergeMissingVehicleModules(
        OwnedVehicleData current,
        OwnedVehicleData imported)
    {
        current.EnsureCollections();
        imported.EnsureCollections();

        foreach (EquippedWeaponData importedWeapon in imported.equippedWeapons)
        {
            if (importedWeapon == null ||
                string.IsNullOrWhiteSpace(importedWeapon.slotId))
                continue;

            bool slotExists = false;
            foreach (EquippedWeaponData currentWeapon in current.equippedWeapons)
            {
                if (currentWeapon != null &&
                    currentWeapon.slotId == importedWeapon.slotId)
                {
                    slotExists = true;
                    break;
                }
            }

            if (!slotExists)
            {
                current.equippedWeapons.Add(new EquippedWeaponData
                {
                    slotId = importedWeapon.slotId,
                    weaponId = importedWeapon.weaponId
                });
            }
        }

        foreach (InstalledUpgradeData importedUpgrade in imported.installedUpgrades)
        {
            if (importedUpgrade == null ||
                string.IsNullOrWhiteSpace(importedUpgrade.slotId))
                continue;

            bool slotExists = false;
            foreach (InstalledUpgradeData currentUpgrade in current.installedUpgrades)
            {
                if (currentUpgrade != null &&
                    currentUpgrade.slotId == importedUpgrade.slotId)
                {
                    slotExists = true;
                    break;
                }
            }

            if (!slotExists)
            {
                current.installedUpgrades.Add(new InstalledUpgradeData
                {
                    slotId = importedUpgrade.slotId,
                    upgradeId = importedUpgrade.upgradeId,
                    level = importedUpgrade.level
                });
            }
        }
    }

    private static OwnedVehicleData FindMigratedVehicle(
        PlayerProfileData profile,
        string vehicleId)
    {
        foreach (OwnedVehicleData vehicle in profile.inventory.ownedVehicles)
        {
            if (vehicle != null && vehicle.vehicleId == vehicleId)
                return vehicle;
        }

        return null;
    }

    private static void AddUnique(List<string> list, string id)
    {
        if (!list.Contains(id))
            list.Add(id);
    }

    // DTOs for reading the previous profile format only.
    // They are not part of the active player data model.
    [Serializable]
    private sealed class LegacyPlayerProfileData
    {
        public int schemaVersion = 1;
        public int currency = 1000;
        public string playerName = string.Empty;
        public string selectedCharacterId = string.Empty;
        public string selectedVehicleId = string.Empty;
        public List<LegacyOwnedItemRecord> ownedItems =
            new List<LegacyOwnedItemRecord>();
        public List<LegacyVehiclePersistentState> vehicleStates =
            new List<LegacyVehiclePersistentState>();
    }

    [Serializable]
    private sealed class LegacyOwnedItemRecord
    {
        public string definitionId;
        public ItemCategory category;
        public int quantity = 1;
        public string firstPurchasedUtc;
    }

    [Serializable]
    private sealed class LegacyVehiclePersistentState
    {
        public string vehicleDefinitionId;
        public float health;
        public float fuel;
        public List<LegacyInstalledItemRecord> equippedWeapons =
            new List<LegacyInstalledItemRecord>();
        public List<LegacyInstalledItemRecord> installedUpgrades =
            new List<LegacyInstalledItemRecord>();
    }

    [Serializable]
    private sealed class LegacyInstalledItemRecord
    {
        public string slotId;
        public string definitionId;
    }
}
