using System.Text;

namespace FO4HeelSoundPatcher.Logging;

/// <summary>
/// Console + file logger for the patcher run.
/// <para>
/// The console honours <see cref="LogVerbosity"/> so the Synthesis UI stays readable, while the log
/// file always records everything. That way a run that went wrong can be diagnosed afterwards
/// without having to re-run it at a higher verbosity.
/// </para>
/// </summary>
public sealed class PatcherLog : IDisposable
{
    private readonly LogVerbosity _consoleLevel;
    private readonly StreamWriter? _file;
    private readonly Dictionary<string, int> _counters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _skipReasons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _sourceHits = new(StringComparer.Ordinal);
    private readonly DateTime _started = DateTime.Now;

    public int WarningCount { get; private set; }
    public int ErrorCount { get; private set; }

    /// <summary>Set to whatever record is currently being processed, so a crash can name it.</summary>
    public string? CurrentContext { get; set; }

    public PatcherLog(LogVerbosity consoleLevel, string? logFilePath)
    {
        _consoleLevel = consoleLevel;

        if (logFilePath is null) return;

        try
        {
            var dir = Path.GetDirectoryName(logFilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            _file = new StreamWriter(logFilePath, append: false, Encoding.UTF8) { AutoFlush = true };
            _file.WriteLine($"FO4 High Heel Sounds patcher - {_started:yyyy-MM-dd HH:mm:ss}");
            _file.WriteLine(new string('=', 100));
        }
        catch (Exception ex)
        {
            // A missing log file must never take the patch down.
            Console.WriteLine($"[WARN  ] could not open log file '{logFilePath}': {ex.Message}");
            _file = null;
            WarningCount++;
        }
    }

    public string? LogFilePath { get; init; }

    // ---------------------------------------------------------------- raw levels

    public void Error(string message) { ErrorCount++; Write(LogVerbosity.Quiet, "ERROR ", message); }

    public void Warn(string message) { WarningCount++; Write(LogVerbosity.Quiet, "WARN  ", message); }

    public void Info(string message) => Write(LogVerbosity.Normal, "INFO  ", message);

    public void Detail(string message) => Write(LogVerbosity.Detailed, "DETAIL", message);

    public void Debug(string message) => Write(LogVerbosity.Debug, "DEBUG ", message);

    /// <summary>Header lines and the summary, always shown.</summary>
    public void Always(string message) => Write(LogVerbosity.Quiet, "      ", message);

    private void Write(LogVerbosity minLevel, string tag, string message)
    {
        var line = $"[{tag}] {message}";
        if (_consoleLevel >= minLevel) Console.WriteLine(line);
        _file?.WriteLine(line);
    }

    // ---------------------------------------------------------------- structured events

    public void Count(string key, int by = 1) =>
        _counters[key] = _counters.GetValueOrDefault(key) + by;

    public void RecordSourceHit(string source) =>
        _sourceHits[source] = _sourceHits.GetValueOrDefault(source) + 1;

    /// <summary>One line per ArmorAddon that got the heel footstep set.</summary>
    public void Patched(string source, float height, string armor, string addon, string via)
    {
        Count("patched");
        Write(LogVerbosity.Normal, "PATCH ",
            $"{source,-9} h={Num.Height(height)}  {armor}  ->  {addon}  ({via})");
    }

    /// <summary>One line per record that was considered but left alone.</summary>
    public void Skipped(string reasonKey, string message)
    {
        Count("skipped");
        _skipReasons[reasonKey] = _skipReasons.GetValueOrDefault(reasonKey) + 1;
        Write(LogVerbosity.Detailed, "SKIP  ", message);
    }

    // ---------------------------------------------------------------- summary

    public void WriteSummary()
    {
        var elapsed = DateTime.Now - _started;

        Always(string.Empty);
        Always(new string('-', 100));
        Always("Summary");
        Always($"  armors examined       : {_counters.GetValueOrDefault("armors")}");
        Always($"  armors with a height  : {_counters.GetValueOrDefault("armors_with_height")}");
        Always($"  armor addons patched  : {_counters.GetValueOrDefault("patched")}");
        Always($"  skipped               : {_counters.GetValueOrDefault("skipped")}");

        if (_sourceHits.Count > 0)
        {
            Always("  heights found per source:");
            foreach (var (source, count) in _sourceHits.OrderByDescending(x => x.Value))
                Always($"    {source,-12} {count}");
        }

        if (_skipReasons.Count > 0)
        {
            Always("  skip reasons:");
            foreach (var (reason, count) in _skipReasons.OrderByDescending(x => x.Value))
                Always($"    {reason,-28} {count}");
        }

        Always($"  warnings              : {WarningCount}");
        Always($"  errors                : {ErrorCount}");
        Always($"  elapsed               : {Num.Seconds(elapsed.TotalSeconds)}s");

        if (_counters.GetValueOrDefault("patched") == 0)
        {
            Always(string.Empty);
            var topReason = _skipReasons.OrderByDescending(x => x.Value).FirstOrDefault();
            Always(topReason.Key is null
                ? "Nothing was patched. No armor in the load order carried an HHS or HO3 heel height."
                : $"Nothing was patched. Most common reason: '{topReason.Key}' ({topReason.Value}x).");
        }

        if (LogFilePath is not null) Always($"  full log              : {LogFilePath}");
        Always(new string('-', 100));
    }

    public void Dispose()
    {
        _file?.Flush();
        _file?.Dispose();
    }
}
