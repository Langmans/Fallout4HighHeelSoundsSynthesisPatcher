using System.IO.Compression;
using FO4HeelSoundPatcher.Logging;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Archives;
using Noggog;

namespace FO4HeelSoundPatcher.Assets;

/// <summary>
/// Resolves Data-relative paths against loose files first and BA2 archives second.
/// <para>
/// Mutagen ships <c>GameAssetProvider</c> for this, but its archive half re-opens every applicable
/// archive on every single lookup. With hundreds of meshes to check that is far too slow, so this
/// builds one flat index of every archived path the first time an archive lookup is needed and
/// serves everything from there.
/// </para>
/// </summary>
public sealed class DataAssetLocator
{
    private readonly string _dataFolder;
    private readonly bool _searchArchives;
    private readonly PatcherLog _log;
    private readonly List<FilePath> _archivePaths = new();

    /// <summary>Normalised archived path -> the file entry. Built lazily on first archive lookup.</summary>
    private Dictionary<string, IArchiveFile>? _archiveIndex;

    /// <summary>Where the files actually came from, for the run summary.</summary>
    public int LooseFilesRead { get; private set; }

    public int ArchivedFilesRead { get; private set; }

    public int FilesNotFound { get; private set; }

    /// <summary>
    /// Archives in the Next-Gen BA2 format. Their entries come back still compressed, so they are
    /// worth naming in the log: it is the difference between "no heel data in your archived mods"
    /// meaning there is none, and it meaning nothing could be read.
    /// </summary>
    public int NextGenArchives { get; private set; }

    public string Statistics =>
        _searchArchives
            ? $"Files read: {LooseFilesRead} loose, {ArchivedFilesRead} from BA2, " +
              $"{FilesNotFound} not found"
            : $"Files read: {LooseFilesRead} loose, {FilesNotFound} not found (archives not searched)";

    public DataAssetLocator(string dataFolder, PatcherLog log, bool searchArchives = true)
    {
        _dataFolder = dataFolder;
        _log = log;
        _searchArchives = searchArchives;
        _log.Info($"Data folder: {dataFolder}");

        if (!searchArchives)
        {
            // An empty index short-circuits every archive lookup, so nothing is ever opened.
            _archiveIndex = new Dictionary<string, IArchiveFile>(StringComparer.Ordinal);
            _log.Info("BA2 archives: not searched (loose files only)");
            return;
        }

        try
        {
            _archivePaths.AddRange(Archive.GetApplicableArchivePaths(GameRelease.Fallout4, dataFolder));
            _log.Info($"Applicable BA2 archives: {_archivePaths.Count}");
        }
        catch (Exception ex)
        {
            _log.Warn($"Could not enumerate BA2 archives, falling back to loose files only: {ex.Message}");
        }
    }

    /// <summary>
    /// Normalises a Data-relative path for comparison: forward slashes become backslashes, leading
    /// separators are dropped and the whole thing is lowercased.
    /// </summary>
    public static string Normalize(string path)
    {
        var normalized = path.Replace('/', '\\').Trim();
        while (normalized.StartsWith('\\')) normalized = normalized[1..];
        return normalized.ToLowerInvariant();
    }

    /// <summary>
    /// Turns a world model path from an ArmorAddon record into a Data-relative path.
    /// <para>
    /// Mirrors HHS's own <c>File::GetRelativeDir</c>: a path that already starts with
    /// <c>meshes\</c> is kept, a leading <c>data\</c> is stripped, and anything else gets
    /// <c>meshes\</c> prepended.
    /// </para>
    /// </summary>
    public static string ToMeshDataPath(string worldModelPath)
    {
        var normalized = Normalize(worldModelPath);

        if (normalized.StartsWith("meshes\\", StringComparison.Ordinal)) return normalized;
        if (normalized.StartsWith("data\\", StringComparison.Ordinal)) return normalized["data\\".Length..];

        return "meshes\\" + normalized;
    }

