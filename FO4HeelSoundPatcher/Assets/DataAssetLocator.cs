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
    private readonly PatcherLog _log;
    private readonly List<FilePath> _archivePaths = new();

    /// <summary>Normalised archived path -> the file entry. Built lazily on first archive lookup.</summary>
    private Dictionary<string, IArchiveFile>? _archiveIndex;

    public DataAssetLocator(string dataFolder, PatcherLog log)
    {
        _dataFolder = dataFolder;
        _log = log;

        try
        {
            _archivePaths.AddRange(Archive.GetApplicableArchivePaths(GameRelease.Fallout4, dataFolder));
            _log.Info($"Data folder: {dataFolder}");
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
    public bool TryOpen(string dataRelativePath, out Stream stream, out string origin)
    {
        var normalized = Normalize(dataRelativePath);

        var loosePath = Path.Combine(_dataFolder, normalized);
        if (File.Exists(loosePath))
        {
            try
            {
                stream = File.OpenRead(loosePath);
                origin = "loose";
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
                stream = archiveFile.AsStream();
                origin = "ba2";
                return true;
            }
            catch (Exception ex)
            {
                _log.Warn($"Could not read '{normalized}' from BA2: {ex.Message}");
            }
        }

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
                var reader = Archive.CreateReader(GameRelease.Fallout4, archivePath);
                var before = _archiveIndex.Count;
                foreach (var file in reader.Files)
                {
                    // Later archives must not shadow earlier ones; first listing wins, matching
                    // the order GetApplicableArchivePaths hands them to us.
                    _archiveIndex.TryAdd(Normalize(file.Path), file);
                }

                indexed++;
                _log.Debug($"Indexed BA2 '{archivePath.Name}': {_archiveIndex.Count - before} new entries");
            }
            catch (Exception ex)
            {
                _log.Warn($"Could not read BA2 '{archivePath.Name}': {ex.Message}");
            }
        }

        _log.Info($"Indexed {_archiveIndex.Count} files from {indexed}/{_archivePaths.Count} BA2 archives");
        return _archiveIndex;
    }
}
