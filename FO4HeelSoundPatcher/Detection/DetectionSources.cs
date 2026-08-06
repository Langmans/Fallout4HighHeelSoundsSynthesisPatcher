using FO4HeelSoundPatcher.Nif;

namespace FO4HeelSoundPatcher.Detection;

/// <summary>
/// The height sources in effect for a run, plus the order to consult them in. A source the user
/// removed from the order is simply null, so it is never constructed and never reads anything.
/// </summary>
/// <param name="Order">The configured order, already resolved and deduplicated.</param>
/// <param name="Txt">Reader for .txt files beside the mesh, or null when not in the order.</param>
/// <param name="Json">Reader for Data\F4SE\Plugins\HHS json, or null when not in the order.</param>
/// <param name="Nif">Reader for in-mesh extra data, or null when not in the order.</param>
/// <param name="Ho3">Reader for the HO3 script property, or null when not in the order.</param>
public sealed record DetectionSources(
    IReadOnlyList<HeightSource> Order,
    HhsTxtSource? Txt,
    HhsJsonSource? Json,
    NifHeelHeightReader? Nif,
    Ho3ScriptSource? Ho3)
{
    /// <summary>
    /// Whether the HO3 script is consulted before any mesh source.
    /// <para>
    /// Mesh sources target one armor piece each while HO3 targets the whole armor, so they cannot
    /// simply be interleaved. What the order does decide is which of the two wins when both have
    /// an answer, and that is what this flag captures.
    /// </para>
    /// </summary>
    public bool Ho3IsFirst { get; } =
        Order.Count > 0 && Order[0] == HeightSource.Ho3Script;
}