    public bool Exists(string dataRelativePath)
    {
        var normalized = Normalize(dataRelativePath);
        if (File.Exists(Path.Combine(_dataFolder, normalized))) return true;
        return ArchiveIndex().ContainsKey(normalized);
    }

    /// <summary>
    /// Opens a Data-relative path. The caller owns the returned stream.
    /// <paramref name="origin"/> reports where it came from, for logging.
    /// </summary>
    /// <param name="dataRelativePath">Path to open, relative to the Data folder.</param>
    /// <param name="stream">The opened stream; the caller owns it.</param>
    /// <param name="origin">Where it came from, for logging.</param>
    /// <param name="maxBytes">
    /// Stop after this many bytes when the entry has to be decompressed. Zero means all of it.
    /// Useful when only a file header is needed - inflating a whole mesh to read its first few
    /// kilobytes is most of the cost of scanning archived meshes.
    /// </param>
    public bool TryOpen(string dataRelativePath, out Stream stream, out string origin, int maxBytes = 0)
    {
        var normalized = Normalize(dataRelativePath);

        var loosePath = Path.Combine(_dataFolder, normalized);
        if (File.Exists(loosePath))
        {
            try
            {
                stream = File.OpenRead(loosePath);
                origin = "loose";
                LooseFilesRead++;
                return true;
            }
            catch (Exception ex)
            {
                _log.Warn($"Could not read loose file '{normalized}': {ex.Message}");
            }
        }

        if (ArchiveIndex().TryGetValue(normalized, out var archiveFile))
        {
            try
            {
                stream = OpenArchived(archiveFile, maxBytes);
                origin = "ba2";
                ArchivedFilesRead++;
                return true;
            }
            catch (Exception ex)
            {
                _log.Warn($"Could not read '{normalized}' from BA2: {ex.Message}");
            }
        }

        FilesNotFound++;
        stream = Stream.Null;
        origin = string.Empty;
        return false;
    }

    public bool TryReadAllText(string dataRelativePath, out string text, out string origin)
    {
        if (TryOpen(dataRelativePath, out var stream, out origin))
        {
            using (stream)
            using (var reader = new StreamReader(stream))
            {
                text = reader.ReadToEnd();
                return true;
            }
        }

        text = string.Empty;
        return false;
    }

    /// <summary>
    /// Lists every file directly inside a Data-relative folder with the given extension, across
    /// loose files and archives. Returns normalised Data-relative paths, deduplicated.
    /// </summary>
    public IReadOnlyList<string> ListFiles(string dataRelativeFolder, string extension)
    {
        var folder = Normalize(dataRelativeFolder).TrimEnd('\\');
        var ext = extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();
        var results = new SortedSet<string>(StringComparer.Ordinal);

        var looseFolder = Path.Combine(_dataFolder, folder);
        if (Directory.Exists(looseFolder))
        {
            foreach (var file in Directory.EnumerateFiles(looseFolder, "*" + ext, SearchOption.TopDirectoryOnly))
                results.Add(folder + "\\" + Path.GetFileName(file).ToLowerInvariant());
        }

        var prefix = folder + "\\";
        foreach (var archived in ArchiveIndex().Keys)
        {
            if (!archived.StartsWith(prefix, StringComparison.Ordinal)) continue;
            if (!archived.EndsWith(ext, StringComparison.Ordinal)) continue;
            // Direct children only.
            if (archived.IndexOf('\\', prefix.Length) >= 0) continue;
            results.Add(archived);
        }

        return results.ToList();
    }

