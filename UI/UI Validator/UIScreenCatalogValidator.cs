
using System.Collections.Generic;
using System.Text;

public static class UIScreenCatalogValidator
{
    public static bool Validate(
        UIScreenCatalog catalog,
        out string report)
    {
        return Validate(catalog, validateScreenAssets: true, out report);
    }

    internal static bool ValidateStructure(UIScreenCatalog catalog, out string report)
    {
        return Validate(catalog, validateScreenAssets: false, out report);
    }

    private static bool Validate(
        UIScreenCatalog catalog,
        bool validateScreenAssets,
        out string report)
    {
        StringBuilder errors = new StringBuilder();

        if (catalog == null)
        {
            report = "UIScreenCatalog reference is missing.";
            return false;
        }

        if (catalog.PanelSettings == null)
        {
            errors.AppendLine(
                "- PanelSettings is not assigned.");
        }

        if (catalog.Count == 0)
        {
            errors.AppendLine(
                "- Catalog contains no screen definitions.");
        }

        HashSet<string> registeredIDs =
            new HashSet<string>(System.StringComparer.Ordinal);

        for (int i = 0; i < catalog.Count; i++)
        {
            UIScreenDefinition definition = catalog.GetScreen(i);

            string prefix = $"Screen definition [{i}]";

            if (string.IsNullOrWhiteSpace(definition.ID))
            {
                errors.AppendLine(
                    $"- {prefix}: ID is empty.");
            }
            else if (!registeredIDs.Add(definition.ID))
            {
                errors.AppendLine(
                    $"- {prefix}: Duplicate ID '{definition.ID}'.");
            }

            if (validateScreenAssets && definition.SourceAsset == null)
            {
                errors.AppendLine(
                    $"- {prefix} '{definition.ID}': VisualTreeAsset is missing.");
            }

            if (validateScreenAssets && definition.ControllerPrefab == null)
            {
                errors.AppendLine(
                    $"- {prefix} '{definition.ID}': Controller prefab is missing.");
            }
        }

        report = errors.ToString();

        return errors.Length == 0;
    }
}
