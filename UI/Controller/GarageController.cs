using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Garage UI presentation and actions through the existing garage service.</summary>
public sealed class GarageScreenController : UIScreenController
{
    [SerializeField] private string mainMenuScreenID = "MainMenu";

    private Button backButton;
    private Button shopButton;
    private Button previousButton;
    private Button nextButton;
    private Button selectButton;
    private Button vehicleTab;
    private Button loadoutTab;
    private Label currencyValue;
    private Label vehicleName;
    private Label vehicleIndex;
    private Label vehicleDescription;
    private Label mountSummary;
    private Label speedValue;
    private Label healthValue;
    private Label fuelValue;
    private Label accelerationValue;
    private Label handlingValue;
    private Label selectionStatus;
    private VisualElement weaponLoadout;
    private VisualElement vehicleDetails;
    private VisualElement preview;
    private VisualElement sidebar;
    private ScrollView detailScroll;
    private bool viewingLoadout;
    private string editingSlotId;

    private VehicleGarageService garage;
    private GameEventBus events;
    private IReadOnlyList<VehicleGarageEntry> catalog;
    private int selectedIndex = -1;
    private bool opened;
    private VisualElement pointerRoot;
    private VisualElement layoutRoot;

    protected override void OnBindUI()
    {
        layoutRoot = Required<VisualElement>("garage-screen");
        layoutRoot.RegisterCallback<GeometryChangedEvent>(OnLayoutChanged);
        backButton = Required<Button>("back-button");
        shopButton = Required<Button>("shop-button");
        previousButton = Required<Button>("previous-button");
        nextButton = Required<Button>("next-button");
        selectButton = Required<Button>("select-button");
        vehicleTab = Required<Button>("vehicle-tab");
        loadoutTab = Required<Button>("loadout-tab");
        preview = Required<VisualElement>("garage-preview");
        sidebar = Required<VisualElement>("garage-sidebar");
        detailScroll = Required<ScrollView>("garage-detail-scroll");
        vehicleDetails = Required<VisualElement>("vehicle-details");
        weaponLoadout = Required<VisualElement>("weapon-loadout");
        TryGetElement("currency-value", out currencyValue);
        TryGetElement("vehicle-name", out vehicleName);
        TryGetElement("vehicle-index", out vehicleIndex);
        TryGetElement("vehicle-description", out vehicleDescription);
        TryGetElement("mount-summary", out mountSummary);
        TryGetElement("speed-value", out speedValue);
        TryGetElement("health-value", out healthValue);
        TryGetElement("fuel-value", out fuelValue);
        TryGetElement("acceleration-value", out accelerationValue);
        TryGetElement("handling-value", out handlingValue);
        TryGetElement("selection-status", out selectionStatus);

        backButton.clicked += HandleBack;
        shopButton.clicked += HandleShop;
        previousButton.clicked += HandlePrevious;
        nextButton.clicked += HandleNext;
        selectButton.clicked += HandleSelect;
        vehicleTab.clicked += ShowVehicleDetails;
        loadoutTab.clicked += ShowLoadout;
        preview.RegisterCallback<GeometryChangedEvent>(OnPreviewLayoutChanged);
        pointerRoot = Host != null ? Host.Root : null;
        if (pointerRoot != null)
        {
            pointerRoot.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            pointerRoot.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            pointerRoot.RegisterCallback<PointerCancelEvent>(OnPointerUp, TrickleDown.TrickleDown);
        }
    }

    protected override void OnUnbindUI()
    {
        layoutRoot?.UnregisterCallback<GeometryChangedEvent>(OnLayoutChanged);
        layoutRoot = null;
        if (backButton != null) backButton.clicked -= HandleBack;
        if (shopButton != null) shopButton.clicked -= HandleShop;
        if (previousButton != null) previousButton.clicked -= HandlePrevious;
        if (nextButton != null) nextButton.clicked -= HandleNext;
        if (selectButton != null) selectButton.clicked -= HandleSelect;
        if (vehicleTab != null) vehicleTab.clicked -= ShowVehicleDetails;
        if (loadoutTab != null) loadoutTab.clicked -= ShowLoadout;
        preview?.UnregisterCallback<GeometryChangedEvent>(OnPreviewLayoutChanged);
        if (pointerRoot != null)
        {
            pointerRoot.UnregisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            pointerRoot.UnregisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            pointerRoot.UnregisterCallback<PointerCancelEvent>(OnPointerUp, TrickleDown.TrickleDown);
        }
        CameraController.ClearGarageUIInputBlockers();
        pointerRoot = null;
        if (opened) CloseGarage();
        ClearElements();
    }

