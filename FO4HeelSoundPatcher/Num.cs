using System.Globalization;

namespace FO4HeelSoundPatcher;

/// <summary>
/// Number formatting for anything the patcher prints.
/// <para>
/// Always invariant. Heel heights come from files written on machines with any decimal separator
/// and get compared against thresholds the user typed, so a log that renders 12.5 as "12,5" makes
/// those comparisons impossible to check by eye.
/// </para>
/// </summary>
internal static class Num
{
    public static string Height(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    public static string Seconds(double value) => value.ToString("0.0", CultureInfo.InvariantCulture);
}
