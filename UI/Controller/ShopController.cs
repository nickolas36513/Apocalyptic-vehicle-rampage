using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class ShopScreenController : UIScreenController
{
    private IReadOnlyList<ItemDefinition> catalog = Array.Empty<ItemDefinition>();
    private PlayerProfileRuntimeService profiles;
    private ItemCategory category = ItemCategory.Vehicle;
    private ItemDefinition selected;
    private Button selectedRow;

    private VisualElement layoutRoot;
    private ScrollView itemList;
    private Button backButton;
    private Button vehiclesTab;
    private Button weaponsTab;
    private Button upgradesTab;
    private Button purchaseButton;
    private Label currencyValue;
    private Label categoryTitle;
    private Label detailCategory;
    private Label detailName;
    private Label detailDescription;
    private Label detailPrice;
    private Label purchaseMessage;

    protected override void OnBindUI()
    {
        layoutRoot = Required<VisualElement>("shop-screen");
        itemList = Required<ScrollView>("item-list");
        backButton = Required<Button>("back-button");
        vehiclesTab = Required<Button>("vehicles-tab");
        weaponsTab = Required<Button>("weapons-tab");
        upgradesTab = Required<Button>("upgrades-tab");
        purchaseButton = Required<Button>("purchase-button");
        currencyValue = Required<Label>("currency-value");
        categoryTitle = Required<Label>("category-title");
        detailCategory = Required<Label>("detail-category");
        detailName = Required<Label>("detail-name");
        detailDescription = Required<Label>("detail-description");
        detailPrice = Required<Label>("detail-price");
        purchaseMessage = Required<Label>("purchase-message");

        backButton.clicked += HandleBack;
        vehiclesTab.clicked += ShowVehicles;
        weaponsTab.clicked += ShowWeapons;
        upgradesTab.clicked += ShowUpgrades;
        purchaseButton.clicked += HandlePurchase;
        layoutRoot.RegisterCallback<GeometryChangedEvent>(OnLayoutChanged);
    }

    protected override void OnShown()
    {
        profiles = Bootloader.Instance?.PlayerProfiles;
        bool ready = profiles != null && profiles.IsInitialized;
        catalog = ready ? profiles.Definitions.GetAll<ItemDefinition>() : Array.Empty<ItemDefinition>();
        RebuildCatalog(selected?.DefinitionId);
        if (!ready)
            purchaseMessage.text = "Shop services are unavailable.";
    }

    protected override void OnUnbindUI()
    {
        if (backButton != null) backButton.clicked -= HandleBack;
        if (vehiclesTab != null) vehiclesTab.clicked -= ShowVehicles;
        if (weaponsTab != null) weaponsTab.clicked -= ShowWeapons;
        if (upgradesTab != null) upgradesTab.clicked -= ShowUpgrades;
        if (purchaseButton != null) purchaseButton.clicked -= HandlePurchase;
        layoutRoot?.UnregisterCallback<GeometryChangedEvent>(OnLayoutChanged);
        itemList?.Clear();
        catalog = Array.Empty<ItemDefinition>();
        profiles = null;
        selected = null;
        selectedRow = null;
    }

    private void OnLayoutChanged(GeometryChangedEvent evt)
    {
        Rect bounds = evt.newRect;
        layoutRoot.EnableInClassList("compact", bounds.height < 760f);
        layoutRoot.EnableInClassList("narrow", bounds.width < bounds.height * 1.3f);
    }

    private void ShowVehicles() => SelectCategory(ItemCategory.Vehicle);
    private void ShowWeapons() => SelectCategory(ItemCategory.Weapon);
    private void ShowUpgrades() => SelectCategory(ItemCategory.Upgrade);

    private void SelectCategory(ItemCategory nextCategory)
    {
        if (category == nextCategory)
            return;

        category = nextCategory;
        RebuildCatalog(null);
        itemList.scrollOffset = Vector2.zero;
    }

    private void RebuildCatalog(string preferredId)
    {
        itemList.Clear();
        selected = null;
        selectedRow = null;
        purchaseMessage.text = string.Empty;
        vehiclesTab.EnableInClassList("tab-active", category == ItemCategory.Vehicle);
        weaponsTab.EnableInClassList("tab-active", category == ItemCategory.Weapon);
        upgradesTab.EnableInClassList("tab-active", category == ItemCategory.Upgrade);
        categoryTitle.text = category switch
        {
            ItemCategory.Vehicle => "VEHICLES",
            ItemCategory.Weapon => "WEAPONS",
            _ => "UPGRADES"
        };

        foreach (ItemDefinition item in catalog)
        {
            if (item == null || item.Category != category)
                continue;

            Button row = new Button();
            row.AddToClassList("shop-item");
            Label nameLabel = new Label(item.DisplayName);
            nameLabel.AddToClassList("shop-item-name");
            nameLabel.pickingMode = PickingMode.Ignore;
            Label priceLabel = new Label(DescribePrice(item));
            priceLabel.AddToClassList("shop-item-price");
            priceLabel.pickingMode = PickingMode.Ignore;
            row.Add(nameLabel);
            row.Add(priceLabel);
            row.clicked += () => SelectItem(item, row);
            itemList.Add(row);

            if (selected == null || item.DefinitionId == preferredId)
            {
                selected = item;
                selectedRow = row;
            }
        }

        if (selectedRow != null)
            selectedRow.AddToClassList("shop-item-selected");
        RefreshDetails();
    }

    private void SelectItem(ItemDefinition item, Button row)
    {
        selectedRow?.RemoveFromClassList("shop-item-selected");
        selected = item;
        selectedRow = row;
        selectedRow.AddToClassList("shop-item-selected");
        purchaseMessage.text = string.Empty;
        RefreshDetails();
    }

    private string DescribePrice(ItemDefinition item)
    {
        if (profiles.Purchases.Owns(item.DefinitionId))
            return "OWNED";

        return item.PurchasePrice >= 0 ? $"{item.PurchasePrice:N0} CR" : "NOT FOR SALE";
    }

    private void RefreshDetails()
    {
        bool ready = profiles != null && profiles.IsInitialized;
        currencyValue.text = ready ? profiles.Purchases.Currency.ToString("N0") : "—";
        detailCategory.text = selected == null ? string.Empty : selected.Category.ToString().ToUpperInvariant();
        detailName.text = selected == null ? "No items available" : selected.DisplayName;
        detailDescription.text = selected == null ? "This category has no listed items yet." : selected.Description;
        detailPrice.text = selected == null ? "—" : DescribePrice(selected);

        bool owned = ready && selected != null && profiles.Purchases.Owns(selected.DefinitionId);
        bool forSale = ready && selected != null && selected.PurchasePrice >= 0 && !owned;
        purchaseButton.text = owned ? "OWNED" : forSale ? "BUY" : "UNAVAILABLE";
        purchaseButton.SetEnabled(forSale);
    }

    private void HandlePurchase()
    {
        if (profiles == null || !profiles.IsInitialized || selected == null)
            return;

        string itemId = selected.DefinitionId;
        ItemCategory purchasedCategory = selected.Category;
        PurchaseResult result = profiles.Purchases.TryPurchase(itemId);
        if (result.Success)
        {
            RebuildCatalog(itemId);
            purchaseMessage.text = purchasedCategory == ItemCategory.Vehicle || purchasedCategory == ItemCategory.Weapon
                ? "Purchased. Find it in Garage."
                : result.Message;
        }
        else
        {
            RefreshDetails();
            purchaseMessage.text = result.Message;
        }
    }

    private void HandleBack()
    {
        UIScreenRegistry ui = Bootloader.Instance?.UI;
        if (ui != null && !ui.Show("MainMenu", out string error))
            Debug.LogError($"[Shop] {error}", this);
    }

    private T Required<T>(string name) where T : VisualElement
    {
        if (TryGetElement(name, out T element))
            return element;

        throw new InvalidOperationException($"Shop screen is missing '{name}'.");
    }
}