    protected override void OnShown()
    {
        viewingLoadout = false;
        editingSlotId = null;
        UpdateSidebar();
        if (!TryCreateGarageService())
        {
            garage = null;
            catalog = null;
            DisplaySelectedVehicle();
            SetStatus("Garage services unavailable.");
            return;
        }

        catalog = garage.GetOwnedVehicles();
        selectedIndex = FindVehicle(garage.SelectedVehicleId);
        if (selectedIndex < 0 && catalog.Count > 0)
            selectedIndex = 0;

        garage.NotifyOpened();
        events?.Subscribe<VehiclePurchasedEvent>(OnVehiclePurchased);
        events?.Subscribe<VehicleSelectedEvent>(OnVehicleSelected);
        opened = true;
        ShowBrowsedPrefab();
        DisplaySelectedVehicle();
        UpdatePreviewFraming();
    }

    protected override void OnHidden() => CloseGarage();

    private void OnLayoutChanged(GeometryChangedEvent evt)
    {
        Rect bounds = evt.newRect;
        layoutRoot.EnableInClassList("compact", bounds.height < 760f);
        layoutRoot.EnableInClassList("narrow", bounds.width < bounds.height * 1.3f);
        UpdatePreviewFraming();
    }

    private void OnPreviewLayoutChanged(GeometryChangedEvent evt) => UpdatePreviewFraming();

    private void UpdatePreviewFraming()
    {
        if (!opened || preview == null || layoutRoot == null)
            return;

        Rect bounds = layoutRoot.worldBound;
        if (bounds.width > 0 && preview.worldBound.width > 0)
            PlayerManager.Instance?.CameraController?.SetGaragePreviewCenter(
                (preview.worldBound.center.x - bounds.xMin) / bounds.width);
    }

    private bool TryCreateGarageService()
    {
        Bootloader bootloader = Bootloader.Instance;
        if (bootloader == null || bootloader.PlayerProfiles == null ||
            !bootloader.PlayerProfiles.IsInitialized)
            return false;

        garage = new VehicleGarageService(bootloader.PlayerProfiles, bootloader.Events);
        events = bootloader.Events;
        return true;
    }

    private void CloseGarage()
    {
        CameraController.ClearGarageUIInputBlockers();
        PlayerManager.Instance?.CameraController?.SetGaragePreviewCenter(0.5f);
        if (events != null)
        {
            events.Unsubscribe<VehiclePurchasedEvent>(OnVehiclePurchased);
            events.Unsubscribe<VehicleSelectedEvent>(OnVehicleSelected);
        }
        if (opened && garage != null)
            garage.NotifyClosed();
        opened = false;
    }

    private void HandlePrevious() => MoveSelection(-1);
    private void HandleNext() => MoveSelection(1);

    private void OnPointerDown(PointerDownEvent evt)
    {
        for (VisualElement element = evt.target as VisualElement;
             element != null;
             element = element.parent)
        {
            if (element is Button || element == sidebar)
            {
                CameraController.SetGarageUIPointerBlocked(evt.pointerId, true);
                return;
            }
        }
    }

    private void OnPointerUp(EventBase evt)
    {
        if (evt is IPointerEvent pointerEvent)
            CameraController.SetGarageUIPointerBlocked(pointerEvent.pointerId, false);
    }

    private void MoveSelection(int direction)
    {
        if (catalog == null || catalog.Count == 0)
            return;
        selectedIndex = (selectedIndex + direction + catalog.Count) % catalog.Count;
        editingSlotId = null;
        detailScroll.scrollOffset = Vector2.zero;
        ShowBrowsedPrefab();
        DisplaySelectedVehicle();
    }

    private void ShowBrowsedPrefab()
    {
        if (TryGetSelected(out VehicleGarageEntry entry))
            garage.NotifyPreviewChanged(entry.VehicleId);
    }

