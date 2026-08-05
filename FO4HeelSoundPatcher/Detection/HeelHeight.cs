namespace FO4HeelSoundPatcher.Detection;

/// <summary>A heel height plus where it came from, for logging and for the run summary.</summary>
public readonly record struct HeelHeight(float Value, string Source, string Origin)
{
    public override string ToString() => $"{Value:0.00} ({Source}: {Origin})";
}
