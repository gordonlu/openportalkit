using System.Text.Json;

namespace OpenPortalKit.Modules.Content.BlockTemplates;

public static class PageTemplateValidator
{
    public static PageTemplateValidationResult Validate(
        PageTemplate template,
        IBlockDefinitionCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(catalog);

        var errors = new List<string>();
        if (template.Id == Guid.Empty)
        {
            errors.Add("Template identifier is required.");
        }

        if (string.IsNullOrWhiteSpace(template.Code))
        {
            errors.Add("Template code is required.");
        }

        if (string.IsNullOrWhiteSpace(template.Name))
        {
            errors.Add("Template name is required.");
        }

        if (template.Version <= 0)
        {
            errors.Add("Template version must be positive.");
        }

        if (template.Blocks.Count == 0)
        {
            errors.Add("A template must include at least one predefined block.");
        }

        if (template.Blocks.GroupBy(block => block.Id).Any(group => group.Count() > 1))
        {
            errors.Add("Block instance identifiers must be unique within a template.");
        }

        if (template.Blocks.GroupBy(block => block.SortOrder).Any(group => group.Count() > 1))
        {
            errors.Add("Block sort order values must be unique within a template.");
        }

        foreach (var block in template.Blocks)
        {
            ValidateBlock(block, catalog, errors);
        }

        return errors.Count == 0
            ? PageTemplateValidationResult.Success
            : new PageTemplateValidationResult(errors);
    }

    private static void ValidateBlock(
        BlockInstance block,
        IBlockDefinitionCatalog catalog,
        ICollection<string> errors)
    {
        if (block.Id == Guid.Empty)
        {
            errors.Add("Block instance identifier is required.");
        }

        if (block.SortOrder < 0)
        {
            errors.Add($"Block '{block.DefinitionCode}' has an invalid sort order.");
        }

        if (string.IsNullOrWhiteSpace(block.DefinitionCode))
        {
            errors.Add("Block definition code is required.");
            return;
        }

        var definition = catalog.FindByCode(block.DefinitionCode);
        if (definition is null)
        {
            errors.Add($"Block '{block.DefinitionCode}' is not a predefined block.");
            return;
        }

        if (!string.Equals(block.SchemaVersion, definition.SchemaVersion, StringComparison.Ordinal))
        {
            errors.Add($"Block '{block.DefinitionCode}' does not match schema '{definition.SchemaVersion}'.");
        }

        try
        {
            using var document = JsonDocument.Parse(block.ConfigurationJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"Block '{block.DefinitionCode}' configuration must be a JSON object.");
                return;
            }

            var settings = definition.Settings.ToDictionary(setting => setting.Key, StringComparer.OrdinalIgnoreCase);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!settings.TryGetValue(property.Name, out var setting))
                {
                    errors.Add($"Block '{block.DefinitionCode}' does not allow configuration field '{property.Name}'.");
                    continue;
                }

                if (!HasExpectedType(property.Value, setting.Type))
                {
                    errors.Add($"Block '{block.DefinitionCode}' field '{property.Name}' must be {Describe(setting.Type)}.");
                }

                if (string.Equals(property.Name, "take", StringComparison.OrdinalIgnoreCase) &&
                    (!property.Value.TryGetInt32(out var take) || take is < 1 or > 50))
                {
                    errors.Add($"Block '{block.DefinitionCode}' field 'take' must be between 1 and 50.");
                }
            }

            foreach (var setting in definition.Settings.Where(setting => setting.IsRequired))
            {
                if (!document.RootElement.TryGetProperty(setting.Key, out var value) || IsEmpty(value))
                {
                    errors.Add($"Block '{block.DefinitionCode}' requires '{setting.Key}'.");
                }
            }
        }
        catch (JsonException)
        {
            errors.Add($"Block '{block.DefinitionCode}' configuration must be valid JSON.");
        }
        catch (ArgumentNullException)
        {
            errors.Add($"Block '{block.DefinitionCode}' configuration is required.");
        }
    }

    private static bool IsEmpty(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Null => true,
            JsonValueKind.String => string.IsNullOrWhiteSpace(value.GetString()),
            _ => false
        };
    }

    private static bool HasExpectedType(JsonElement value, BlockSettingType type)
    {
        return type switch
        {
            BlockSettingType.Text or BlockSettingType.RichText or BlockSettingType.ContentQuery or BlockSettingType.DataSetReference =>
                value.ValueKind == JsonValueKind.String,
            BlockSettingType.Url => value.ValueKind == JsonValueKind.String && IsRelativeOrAbsoluteUrl(value.GetString()),
            BlockSettingType.Number => value.ValueKind == JsonValueKind.Number,
            BlockSettingType.Boolean => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            _ => false
        };
    }

    private static bool IsRelativeOrAbsoluteUrl(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            (value.StartsWith("/", StringComparison.Ordinal) ||
             Uri.TryCreate(value, UriKind.Absolute, out _));
    }

    private static string Describe(BlockSettingType type)
    {
        return type switch
        {
            BlockSettingType.Text => "text",
            BlockSettingType.RichText => "rich text",
            BlockSettingType.Url => "a relative or absolute URL",
            BlockSettingType.Number => "a number",
            BlockSettingType.Boolean => "a boolean",
            BlockSettingType.ContentQuery => "a content query string",
            BlockSettingType.DataSetReference => "a dataset reference string",
            _ => "a valid value"
        };
    }
}