    private void HandleSelect()
    {
        if (!TryGetSelected(out VehicleGarageEntry entry))
            return;

        bool success = garage.TrySelect(entry.VehicleId, out string message);
        catalog = garage.GetOwnedVehicles();
        selectedIndex = FindVehicle(entry.VehicleId);
        DisplaySelectedVehicle();
        if (!success)
            SetStatus(message);
    }

    private void OnVehiclePurchased(VehiclePurchasedEvent message)
    {
        if (garage == null)
            return;
        string browsedId = TryGetSelected(out VehicleGarageEntry entry) ? entry.VehicleId : garage.SelectedVehicleId;
        catalog = garage.GetOwnedVehicles();
        selectedIndex = FindVehicle(browsedId);
        if (selectedIndex < 0 && catalog.Count > 0)
            selectedIndex = 0;
        DisplaySelectedVehicle();
    }

    private void OnVehicleSelected(VehicleSelectedEvent message)
    {
        if (garage == null)
            return;
        catalog = garage.GetOwnedVehicles();
        int index = FindVehicle(message.VehicleId);
        if (index >= 0 && index != selectedIndex)
        {
            selectedIndex = index;
            editingSlotId = null;
        }
        DisplaySelectedVehicle();
    }

    private void DisplaySelectedVehicle()
    {
        bool canBrowse = catalog != null && catalog.Count > 1;
        previousButton.SetEnabled(canBrowse);
        nextButton.SetEnabled(canBrowse);
        RefreshCurrency();

        if (!TryGetSelected(out VehicleGarageEntry entry))
        {
            weaponLoadout.Clear();
            SetStatus("Visit Shop to buy a vehicle.");
            SetLabel(vehicleName, "No owned vehicles");
            SetLabel(vehicleDescription, string.Empty);
            SetLabel(mountSummary, string.Empty);
            SetLabel(vehicleIndex, "0 / 0");
            SetLabel(speedValue, "—");
            SetLabel(healthValue, "—");
            SetLabel(fuelValue, "—");
            SetLabel(accelerationValue, "—");
            SetLabel(handlingValue, "—");
            selectButton.text = "USE VEHICLE";
            selectButton.SetEnabled(false);
            return;
        }

        SetLabel(vehicleName, entry.DisplayName);
        SetLabel(vehicleDescription, entry.Description);
        SetLabel(vehicleIndex, $"{selectedIndex + 1:00} / {catalog.Count:00}");
        VehicleRuntime runtime = PlayerManager.Instance?.ActiveRuntime;
        int mounts = runtime != null && runtime.Definition.DefinitionId == entry.VehicleId
            ? runtime.Definition.WeaponSlots.Count : 0;
        SetLabel(mountSummary, $"{mounts} weapon mount points");
        SetLabel(speedValue, entry.Stats.MaxSpeed.ToString("0.#"));
        SetLabel(healthValue, $"{entry.Stats.CurrentHealth:0.#} / {entry.Stats.MaxHealth:0.#}");
        SetLabel(fuelValue, $"{entry.Stats.CurrentFuel:0.#} / {entry.Stats.MaxFuel:0.#}");
        SetLabel(accelerationValue, entry.Stats.Acceleration.ToString("0.##"));
        SetLabel(handlingValue, entry.Stats.Handling.ToString("0.##"));
        SetStatus(entry.IsSelected ? "SELECTED" : "OWNED");
        selectButton.text = entry.IsSelected ? "SELECTED" : "USE VEHICLE";
        selectButton.SetEnabled(!entry.IsSelected);
        if (viewingLoadout)
            ShowWeaponLoadout(entry.VehicleId);
    }

    private void ShowVehicleDetails() => SwitchSidebar(false);
    private void ShowLoadout() => SwitchSidebar(true);

    private void SwitchSidebar(bool loadout)
    {
        viewingLoadout = loadout;
        editingSlotId = null;
        UpdateSidebar();
        detailScroll.scrollOffset = Vector2.zero;
        DisplaySelectedVehicle();
    }

    private void UpdateSidebar()
    {
        vehicleDetails.style.display = viewingLoadout ? DisplayStyle.None : DisplayStyle.Flex;
        weaponLoadout.style.display = viewingLoadout ? DisplayStyle.Flex : DisplayStyle.None;
        vehicleTab.EnableInClassList("garage-tab-active", !viewingLoadout);
        loadoutTab.EnableInClassList("garage-tab-active", viewingLoadout);
    }

