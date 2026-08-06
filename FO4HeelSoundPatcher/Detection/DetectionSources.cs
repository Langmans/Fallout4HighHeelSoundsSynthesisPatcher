using FO4HeelSoundPatcher.Assets;
using FO4HeelSoundPatcher.Logging;
using FO4HeelSoundPatcher.Nif;
using Mutagen.Bethesda.Plugins.Cache;

namespace FO4HeelSoundPatcher.Detection;

/// <summary>
/// The height sources in effect for a run. A source the user left out of the order is never
/// constructed, so it reads nothing at all.
/// </summary>
/// <param name="MeshSources">Mesh-based sources, already in the order to consult them.</param>
/// <param name="Ho3">The HO3 script reader, or null when it is not in the order.</param>
/// <param name="Ho3IsFirst">Whether the HO3 script outranks the mesh sources.</param>
public sealed record DetectionSources(
    IReadOnlyList<IMeshHeightSource> MeshSources,
    Ho3ScriptSource? Ho3,
    bool Ho3IsFirst)
{
    /// <summary>
    /// Builds the sources for a resolved order.
    /// <para>
    /// This is the one place that knows which class implements which setting entry. Adding a source
    /// means a new enum member, a class implementing <see cref="IMeshHeightSource"/>, and one line
    /// here.
    /// </para>
    /// </summary>
    public static DetectionSources Create(
        IReadOnlyList<HeightSource> order,
        DataAssetLocator assets,
        ILinkCache linkCache,
        PatcherLog log)
    {
        var meshSources = new List<IMeshHeightSource>();

        foreach (var kind in order)
        {
            IMeshHeightSource? source = kind switch
            {
                HeightSource.HhsJson => new HhsJsonSource(assets, linkCache, log),
                HeightSource.HhsNif => new NifHeelHeightReader(assets, log),
                HeightSource.HhsTxt => new HhsTxtSource(assets, log),
                _ => null,
            };

            if (source is not null) meshSources.Add(source);
        }

        return new DetectionSources(
            MeshSources: meshSources,
            Ho3: order.Contains(HeightSource.Ho3Script) ? new Ho3ScriptSource(log) : null,
            Ho3IsFirst: HeightSourceOrder.Ho3OutranksMeshSources(order));
    }

    /// <summary>The first configured source that has a height for this mesh path.</summary>
    public HeelHeight? FindMeshHeight(string meshDataPath)
    {
        foreach (var source in MeshSources)
        {
            var height = source.TryGetHeight(meshDataPath);
            if (height is not null) return height;
        }

        return null;
    }

    /// <summary>Whatever the sources want reported at the end of a run.</summary>
    public IEnumerable<string> Statistics =>
        MeshSources.Select(source => source.Statistics).OfType<string>();
}
