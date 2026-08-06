using FO4HeelSoundPatcher.Assets;
using FO4HeelSoundPatcher.Detection;
using FO4HeelSoundPatcher.Filtering;
using FO4HeelSoundPatcher.Logging;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Synthesis;

namespace FO4HeelSoundPatcher;

/// <summary>
/// Walks every winning Armor record, works out whether it is a heel, and assigns the heel footstep
/// set to the ArmorAddon records that should make the sound.
/// </summary>
public sealed class HeelSoundPatcher
{
    private readonly IPatcherState<IFallout4Mod, IFallout4ModGetter> _state;
    private readonly Settings _settings;

    public HeelSoundPatcher(IPatcherState<IFallout4Mod, IFallout4ModGetter> state, Settings settings)
    {
        _state = state;
        _settings = settings;
    }

    public void Run()
    {
        var logPath = _settings.Logging.WriteLogFile ? BuildLogPath() : null;
        using var log = new PatcherLog(_settings.Logging.Verbosity, logPath) { LogFilePath = logPath };

        try
        {
            RunCore(log);
        }
        catch (Exception ex)
        {
            log.Error($"Patcher aborted while processing {log.CurrentContext ?? "<startup>"}");
            log.Error($"{ex.GetType().FullName}: {ex.Message}");
            log.Error(ex.StackTrace ?? "<no stack trace>");
            log.WriteSummary();
            throw;
        }
    }

    private void RunCore(PatcherLog log)
    {
        WriteHeader(log);

        var footstepSet = _settings.Sound.HeelFootstepSet;
        if (footstepSet.FormKey.IsNull)
        {
            log.Error("No heel footstep set configured. Set one in the patcher settings.");
            log.WriteSummary();
            return;
        }

        if (!footstepSet.TryResolve(_state.LinkCache, out var footstepSetRecord))
        {
            log.Error(
                $"Could not resolve the configured footstep set {footstepSet.FormKey}. " +
                $"Is '{footstepSet.FormKey.ModKey}' enabled in the load order?");
            log.WriteSummary();
            return;
        }

        log.Info($"Heel footstep set: {footstepSet.FormKey} '{footstepSetRecord.EditorID}'");

        var order = HeightSourceOrder.Resolve(
            _settings.Detection.UseDefaultSourceOrder, _settings.Detection.SourcePriority, out var why);
        log.Info($"Detection order: {string.Join(" -> ", order)}  ({why})");
        log.RegisterSources(order);

        var assets = new DataAssetLocator(
            _state.DataFolderPath, log,
            _state.LoadOrder.ListedOrder.Select(listing => listing.ModKey),
            _settings.Detection.SearchArchives);
        var sources = DetectionSources.Create(order, assets, _state.LinkCache, log);

        var nameBlacklist = new RegexBlacklist(
            "Armor name blacklist", _settings.Filtering.ArmorNameBlacklist,
            _settings.Filtering.RegexCaseSensitive, log);
        var editorIdBlacklist = new RegexBlacklist(
            "Editor ID blacklist", _settings.Filtering.EditorIdBlacklist,
            _settings.Filtering.RegexCaseSensitive, log);

        var modBlacklist = _settings.Filtering.ModBlacklist.ToHashSet();
        var armorBlacklist = _settings.Filtering.ArmorBlacklist.Select(x => x.FormKey).ToHashSet();
        var heelSlots = _settings.Detection.HeelSlots.ToFlags();

        log.Info(
            $"Heel slots: {(heelSlots == 0 ? "<none configured>" : heelSlots.ToString())}");
        log.Info(
            $"Height window: min {Num.Height(_settings.Detection.MinimumHeelHeight)}, " +
            $"max {(_settings.Detection.MaximumHeelHeight > 0 ? Num.Height(_settings.Detection.MaximumHeelHeight) : "unbounded")}");
        if (_settings.Logging.DryRun) log.Always("DRY RUN - no records will be written");
        log.Always(string.Empty);

        // An ArmorAddon can be shared by several Armors; only touch and report each one once.
        var handledAddons = new HashSet<FormKey>();

        foreach (var armor in _state.LoadOrder.PriorityOrder.Armor().WinningOverrides())
        {
            _state.Cancel.ThrowIfCancellationRequested();
            log.CurrentContext = $"ARMO {armor.FormKey} '{armor.EditorID}'";
            log.Count("armors");

            if (!PassesFilters(armor, modBlacklist, armorBlacklist, nameBlacklist, editorIdBlacklist, log))
                continue;

            if (armor.Armatures.Count == 0)
            {
                log.Skipped("no armature", $"{Describe(armor)} has no armature");
                continue;
            }

            var addons = ResolveAddons(armor);
            if (addons.Count == 0)
            {
                log.Skipped("armature unresolvable",
                    $"{Describe(armor)} has {armor.Armatures.Count} armature entries but none resolve");
                continue;
            }

            var targets = FindTargets(armor, addons, sources, heelSlots, log);
            if (targets.Count == 0) continue;

            log.Count("armors_with_height");
            foreach (var source in targets.Select(target => target.Height.Source).Distinct())
            {
                log.RecordSourceHit(source);
            }

            foreach (var (addon, height) in targets)
            {
                if (!handledAddons.Add(addon.FormKey))
                {
                    log.Debug($"{Describe(armor)} -> ARMA {addon.FormKey} already handled");
                    continue;
                }

                ApplyFootstepSet(armor, addon, height, footstepSet, log);
            }
        }

        log.CurrentContext = null;
        log.Info(string.Empty);
        foreach (var statistic in sources.Statistics) log.Info(statistic);
        log.Info(assets.Statistics);
        log.WriteSummary();
    }