    private void ShowWeaponLoadout(string vehicleId)
    {
        weaponLoadout.Clear();
        PlayerProfileRuntimeService profile = Bootloader.Instance?.PlayerProfiles;
        VehicleRuntime vehicle = PlayerManager.Instance?.ActiveRuntime;
        if (profile == null || vehicle == null || vehicle.Definition.DefinitionId != vehicleId)
        {
            weaponLoadout.Add(new Label("Vehicle preview unavailable."));
            return;
        }

        if (editingSlotId != null && vehicle.Definition.TryGetWeaponSlot(editingSlotId, out _))
        {
            ShowWeaponPicker(profile, vehicle, vehicleId, editingSlotId);
            return;
        }

        foreach (VehicleWeaponSlotDefinition slot in vehicle.Definition.WeaponSlots)
        {
            if (slot == null || string.IsNullOrWhiteSpace(slot.SlotId))
                continue;

            string slotId = slot.SlotId;
            vehicle.Weapons.Equipped.TryGetValue(slotId, out WeaponDefinition equipped);
            VisualElement row = new VisualElement();
            row.AddToClassList("garage-slot-row");
            Label icon = new Label(equipped == null ? "—" : SlotBadge(slotId));
            icon.AddToClassList("garage-slot-icon");
            if (equipped != null && equipped.Icon != null)
            {
                icon.text = string.Empty;
                icon.style.backgroundImage = new StyleBackground(equipped.Icon);
            }
            row.Add(icon);

            VisualElement labels = new VisualElement();
            labels.AddToClassList("garage-slot-labels");
            AddLoadoutLabel(labels, SlotName(slotId), "garage-slot-kind");
            AddLoadoutLabel(labels, equipped != null ? equipped.DisplayName : "Empty", "garage-slot-name");
            row.Add(labels);
            AddWeaponAction(row, equipped != null ? "CHANGE" : "EQUIP", () =>
            {
                editingSlotId = slotId;
                ShowWeaponLoadout(vehicleId);
                detailScroll.scrollOffset = Vector2.zero;
            });
            weaponLoadout.Add(row);
        }

        if (profile.Profile.inventory.unlockedWeaponIds.Count == 0)
            AddLoadoutLabel(weaponLoadout, "Buy weapons in Shop to equip them here.", "garage-description");
    }

    private void ShowWeaponPicker(PlayerProfileRuntimeService profile, VehicleRuntime vehicle,
        string vehicleId, string slotId)
    {
        AddLoadoutLabel(weaponLoadout, SlotName(slotId).ToUpperInvariant(), "garage-section");
        vehicle.Weapons.Equipped.TryGetValue(slotId, out WeaponDefinition equipped);
        AddLoadoutLabel(weaponLoadout,
            equipped != null ? $"Equipped: {equipped.DisplayName}" : "Empty slot", "garage-description");

        bool hasAlternative = false;
        foreach (WeaponDefinition weapon in profile.Definitions.GetAll<WeaponDefinition>())
        {
            if (weapon == equipped || !profile.Inventory.HasWeaponUnlocked(weapon.DefinitionId) ||
                !vehicle.Weapons.CanEquip(slotId, weapon, out _, out _))
                continue;

            hasAlternative = true;
            AddWeaponAction(weaponLoadout, $"EQUIP {weapon.DisplayName}",
                () => ChangeWeapon(vehicleId, slotId, weapon));
        }

        if (!hasAlternative)
            AddLoadoutLabel(weaponLoadout, "No other compatible owned weapons.", "garage-description");
        if (equipped != null)
            AddWeaponAction(weaponLoadout, "REMOVE WEAPON", () => ChangeWeapon(vehicleId, slotId, null));
        AddWeaponAction(weaponLoadout, "BACK TO SLOTS", () =>
        {
            editingSlotId = null;
            ShowWeaponLoadout(vehicleId);
            detailScroll.scrollOffset = Vector2.zero;
        });
    }

    private static string SlotName(string slotId) => slotId switch
    {
        WeaponSlotRules.SecondaryLeft => "Secondary Left",
        WeaponSlotRules.SecondaryRight => "Secondary Right",
        _ => slotId
    };