    /// <summary>
    /// Reads an archive entry, working around archives whose entries the reader does not realise
    /// are compressed.
    /// <para>
    /// Fallout 4's Next-Gen BA2 format (version 8, also produced by the backported Archive2) stores
    /// its entry sizes differently, and Mutagen 0.54 reads a compressed entry as an uncompressed
    /// one - handing back the raw zlib blob. Every mesh out of such an archive then fails to parse.
    /// Inflating it here recovers the real contents. An entry that genuinely is not compressed does
    /// not carry a zlib header, so this is a no-op for archives that already work.
    /// </para>
    /// </summary>
    private static MemoryStream OpenArchived(IArchiveFile file, int maxBytes)
    {
        var bytes = file.GetBytes();

        if (LooksLikeZlib(bytes))
        {
            try
            {
                using var compressed = new MemoryStream(bytes);
                using var inflater = new ZLibStream(compressed, CompressionMode.Decompress);
                var inflated = new MemoryStream();

                if (maxBytes > 0)
                {
                    var buffer = new byte[Math.Min(maxBytes, 81920)];
                    int read;
                    while (inflated.Length < maxBytes &&
                           (read = inflater.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        inflated.Write(buffer, 0, read);
                    }
                }
                else
                {
                    inflater.CopyTo(inflated);
                }

                inflated.Position = 0;

                return inflated;
            }
            catch (InvalidDataException)
            {
                // Not actually zlib after all; fall through and use the bytes as they came.
            }
        }

        return new MemoryStream(bytes);
    }

    /// <summary>
    /// Reads the archive's format version from its header. Version 7 and up is the Next-Gen
    /// layout, which is what the backported Archive2 writes too.
    /// </summary>
    private static bool IsNextGenFormat(FilePath archivePath)
    {
        try
        {
            using var stream = File.OpenRead(archivePath);
            using var reader = new BinaryReader(stream);

            if (new string(reader.ReadChars(4)) != "BTDX") return false;
            return reader.ReadUInt32() >= 7;
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <summary>
    /// A zlib stream starts with 0x78 and a two byte header whose big-endian value is a multiple
    /// of 31. Checking both makes a false positive on real file content very unlikely.
    /// </summary>
    private static bool LooksLikeZlib(byte[] data) =>
        data.Length >= 2 && data[0] == 0x78 && ((data[0] << 8) | data[1]) % 31 == 0;

    /// <summary>
    /// The only extensions this patcher ever looks up. Archives hold hundreds of thousands of
    /// textures, sounds and animations that can never be a lookup hit, and indexing those costs
    /// both time and memory for nothing.
    /// </summary>
    private static readonly string[] IndexedExtensions = [".txt", ".json", ".nif"];

    private static bool IsWorthIndexing(string normalizedPath) =>
        IndexedExtensions.Any(ext => normalizedPath.EndsWith(ext, StringComparison.Ordinal));

    private Dictionary<string, IArchiveFile> ArchiveIndex()
    {
        if (_archiveIndex is not null) return _archiveIndex;

        _archiveIndex = new Dictionary<string, IArchiveFile>(StringComparer.Ordinal);
        if (_archivePaths.Count == 0) return _archiveIndex;

        var indexed = 0;
        foreach (var archivePath in _archivePaths)
        {
            try
            {
                if (IsNextGenFormat(archivePath)) NextGenArchives++;

                var reader = Archive.CreateReader(GameRelease.Fallout4, archivePath);
                var before = _archiveIndex.Count;
                foreach (var file in reader.Files)
                {
                    var path = Normalize(file.Path);
                    if (!IsWorthIndexing(path)) continue;

                    // Later archives must not shadow earlier ones; first listing wins, matching
                    // the order GetApplicableArchivePaths hands them to us.
                    _archiveIndex.TryAdd(path, file);
                }

                indexed++;
                _log.Debug($"Indexed BA2 '{archivePath.Name}': {_archiveIndex.Count - before} new entries");
            }
            catch (Exception ex)
            {
                _log.Warn($"Could not read BA2 '{archivePath.Name}': {ex.Message}");
            }
        }

        var nextGen = NextGenArchives > 0
            ? $", {NextGenArchives} of them Next-Gen format"
            : string.Empty;
        _log.Info(
            $"Indexed {_archiveIndex.Count} relevant files ({string.Join("/", IndexedExtensions)}) " +
            $"from {indexed}/{_archivePaths.Count} BA2 archives{nextGen}");
        return _archiveIndex;
    }
}