    // ------------------------------------------------------------------ filtering

    private static bool PassesFilters(
        IArmorGetter armor,
        HashSet<ModKey> modBlacklist,
        HashSet<FormKey> armorBlacklist,
        RegexBlacklist nameBlacklist,
        RegexBlacklist editorIdBlacklist,
        PatcherLog log)
    {
        if (modBlacklist.Contains(armor.FormKey.ModKey))
        {
            log.Skipped("plugin blacklisted", $"{Describe(armor)} - plugin is blacklisted");
            return false;
        }

        if (armorBlacklist.Contains(armor.FormKey))
        {
            log.Skipped("armor blacklisted", $"{Describe(armor)} - armor is blacklisted");
            return false;
        }

        var nameMatch = nameBlacklist.Match(armor.Name?.String);
        if (nameMatch is not null)
        {
            log.Skipped("name blacklisted", $"{Describe(armor)} - name matches {nameMatch}");
            return false;
        }

        var editorIdMatch = editorIdBlacklist.Match(armor.EditorID);
        if (editorIdMatch is not null)
        {
            log.Skipped("editor id blacklisted", $"{Describe(armor)} - editor id matches {editorIdMatch}");
            return false;
        }

        return true;
    }

    // ------------------------------------------------------------------ detection

    private List<IArmorAddonGetter> ResolveAddons(IArmorGetter armor)
    {
        var addons = new List<IArmorAddonGetter>(armor.Armatures.Count);
        foreach (var armature in armor.Armatures)
        {
            if (armature.ArmorAddon.TryResolve(_state.LinkCache, out var addon)) addons.Add(addon);
        }

        return addons;
    }

    /// <summary>
    /// Works out which addons should get the sound, consulting the sources in the configured order
    /// and stopping at the first that has an answer.
    /// <para>
    /// The mesh sources hang off one specific addon's model, so they name their own target. The HO3
    /// script marks the whole Armor instead, and the biped slots decide which of its addons make
    /// the sound. Where HO3 sits in the order therefore decides whether it overrides mesh data or
    /// only fills the gaps.
    /// </para>
    /// </summary>
    private List<(IArmorAddonGetter Addon, HeelHeight Height)> FindTargets(
        IArmorGetter armor,
        List<IArmorAddonGetter> addons,
        DetectionSources sources,
        BipedObjectFlag heelSlots,
        PatcherLog log)
    {
        if (sources.Ho3IsFirst)
        {
            var fromScript = FindRecordTargets(armor, addons, sources, heelSlots, log);
            if (fromScript.Count > 0) return fromScript;
        }

        var targets = new List<(IArmorAddonGetter, HeelHeight)>();

        foreach (var addon in addons)
        {
            foreach (var meshPath in MeshPaths(addon))
            {
                var height = sources.FindMeshHeight(meshPath);
                if (height is null) continue;

                if (!WithinRange(height.Value, armor, addon, log)) break;

                targets.Add((addon, height.Value));
                break;
            }
        }

        if (targets.Count > 0) return targets;

        return sources.Ho3IsFirst ? targets : FindRecordTargets(armor, addons, sources, heelSlots, log);
    }

    /// <summary>The HO3 script path: a height for the Armor, with the biped slots picking addons.</summary>
    private List<(IArmorAddonGetter Addon, HeelHeight Height)> FindRecordTargets(
        IArmorGetter armor,
        List<IArmorAddonGetter> addons,
        DetectionSources sources,
        BipedObjectFlag heelSlots,
        PatcherLog log)
    {
        var targets = new List<(IArmorAddonGetter, HeelHeight)>();

        if (sources.Ho3 is null) return targets;

        var recordHeight = sources.Ho3.TryGetHeight(armor);
        if (recordHeight is null) return targets;

        if (!WithinRange(recordHeight.Value, armor, null, log)) return targets;

        var chosen = addons
            .Where(addon => (addon.BodyTemplate?.FirstPersonFlags ?? 0) != 0 &&
                            ((addon.BodyTemplate!.FirstPersonFlags & heelSlots) != 0))
            .ToList();
        var via = "slot match";

        if (chosen.Count == 0)
        {
            if (!_settings.Detection.FallbackToAllAddons)
            {
                log.Skipped("no matching slot",
                    $"{Describe(armor)} has height {Num.Height(recordHeight.Value.Value)} but no addon covers a heel slot");
                return targets;
            }

            chosen = addons.Where(HasWorldModel).ToList();
            via = "slot fallback";

            if (chosen.Count == 0)
            {
                log.Skipped("no addon with model",
                    $"{Describe(armor)} has height {Num.Height(recordHeight.Value.Value)} but no addon has a world model");
                return targets;
            }
        }

        foreach (var addon in chosen)
        {
            targets.Add((addon, recordHeight.Value with { Origin = $"{recordHeight.Value.Origin}, {via}" }));
        }

        return targets;
    }

