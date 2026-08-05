using System.Globalization;
using System.Text.Json;
using FO4HeelSoundPatcher.Assets;
using FO4HeelSoundPatcher.Logging;
using Mutagen.Bethesda.Plugins;

namespace FO4HeelSoundPatcher.Detection;

/// <summary>
/// The HHS json method: one or more files in <c>Data\F4SE\Plugins\HHS</c> replacing a pile of .txt
/// files. Every top level key is a group holding an array of entries, and entries come in two
/// shapes (see <c>template.json</c> in the "Resources for modders" download):
/// <code>
/// {
///   "Myshoes1" : [ { "key" : "MyShoes1\\MyShoes.nif", "value" : 10 } ],
///   "MyShoes.esp" : [ { "formid" : "00800", "gender" : 1, "value" : 10 } ]
/// }
/// </code>
/// The first shape keys on a mesh path, the second on the plugin (the group name) plus a FormID.
/// </summary>
public sealed class HhsJsonSource
{
    public const string SourceName = "HHS-json";

    private const string HhsJsonFolder = "F4SE\\Plugins\\HHS";

    private readonly PatcherLog _log;

    /// <summary>Normalised mesh path (with and without the meshes\ prefix) -> height.</summary>
    private readonly Dictionary<string, float> _byMesh = new(StringComparer.Ordinal);

    /// <summary>FormKey of the Armor or ArmorAddon -> height.</summary>
    private readonly Dictionary<FormKey, float> _byFormKey = new();

    public int MeshEntryCount => _byMesh.Count;
    public int FormKeyEntryCount => _byFormKey.Count;

    public HhsJsonSource(DataAssetLocator assets, PatcherLog log)
    {
        _log = log;

        var files = assets.ListFiles(HhsJsonFolder, ".json");
        if (files.Count == 0)
        {
            _log.Info($"No HHS json files found in {HhsJsonFolder}");
            return;
        }

        foreach (var file in files)
        {
            if (!assets.TryReadAllText(file, out var text, out var origin))
            {
                _log.Warn($"could not read HHS json '{file}'");
                continue;
            }

            try
            {
                Load(text, file);
                _log.Info($"Loaded HHS json '{file}' ({origin})");
            }
            catch (JsonException ex)
            {
                _log.Warn($"malformed HHS json '{file}' skipped: {ex.Message}");
            }
        }

        _log.Info($"HHS json: {_byMesh.Count} mesh entries, {_byFormKey.Count} FormID entries");
    }

    public HeelHeight? TryGetByMesh(string meshDataPath)
    {
        var key = DataAssetLocator.Normalize(meshDataPath);
        return _byMesh.TryGetValue(key, out var height)
            ? new HeelHeight(height, SourceName, key)
            : null;
    }

    public HeelHeight? TryGetByFormKey(FormKey formKey)
    {
        return _byFormKey.TryGetValue(formKey, out var height)
            ? new HeelHeight(height, SourceName, formKey.ToString())
            : null;
    }

    private void Load(string text, string file)
    {
        var options = new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
        };

        using var document = JsonDocument.Parse(text, options);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            _log.Warn($"'{file}': expected a json object at the top level, got {document.RootElement.ValueKind}");
            return;
        }

        foreach (var group in document.RootElement.EnumerateObject())
        {
            if (group.Value.ValueKind != JsonValueKind.Array)
            {
                _log.Warn($"'{file}': group '{group.Name}' is not an array, skipped");
                continue;
            }

            var groupModKey = TryParseModKey(group.Name);

            foreach (var entry in group.Value.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object) continue;
                if (!TryGetValue(entry, out var height)) continue;

                if (entry.TryGetProperty("key", out var meshKey) && meshKey.ValueKind == JsonValueKind.String)
                {
                    AddMeshEntry(meshKey.GetString(), height);
                    continue;
                }

                if (entry.TryGetProperty("formid", out var formIdElement))
                {
                    if (groupModKey is null)
                    {
                        _log.Warn(
                            $"'{file}': group '{group.Name}' has FormID entries but its name is not a " +
                            "plugin filename, so they cannot be resolved");
                        continue;
                    }

                    AddFormIdEntry(groupModKey.Value, formIdElement, entry, height, file, group.Name);
                }
            }
        }
    }

    private void AddMeshEntry(string? rawPath, float height)
    {
        if (string.IsNullOrWhiteSpace(rawPath)) return;

        // HHS writes these relative to meshes\, but be lenient and index both forms so a lookup
        // succeeds whichever way the record path turned out.
        var normalized = DataAssetLocator.Normalize(rawPath);
        Upsert(_byMesh, normalized, height);
        Upsert(_byMesh, DataAssetLocator.ToMeshDataPath(rawPath), height);
    }

    private void AddFormIdEntry(
        ModKey modKey,
        JsonElement formIdElement,
        JsonElement entry,
        float height,
        string file,
        string groupName)
    {
        if (!TryParseFormId(formIdElement, out var formId))
        {
            _log.Warn($"'{file}': group '{groupName}' has an unparsable formid '{formIdElement}'");
            return;
        }

        if (entry.TryGetProperty("gender", out var gender) && gender.ValueKind == JsonValueKind.Number)
            _log.Debug($"'{file}': {modKey}|{formId:X6} gender={gender} (gender is not used for filtering)");

        Upsert(_byFormKey, new FormKey(modKey, formId), height);
    }

    /// <summary>Keeps the highest height when the same key shows up more than once.</summary>
    private static void Upsert<TKey>(Dictionary<TKey, float> target, TKey key, float height)
        where TKey : notnull
    {
        if (target.TryGetValue(key, out var existing) && existing >= height) return;
        target[key] = height;
    }

    private static bool TryGetValue(JsonElement entry, out float height)
    {
        height = 0;
        if (!entry.TryGetProperty("value", out var value)) return false;

        switch (value.ValueKind)
        {
            case JsonValueKind.Number:
                height = (float)value.GetDouble();
                return true;

            case JsonValueKind.String:
                return float.TryParse(
                    value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out height);

            default:
                return false;
        }
    }

    private static bool TryParseFormId(JsonElement element, out uint formId)
    {
        formId = 0;

        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var text = element.GetString()?.Trim();
                if (string.IsNullOrEmpty(text)) return false;
                if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..];
                if (!uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out formId))
                    return false;
                break;

            case JsonValueKind.Number:
                if (!element.TryGetUInt32(out formId)) return false;
                break;

            default:
                return false;
        }

        // Only the object part is meaningful; the load order index is whatever it is at runtime.
        formId &= 0x00FFFFFF;
        return true;
    }

    private static ModKey? TryParseModKey(string groupName)
    {
        return ModKey.TryFromFileName(groupName, out var modKey) ? modKey : (ModKey?)null;
    }
}
