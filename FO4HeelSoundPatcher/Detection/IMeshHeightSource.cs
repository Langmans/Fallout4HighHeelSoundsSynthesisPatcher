namespace FO4HeelSoundPatcher.Detection;

/// <summary>
/// A source of heel heights that reads data attached to a specific mesh, and so identifies one
/// ArmorAddon rather than a whole Armor.
/// <para>
/// The HO3 script is deliberately not one of these: it marks the Armor record and needs the biped
/// slots to decide which addons make the sound. Keeping that case out of this interface keeps the
/// difference visible instead of hiding it behind a shared shape that does not fit.
/// </para>
/// </summary>
public interface IMeshHeightSource
{
    /// <summary>Which setting entry this source corresponds to.</summary>
    HeightSource Kind { get; }

    /// <summary>The height recorded for this Data-relative mesh path, if any.</summary>
    HeelHeight? TryGetHeight(string meshDataPath);

    /// <summary>Anything worth reporting at the end of a run, or null.</summary>
    string? Statistics => null;
}