    /// <summary>Data-relative mesh paths for an addon, in the order the settings ask for.</summary>
    private IEnumerable<string> MeshPaths(IArmorAddonGetter addon)
    {
        var worldModel = addon.WorldModel;
        if (worldModel is null) yield break;

        if (_settings.Detection.CheckFemaleModel)
        {
            var female = worldModel.Female?.File;
            if (!string.IsNullOrWhiteSpace(female)) yield return DataAssetLocator.ToMeshDataPath(female);
        }

        if (_settings.Detection.CheckMaleModel)
        {
            var male = worldModel.Male?.File;
            if (!string.IsNullOrWhiteSpace(male)) yield return DataAssetLocator.ToMeshDataPath(male);
        }
    }

    private static bool HasWorldModel(IArmorAddonGetter addon) =>
        !string.IsNullOrWhiteSpace(addon.WorldModel?.Female?.File) ||
        !string.IsNullOrWhiteSpace(addon.WorldModel?.Male?.File);

    private bool WithinRange(HeelHeight height, IArmorGetter armor, IArmorAddonGetter? addon, PatcherLog log)
    {
        var suffix = addon is null ? string.Empty : $" (ARMA {addon.FormKey})";

        if (height.Value < _settings.Detection.MinimumHeelHeight)
        {
            log.Skipped("below minimum height",
                $"{Describe(armor)}{suffix} - {height.Source} height {Num.Height(height.Value)} " +
                $"< minimum {Num.Height(_settings.Detection.MinimumHeelHeight)}");
            return false;
        }

        if (_settings.Detection.MaximumHeelHeight > 0 && height.Value > _settings.Detection.MaximumHeelHeight)
        {
            log.Skipped("above maximum height",
                $"{Describe(armor)}{suffix} - {height.Source} height {Num.Height(height.Value)} " +
                $"> maximum {Num.Height(_settings.Detection.MaximumHeelHeight)}");
            return false;
        }

        return true;
    }

    // ------------------------------------------------------------------ writing

    private void ApplyFootstepSet(
        IArmorGetter armor,
        IArmorAddonGetter addon,
        HeelHeight height,
        IFormLinkGetter<IFootstepSetGetter> footstepSet,
        PatcherLog log)
    {
        if (addon.FootstepSound.FormKey == footstepSet.FormKey)
        {
            log.Skipped("already set", $"{Describe(armor)} -> ARMA {addon.FormKey} already has the heel set");
            return;
        }

        var existing = addon.FootstepSound;

        var blocked = _settings.Sound.Overwrite switch
        {
            FootstepOverwrite.OnlyWhenUnset when !existing.IsNull => "footstep set already present",
            FootstepOverwrite.UnlessDeliberate when FootstepSets.HasDeliberateSet(addon) =>
                "keeps its own footstep set",
            _ => null,
        };

        if (blocked is not null)
        {
            var name = existing.TryResolve(_state.LinkCache, out var record) && record.EditorID is { } editorId
                ? $"{existing.FormKey} '{editorId}'"
                : existing.FormKey.ToString();

            log.Skipped(blocked,
                $"{Describe(armor)} -> ARMA {addon.FormKey} '{addon.EditorID}' already points at {name}");
            return;
        }

        if (!_settings.Logging.DryRun)
        {
            _state.PatchMod.ArmorAddons.GetOrAddAsOverride(addon).FootstepSound.SetTo(footstepSet);
        }

        log.Patched(
            height.Source,
            height.Value,
            $"ARMO {armor.FormKey} '{armor.EditorID}'",
            $"ARMA {addon.FormKey} '{addon.EditorID}'",
            height.Origin);
    }

    // ------------------------------------------------------------------ helpers

    private void WriteHeader(PatcherLog log)
    {
        log.Always(new string('=', 100));
        log.Always("FO4 High Heel Sounds patcher");
        log.Always(new string('=', 100));
        log.Info($"Output: {_state.OutputPath}");
        log.Info($"Plugins in load order: {_state.LoadOrder.Count}");
        log.Info(
            $"Models checked: female={_settings.Detection.CheckFemaleModel}, " +
            $"male={_settings.Detection.CheckMaleModel}");
        log.Info($"Existing footstep sets: {_settings.Sound.Overwrite}");
    }

    private string BuildLogPath()
    {
        var fileName = string.IsNullOrWhiteSpace(_settings.Logging.LogFileName)
            ? "HeelSoundPatcher.log"
            : _settings.Logging.LogFileName;

        var directory = Path.GetDirectoryName(_state.OutputPath.Path);
        return string.IsNullOrEmpty(directory)
            ? fileName
            : Path.Combine(directory, fileName);
    }

    private static string Describe(IArmorGetter armor) =>
        $"ARMO {armor.FormKey} '{armor.EditorID ?? armor.Name?.String ?? "<unnamed>"}'";

}
