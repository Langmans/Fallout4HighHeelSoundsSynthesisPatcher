using FO4HeelSoundPatcher.Assets;
using FO4HeelSoundPatcher.Logging;
using NiflySharp;
using NiflySharp.Blocks;

namespace FO4HeelSoundPatcher.Nif;

/// <summary>
/// Reads the HHS heel height out of a mesh: a <c>NiFloatExtraData</c> block whose name is
/// <c>HHS</c>, with the height in its float data.
/// <para>
/// Fully parsing a mesh is expensive (outfit meshes run to a megabyte or more) and the vast
/// majority of meshes carry no HHS data at all. So every mesh first goes through a cheap header
/// scan that reads only the block type table and the string table; a mesh that never mentions
/// <c>NiFloatExtraData</c> and <c>HHS</c> is rejected without touching the block data. Results are
/// cached per normalised path because several ArmorAddons often share one mesh.
/// </para>
/// </summary>
public sealed class NifHeelHeightReader
{
    private const string HhsExtraDataName = "HHS";

    private readonly DataAssetLocator _assets;
    private readonly PatcherLog _log;
    private readonly Dictionary<string, float?> _cache = new(StringComparer.Ordinal);

    public int MeshesOpened { get; private set; }
    public int MeshesFullyParsed { get; private set; }

    public NifHeelHeightReader(DataAssetLocator assets, PatcherLog log)
    {
        _assets = assets;
        _log = log;
    }

    /// <summary>Returns the HHS height for a Data-relative mesh path, or null when there is none.</summary>
    public float? TryGetHeight(string meshDataPath)
    {
        var key = DataAssetLocator.Normalize(meshDataPath);
        if (_cache.TryGetValue(key, out var cached))
        {
            _log.Debug($"nif cache hit: {key} -> {(cached.HasValue ? Num.Height(cached.Value) : "none")}");
            return cached;
        }

        var height = Read(key);
        _cache[key] = height;
        return height;
    }

    private float? Read(string meshDataPath)
    {
        if (!_assets.TryOpen(meshDataPath, out var stream, out var origin))
        {
            _log.Detail($"mesh not found: {meshDataPath}");
            return null;
        }

        MeshesOpened++;

        try
        {
            // BA2 streams are not necessarily seekable; the header scan and Nifly both need to
            // rewind, so pull the mesh into memory once.
            using var buffer = new MemoryStream();
            using (stream) stream.CopyTo(buffer);
            buffer.Position = 0;

            var couldContain = NifHeader.CouldContainHhsExtraData(buffer, out var diagnostic);
            if (diagnostic is not null) _log.Debug($"{meshDataPath}: {diagnostic}");
            if (!couldContain)
            {
                _log.Debug($"nif fast reject ({origin}): {meshDataPath}");
                return null;
            }

            buffer.Position = 0;
            MeshesFullyParsed++;

            var nif = new NifFile();
            var result = nif.Load(buffer);
            if (result != 0)
            {
                _log.Warn($"nif parse failed (code {result}): {meshDataPath}");
                return null;
            }

            foreach (var block in nif.Blocks.OfType<NiFloatExtraData>())
            {
                if (!string.Equals(block.Name?.String, HhsExtraDataName, StringComparison.OrdinalIgnoreCase))
                    continue;

                // HHS walks the node tree and reads the extra data hanging off each NiAVObject, so
                // a block that exists but is not linked into any node gives no height in game.
                // NifSkope's "insert block" leaves exactly that kind of orphan behind.
                if (nif.GetBlockIndex(block, out var index) && !nif.IsBlockReferenced(index))
                {
                    _log.Warn(
                        $"nif has an '{HhsExtraDataName}' extra data block that is not attached to " +
                        $"any node, so HHS ignores it: {meshDataPath}");
                    continue;
                }

                _log.Debug($"nif HHS extra data in {meshDataPath}: {Num.Height(block.FloatData)}");
                return block.FloatData;
            }

            _log.Debug($"nif mentions NiFloatExtraData/HHS but has no matching block: {meshDataPath}");
            return null;
        }
        catch (Exception ex)
        {
            _log.Warn($"nif read failed for '{meshDataPath}': {ex.GetType().Name} - {ex.Message}");
            return null;
        }
    }
}
