using System.Text;
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
    private const string ExtraDataBlockType = "NiFloatExtraData";
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
            _log.Debug($"nif cache hit: {key} -> {(cached.HasValue ? cached.Value.ToString("0.00") : "none")}");
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

            if (!HeaderMentionsHhsExtraData(buffer, meshDataPath))
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

                _log.Debug($"nif HHS extra data in {meshDataPath}: {block.FloatData:0.00}");
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
    /// Parses only the NIF header and reports whether both the <c>NiFloatExtraData</c> block type
    /// and the string <c>HHS</c> are present. Returns true on any parse trouble so an unusual but
    /// valid mesh still gets the full parse rather than being silently dropped.
    /// </summary>
    private bool HeaderMentionsHhsExtraData(Stream stream, string meshDataPath)
    {
        try
        {
            using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

            // "Gamebryo File Format, Version 20.2.0.7\n"
            var magic = new StringBuilder();
            for (var i = 0; i < 128; i++)
            {
                var c = reader.ReadByte();
                if (c == '\n') break;
                magic.Append((char)c);
            }

            if (!magic.ToString().StartsWith("Gamebryo File Format", StringComparison.Ordinal))
            {
                _log.Debug($"not a Gamebryo nif, parsing anyway: {meshDataPath}");
                return true;
            }

            var version = reader.ReadUInt32();
            if (version < 0x14020005)
            {
                // Older files store no block size table; leave those to the full parser.
                return true;
            }

            reader.ReadByte();                      // endianness
            reader.ReadUInt32();                    // user version
            var blockCount = reader.ReadUInt32();
            var bsVersion = reader.ReadUInt32();

            SkipExportString(reader);               // author
            if (bsVersion > 130) reader.ReadUInt32();
            SkipExportString(reader);               // process script
            SkipExportString(reader);               // export script
            if (bsVersion == 130) SkipExportString(reader);   // max filepath

            var blockTypeCount = reader.ReadUInt16();
            var hasExtraDataType = false;
            for (var i = 0; i < blockTypeCount; i++)
            {
                if (ReadSizedString(reader) == ExtraDataBlockType) hasExtraDataType = true;
            }

            if (!hasExtraDataType) return false;

            stream.Seek(blockCount * 2L, SeekOrigin.Current);   // block type index
            stream.Seek(blockCount * 4L, SeekOrigin.Current);   // block sizes

            var stringCount = reader.ReadUInt32();
            reader.ReadUInt32();                                // max string length
            for (var i = 0u; i < stringCount; i++)
            {
                if (string.Equals(ReadSizedString(reader), HhsExtraDataName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _log.Debug($"nif header scan inconclusive for '{meshDataPath}' ({ex.GetType().Name}), parsing fully");
            return true;
        }
    }

    /// <summary>Export info string: one length byte, then that many bytes including a terminator.</summary>
    private static void SkipExportString(BinaryReader reader)
    {
        var length = reader.ReadByte();
        if (length > 0) reader.BaseStream.Seek(length, SeekOrigin.Current);
    }

    /// <summary>Header string: uint32 length, then exactly that many characters, no terminator.</summary>
    private static string ReadSizedString(BinaryReader reader)
    {
        var length = reader.ReadUInt32();
        if (length == 0) return string.Empty;
        if (length > 4096) throw new InvalidDataException($"implausible string length {length}");
        return Encoding.ASCII.GetString(reader.ReadBytes((int)length));
    }
}