    private static string SlotBadge(string slotId) => slotId switch
    {
        WeaponSlotRules.Primary => "P",
        WeaponSlotRules.SecondaryLeft => "L",
        WeaponSlotRules.SecondaryRight => "R",
        WeaponSlotRules.Melee => "M",
        _ => "•"
    };

    private static void AddLoadoutLabel(VisualElement parent, string text, string className)
    {
        Label label = new Label(text);
        label.AddToClassList(className);
        parent.Add(label);
    }

    private void AddWeaponAction(VisualElement parent, string text, Action action)
    {
        Button button = new Button(action) { text = text };
        button.AddToClassList("garage-button");
        if (parent == weaponLoadout)
            button.AddToClassList("garage-picker-action");
        parent.Add(button);
    }

    private void ChangeWeapon(string vehicleId, string slotId, WeaponDefinition weapon)
    {
        PlayerProfileRuntimeService profile = Bootloader.Instance?.PlayerProfiles;
        VehicleRuntime vehicle = PlayerManager.Instance?.ActiveRuntime;
        if (profile == null || vehicle == null || vehicle.Definition.DefinitionId != vehicleId)
            return;

        if (weapon != null && !vehicle.Weapons.CanEquip(slotId, weapon, out _, out string mountError))
        {
            SetStatus(mountError);
            return;
        }

        string error;
        bool saved = weapon == null
            ? profile.Inventory.UnequipWeapon(vehicleId, slotId, out error)
            : profile.Inventory.EquipWeapon(vehicleId, slotId, weapon.DefinitionId, out error);
        if (!saved)
        {
            SetStatus(error);
            return;
        }

        if (weapon == null)
            vehicle.Weapons.TryUnequip(slotId);
        else
            vehicle.Weapons.TryEquip(slotId, weapon, out _);

        editingSlotId = null;
        ShowWeaponLoadout(vehicleId);
        detailScroll.scrollOffset = Vector2.zero;
        SetStatus(weapon == null ? "Weapon removed." : "Weapon equipped.");
    }

    private void RefreshCurrency() => SetLabel(currencyValue, garage != null ? garage.Currency.ToString("N0") : "—");

    private bool TryGetSelected(out VehicleGarageEntry entry)
    {
        entry = default;
        return catalog != null && selectedIndex >= 0 && selectedIndex < catalog.Count &&
               (entry = catalog[selectedIndex]).VehicleId != null;
    }

    private int FindVehicle(string vehicleId)
    {
        if (catalog == null || string.IsNullOrWhiteSpace(vehicleId))
            return -1;
        for (int i = 0; i < catalog.Count; i++)
            if (string.Equals(catalog[i].VehicleId, vehicleId, StringComparison.Ordinal))
                return i;
        return -1;
    }

    private void HandleBack() => ShowScreen(mainMenuScreenID);
    private void HandleShop() => ShowScreen("Shop");

    private void ShowScreen(string screenId)
    {
        Bootloader bootloader = Bootloader.Instance;
        if (bootloader == null || bootloader.UI == null)
        {
            Debug.LogError($"Could not open {screenId}: UI services are unavailable.", this);
            return;
        }
        if (!bootloader.UI.Show(screenId, out string error))
            Debug.LogError($"Could not open {screenId}: {error}", this);
    }

    private T Required<T>(string name) where T : VisualElement =>
        TryGetElement(name, out T element)
            ? element
            : throw new InvalidOperationException($"Garage screen is missing '{name}'.");

    private void SetStatus(string message) => SetLabel(selectionStatus, message ?? string.Empty);
    private static void SetLabel(Label label, string value) { if (label != null) label.text = value ?? string.Empty; }

    private void ClearElements()
    {
        weaponLoadout?.Clear();
        weaponLoadout = null;
        preview = sidebar = vehicleDetails = null;
        detailScroll = null;
        vehicleTab = loadoutTab = null;
        vehicleDescription = mountSummary = null;
        backButton = shopButton = previousButton = nextButton = selectButton = null;
        currencyValue = vehicleName = vehicleIndex = speedValue = healthValue = fuelValue = null;
        accelerationValue = handlingValue = selectionStatus = null;
        garage = null;
        events = null;
        catalog = null;
    }
}
