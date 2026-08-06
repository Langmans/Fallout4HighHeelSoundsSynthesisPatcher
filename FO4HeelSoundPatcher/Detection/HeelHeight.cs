namespace FO4HeelSoundPatcher.Detection;

/// <summary>
/// A heel height plus where it came from, for logging and for the run summary.
/// <para>
/// <paramref name="Source"/> is the enum rather than a display string so the log speaks one
/// vocabulary: the names in the detection order, the settings list and the summary breakdown are
/// all the same words.
/// </para>
/// </summary>
/// <param name="Value">The height.</param>
/// <param name="Source">Which source supplied it.</param>
/// <param name="Origin">Where exactly it was read, for the per-record log line.</param>
public readonly record struct HeelHeight(float Value, HeightSource Source, string Origin)
{
    public override string ToString() => $"{Num.Height(Value)} ({Source}: {Origin})";
}
