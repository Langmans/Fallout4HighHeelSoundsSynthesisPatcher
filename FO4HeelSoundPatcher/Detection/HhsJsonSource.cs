using System.Globalization;
using System.Text.Json;
using FO4HeelSoundPatcher.Assets;
using FO4HeelSoundPatcher.Logging;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;

namespace FO4HeelSoundPatcher.Detection;

/// <summary>
/// The HHS json method: files in <c>Data\F4SE\Plugins\HHS</c> replacing a pile of .txt files. Every
/// top level key is a group holding an array of entries, and entries come in two shapes (see
/// <c>template.json</c> in the "Resources for modders" download):
/// <code>
/// {
///   "Myshoes1" : [ { "key" : "MyShoes1\\MyShoes.nif", "value" : 10 } ],
///   "MyShoes.esp" : [ { "formid" : "00800", "gender" : 1, "value" : 10 } ]
/// }
/// </code>
/// <para>
/// Both shapes end up meaning the same thing. HHS keys its cache purely on mesh path: a <c>key</c>
/// entry gives the path directly, and a <c>formid</c> entry is resolved to an <b>ArmorAddon</b>
/// (not an Armor) whose world model path is then used. So this source is resolved down to mesh
/// paths up front, exactly like <c>JsonParser::HeightFile</c> does.
/// </para>
/// <para>
/// <c>gender</c> selects which world model: 0 male, 1 female, 2 both. Value 3 means an object
/// modification's material swap model, which is out of scope here and is logged and skipped.
/// </para>
/// </summary>
public sealed class HhsJsonSource : IMeshHeightSource
{
    public HeightSource Kind => HeightSource.HhsJson;

    private const string HhsJsonFolder = "F4SE\\Plugins\\HHS";

    private readonly PatcherLog _log;

    /// <summary>Normalised mesh path -> height.</summary>
    private readonly Dictionary<string, float> _byMesh = new(StringComparer.Ordinal);

    public int EntryCount => _byMesh.Count;

    public string Statistics => $"HHS json entries: {_byMesh.Count}";

    public HhsJsonSource(DataAssetLocator assets, ILinkCache linkCache, PatcherLog log)
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
                Load(text, file, linkCache);
                _log.Info($"Loaded HHS json '{file}' ({origin})");
            }
            catch (JsonException ex)
            {
                _log.Warn($"malformed HHS json '{file}' skipped: {ex.Message}");
            }
        }

        _log.Info($"HHS json: {_byMesh.Count} mesh entries");
    }

    public HeelHeight? TryGetHeight(string meshDataPath)
    {
        var key = DataAssetLocator.Normalize(meshDataPath);
        return _byMesh.TryGetValue(key, out var height)
            ? new HeelHeight(height, Kind, key)
            : null;
    }

    private void Load(string text, string file, ILinkCache linkCache)
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

            foreach (var entry in group.Value.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object) continue;
                if (!TryGetValue(entry, out var height)) continue;

                // HHS treats a present, non-empty "key" as the mesh path and only falls back to
                // "formid" otherwise.
                if (entry.TryGetProperty("key", out var meshKey) &&
                    meshKey.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrEmpty(meshKey.GetString()))
                {
                    AddMeshEntry(meshKey.GetString()!, height);
                    continue;
                }

                if (entry.TryGetProperty("formid", out var formIdElement))
                {
                    AddFormIdEntry(group.Name, formIdElement, entry, height, file, linkCache);
                }
            }
        }
    }

    private void AddMeshEntry(string rawPath, float height)
    {
        if (string.IsNullOrWhiteSpace(rawPath)) return;
        Upsert(DataAssetLocator.ToMeshDataPath(rawPath), height);
    }

    private void AddFormIdEntry(
        string groupName,
        JsonElement formIdElement,
        JsonElement entry,
        float height,
        string file,
        ILinkCache linkCache)
    {
        if (!ModKey.TryFromFileName(groupName, out var modKey))
        {
            _log.Warn(
                $"'{file}': group '{groupName}' has FormID entries but its name is not a plugin " +
                "filename, so they cannot be resolved");
            return;
        }

        if (!TryParseFormId(formIdElement, out var formId))
        {
            _log.Warn($"'{file}': group '{groupName}' has an unparsable formid '{formIdElement}'");
            return;
        }

        var formKey = new FormKey(modKey, formId);
        var gender = entry.TryGetProperty("gender", out var genderElement) && genderElement.ValueKind == JsonValueKind.Number
            ? genderElement.GetInt32()
            : 1;

        if (gender == 3)
        {
            _log.Detail($"'{file}': {formKey} uses gender 3 (object modification model), not supported");
            return;
        }

        if (!linkCache.TryResolve<IArmorAddonGetter>(formKey, out var addon))
        {
            _log.Detail($"'{file}': {formKey} does not resolve to an ArmorAddon, skipped");
            return;
        }

        var added = 0;
        if (gender is 0 or 2) added += AddModel(addon.WorldModel?.Male?.File, height);
        if (gender is 1 or 2) added += AddModel(addon.WorldModel?.Female?.File, height);

        if (added == 0)
        {
            _log.Detail($"'{file}': {formKey} '{addon.EditorID}' has no world model for gender {gender}");
            return;
        }

        _log.Debug($"'{file}': {formKey} '{addon.EditorID}' -> {added} mesh path(s) at {Num.Height(height)}");
    }

    private int AddModel(string? modelPath, float height)
    {
        if (string.IsNullOrWhiteSpace(modelPath)) return 0;
        Upsert(DataAssetLocator.ToMeshDataPath(modelPath), height);
        return 1;
    }

    /// <summary>Keeps the highest height when the same mesh shows up more than once.</summary>
    private void Upsert(string key, float height)
    {
        if (_byMesh.TryGetValue(key, out var existing) && existing >= height) return;
        _byMesh[key] = height;
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

        // HHS parses the id as bare hex and ORs in the load order index itself, so only the object
        // part is meaningful here.
        formId &= 0x00FFFFFF;
        return true;
    }
}
