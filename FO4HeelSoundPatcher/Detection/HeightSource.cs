namespace FO4HeelSoundPatcher.Detection;

/// <summary>
/// The places a heel height can be recorded, in the order this patcher consults them by default.
/// </summary>
public enum HeightSource
{
    /// <summary>
    /// A json file in <c>Data\F4SE\Plugins\HHS</c>, keyed by mesh path or by ArmorAddon FormID.
    /// </summary>
    HhsJson,

    /// <summary>A <c>NiFloatExtraData</c> block named <c>HHS</c> inside the mesh.</summary>
    HhsNif,

    /// <summary>A <c>.txt</c> file next to the mesh containing <c>Height=13.1</c>.</summary>
    HhsTxt,

    /// <summary>The <c>HHSHeight</c> property of the HO3 <c>HHSOutfit3</c> script on the Armor.</summary>
    Ho3Script,
}

public static class HeightSourceOrder
{
    /// <summary>
    /// The order HHS itself resolves its own sources in, with HO3 last.
    /// <para>
    /// HHS reads the mesh extra data before the txt file (<c>Cache::Map::Find</c>) and pre-seeds
    /// json entries into that same cache at load time, where the first write wins - so json beats
    /// the mesh, which beats the txt file. HO3 comes last because it identifies the Armor rather
    /// than a specific mesh, and mesh data is the more precise answer when both exist.
    /// </para>
    /// </summary>
    public static readonly IReadOnlyList<HeightSource> Default =
    [
        HeightSource.HhsJson,
        HeightSource.HhsNif,
        HeightSource.HhsTxt,
        HeightSource.Ho3Script,
    ];

    /// <summary>True for the sources that read data attached to a specific mesh.</summary>
    public static bool IsMeshSource(this HeightSource source) => source != HeightSource.Ho3Script;

    /// <summary>
    /// Works out the order actually in effect, and explains it.
    /// <para>
    /// Duplicates are dropped, keeping the first occurrence. An empty selection falls back to the
    /// default rather than detecting nothing, since an empty list is far more likely to be an
    /// accident than a deliberate "turn the patcher off".
    /// </para>
    /// </summary>
    public static IReadOnlyList<HeightSource> Resolve(
        bool useDefault,
        IEnumerable<HeightSource> configured,
        out string explanation)
    {
        if (useDefault)
        {
            explanation = "using the default order";
            return Default;
        }

        var ordered = new List<HeightSource>();
        foreach (var source in configured)
        {
            if (!ordered.Contains(source)) ordered.Add(source);
        }

        if (ordered.Count == 0)
        {
            explanation = "custom order is empty, falling back to the default";
            return Default;
        }

        var omitted = Default.Where(source => !ordered.Contains(source)).ToList();
        explanation = omitted.Count == 0
            ? "using a custom order"
            : $"using a custom order; not consulted: {string.Join(", ", omitted)}";

        return ordered;
    }
}
