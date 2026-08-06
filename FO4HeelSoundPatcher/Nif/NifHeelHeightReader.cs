using FO4HeelSoundPatcher.Assets;
using FO4HeelSoundPatcher.Detection;
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
public sealed class NifHeelHeightReader : IMeshHeightSource
{
    public HeightSource Kind => HeightSource.HhsNif;

    private const string HhsExtraDataName = "HHS";

    /// <summary>Enough for any real NIF header; the block and string tables sit well inside this.</summary>
    private const int HeaderScanBytes = 256 * 1024;

    private readonly DataAssetLocator _assets;
    private readonly PatcherLog _log;
    private readonly Dictionary<string, float?> _cache = new(StringComparer.Ordinal);

    public int MeshesOpened { get; private set; }
    public int MeshesFullyParsed { get; private set; }

    /// <summary>Archived meshes whose bytes did not look like a NIF at all.</summary>
    private int _unreadableFromArchive;

    private bool _warnedAboutArchives;

    public string Statistics =>
        $"Meshes opened: {MeshesOpened}, fully parsed: {MeshesFullyParsed}"
        + (_unreadableFromArchive > 0 ? $", {_unreadableFromArchive} archived unreadable" : string.Empty);

    public NifHeelHeightReader(DataAssetLocator assets, PatcherLog log)
    {
        _assets = assets;
        _log = log;
    }

    /// <summary>Returns the HHS height for a Data-relative mesh path, or null when there is none.</summary>
    public HeelHeight? TryGetHeight(string meshDataPath)
    {
        var key = DataAssetLocator.Normalize(meshDataPath);
        if (!_cache.TryGetValue(key, out var cached))
        {
            cached = Read(key);
            _cache[key] = cached;
        }
        else
        {
            _log.Debug($"nif cache hit: {key} -> {(cached.HasValue ? Num.Height(cached.Value) : "none")}");
        }

        return cached.HasValue ? new HeelHeight(cached.Value, Kind, key) : null;
    }

    private float? Read(string meshDataPath)
    {
        // The prescan only needs the header, so cap what gets decompressed. If a mesh turns out
        // to be a candidate it is reopened in full - which is rare enough that the cap pays off.
        if (!_assets.TryOpen(meshDataPath, out var opened, out var origin, HeaderScanBytes))
        {
            _log.Detail($"mesh not found: {meshDataPath}");
            return null;
        }

        MeshesOpened++;

        try
        {
            // Rejecting a mesh only needs its header, and almost every mesh is rejected - meshes
            // here average close to a megabyte, so reading them whole just to look at the first
            // few kilobytes is most of the cost of this source. A seekable stream can be scanned
            // in place and rewound; only a stream that cannot rewind has to be buffered.
            using var stream = AsSeekable(opened);

            var couldContain = NifHeader.CouldContainHhsExtraData(stream, out var diagnostic);
            if (diagnostic is not null)
            {
                _log.Debug($"{meshDataPath}: {diagnostic}");
                if (origin == "ba2" && diagnostic.StartsWith("not a NIF", StringComparison.Ordinal))
                {
                    _unreadableFromArchive++;

                    // Once is enough - otherwise a whole archive's worth of meshes each warn.
                    if (!_warnedAboutArchives)
                    {
                        _warnedAboutArchives = true;
                        _log.Warn(
                            "Meshes read out of a BA2 are not arriving as NIF data, starting with " +
                            $"'{meshDataPath}'. The archive format is most likely newer than the " +
                            "reader can decompress. Heel data packed into archives will be missed; " +
                            "loose files are unaffected.");
                    }
                }
            }

            if (!couldContain)
            {
                _log.Debug($"nif fast reject ({origin}): {meshDataPath}");
                return null;
            }

            MeshesFullyParsed++;

            // Reopen without the cap; the prescan copy may be a truncated prefix.
            if (!_assets.TryOpen(meshDataPath, out var full, out _))
            {
                _log.Warn($"mesh vanished between the header scan and the parse: {meshDataPath}");
                return null;
            }

            using var complete = AsSeekable(full);
            complete.Position = 0;

            var nif = new NifFile();
            var result = nif.Load(complete);
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

    /// <summary>
    /// Returns the stream itself when it can rewind, otherwise a buffered copy. Both the header
    /// scan and Nifly need to start from the beginning, and an archive entry is not guaranteed to
    /// support seeking. Disposing the result also disposes the original.
    /// </summary>
    private static Stream AsSeekable(Stream stream)
    {
        if (stream.CanSeek) return stream;

        using (stream)
        {
            var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            buffer.Position = 0;
            return buffer;
        }
    }
}
