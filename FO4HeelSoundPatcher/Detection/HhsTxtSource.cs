using System.Globalization;
using FO4HeelSoundPatcher.Assets;
using FO4HeelSoundPatcher.Logging;

namespace FO4HeelSoundPatcher.Detection;

/// <summary>
/// The classic HHS method: a .txt file next to the mesh with the same base name, containing a line
/// like <c>Height=13.1</c>.
/// <para>
/// So <c>meshes\some\path\heels.nif</c> is accompanied by <c>meshes\some\path\heels.txt</c>. The
/// file names have to match exactly; HHS itself is case insensitive about the key.
/// </para>
/// </summary>
public sealed class HhsTxtSource
{
    public const string SourceName = "HHS-txt";

    private readonly DataAssetLocator _assets;
    private readonly PatcherLog _log;
    private readonly Dictionary<string, float?> _cache = new(StringComparer.Ordinal);

    public HhsTxtSource(DataAssetLocator assets, PatcherLog log)
    {
        _assets = assets;
        _log = log;
    }

    public HeelHeight? TryGetHeight(string meshDataPath)
    {
        var txtPath = Path.ChangeExtension(DataAssetLocator.Normalize(meshDataPath), ".txt");

        if (!_cache.TryGetValue(txtPath, out var height))
        {
            height = Read(txtPath);
            _cache[txtPath] = height;
        }

        return height.HasValue ? new HeelHeight(height.Value, SourceName, txtPath) : null;
    }

    private float? Read(string txtPath)
    {
        if (!_assets.TryReadAllText(txtPath, out var text, out var origin)) return null;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#')) continue;

            var separator = line.IndexOf('=');
            if (separator <= 0) continue;

            var key = line[..separator].Trim();
            if (!string.Equals(key, "Height", StringComparison.OrdinalIgnoreCase)) continue;

            var value = line[(separator + 1)..].Trim();

            // These files are written by mod authors on all sorts of machines, so parse with the
            // invariant culture and never with the current one. Accept a comma as decimal mark too.
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ||
                float.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            {
                _log.Debug($"txt height {parsed:0.00} from {txtPath} ({origin})");
                return parsed;
            }

            _log.Warn($"could not parse '{line}' in {txtPath}");
            return null;
        }

        _log.Warn($"{txtPath} exists but contains no 'Height=' line");
        return null;
    }
}
