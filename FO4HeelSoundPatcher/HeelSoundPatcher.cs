using FO4HeelSoundPatcher.Assets;
using FO4HeelSoundPatcher.Detection;
using FO4HeelSoundPatcher.Filtering;
using FO4HeelSoundPatcher.Logging;
using FO4HeelSoundPatcher.Nif;
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

        var assets = new DataAssetLocator(_state.DataFolderPath, log);
        var nifReader = new NifHeelHeightReader(assets, log);
        var txtSource = new HhsTxtSource(assets, log);
        var jsonSource = _settings.Detection.EnableHhsJson
            ? new HhsJsonSource(assets, _state.LinkCache, log)
            : null;
        var ho3Source = new Ho3ScriptSource(log);

        var nameBlacklist = new RegexBlacklist(
            "Armor name blacklist", _settings.Filtering.ArmorNameBlacklist,
            _settings.Filtering.RegexCaseSensitive, log);
        var editorIdBlacklist = new RegexBlacklist(
            "Editor ID blacklist", _settings.Filtering.EditorIdBlacklist,
            _settings.Filtering.RegexCaseSensitive, log);

        var modBlacklist = _settings.Filtering.ModBlacklist.ToHashSet();
        var armorBlacklist = _settings.Filtering.ArmorBlacklist.Select(x => x.FormKey).ToHashSet();
        var heelSlots = CombineSlots(_settings.Detection.HeelSlots);

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

            var targets = FindTargets(armor, addons, txtSource, jsonSource, nifReader, ho3Source, heelSlots, log);
            if (targets.Count == 0) continue;

            log.Count("armors_with_height");

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
        log.Info($"Meshes opened: {nifReader.MeshesOpened}, fully parsed: {nifReader.MeshesFullyParsed}");
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
    /// Works out which addons should get the sound.
    /// <para>
    /// Mesh-based sources (txt / nif / json-by-mesh) tell us about one specific addon, so those are
    /// used as-is. Record-based sources (the HO3 script, or a json entry keyed by FormID) only say
    /// "this armor is a heel", so the biped slots decide which addons make the sound.
    /// </para>
    /// </summary>
    private List<(IArmorAddonGetter Addon, HeelHeight Height)> FindTargets(
        IArmorGetter armor,
        List<IArmorAddonGetter> addons,
        HhsTxtSource txtSource,
        HhsJsonSource? jsonSource,
        NifHeelHeightReader nifReader,
        Ho3ScriptSource ho3Source,
        BipedObjectFlag heelSlots,
        PatcherLog log)
    {
        var targets = new List<(IArmorAddonGetter, HeelHeight)>();

        foreach (var addon in addons)
        {
            foreach (var meshPath in MeshPaths(addon))
            {
                var height = FindMeshHeight(meshPath, txtSource, jsonSource, nifReader);
                if (height is null) continue;

                if (!WithinRange(height.Value, armor, addon, log)) break;

                log.RecordSourceHit(height.Value.Source);
                targets.Add((addon, height.Value));
                break;
            }
        }

        if (targets.Count > 0) return targets;

        // Nothing per-mesh. HO3 is the only source that marks the Armor record as a whole - the
        // HHS json "formid" form resolves to an ArmorAddon world model and is handled above.
        if (!_settings.Detection.EnableHo3Script) return targets;

        var recordHeight = ho3Source.TryGetHeight(armor);
        if (recordHeight is null) return targets;

        if (!WithinRange(recordHeight.Value, armor, null, log)) return targets;

        var slotMatched = addons
            .Where(addon => (addon.BodyTemplate?.FirstPersonFlags ?? 0) != 0 &&
                            ((addon.BodyTemplate!.FirstPersonFlags & heelSlots) != 0))
            .ToList();

        var chosen = slotMatched;
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

        log.RecordSourceHit(recordHeight.Value.Source);
        foreach (var addon in chosen)
        {
            targets.Add((addon, recordHeight.Value with { Origin = $"{recordHeight.Value.Origin}, {via}" }));
        }

        return targets;
    }

    /// <summary>
    /// Order matters and is taken from HHS itself. Its json entries are pre-seeded into the same
    /// cache that the mesh lookups later fill, and the first write wins, so json beats everything.
    /// After that <c>Cache::Map::Find</c> tries the nif extra data and only falls back to the txt
    /// file when that yields zero.
    /// </summary>
    private HeelHeight? FindMeshHeight(
        string meshPath,
        HhsTxtSource txtSource,
        HhsJsonSource? jsonSource,
        NifHeelHeightReader nifReader)
    {
        if (jsonSource is not null)
        {
            var fromJson = jsonSource.TryGetByMesh(meshPath);
            if (fromJson is not null) return fromJson;
        }

        if (_settings.Detection.EnableHhsNif)
        {
            var fromNif = nifReader.TryGetHeight(meshPath);
            if (fromNif is not null) return new HeelHeight(fromNif.Value, "HHS-nif", meshPath);
        }

        if (_settings.Detection.EnableHhsTxt)
        {
            var fromTxt = txtSource.TryGetHeight(meshPath);
            if (fromTxt is not null) return fromTxt;
        }

        return null;
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

        if (_settings.Sound.OnlyIfFootstepUnset && !addon.FootstepSound.IsNull)
        {
            log.Skipped("footstep set already present",
                $"{Describe(armor)} -> ARMA {addon.FormKey} '{addon.EditorID}' already has " +
                $"footstep set {addon.FootstepSound.FormKey}");
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
            $"Sources enabled: HO3={_settings.Detection.EnableHo3Script}, " +
            $"txt={_settings.Detection.EnableHhsTxt}, json={_settings.Detection.EnableHhsJson}, " +
            $"nif={_settings.Detection.EnableHhsNif}");
        log.Info(
            $"Models checked: female={_settings.Detection.CheckFemaleModel}, " +
            $"male={_settings.Detection.CheckMaleModel}");
        log.Info($"Only patch addons without a footstep set: {_settings.Sound.OnlyIfFootstepUnset}");
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

    private static BipedObjectFlag CombineSlots(IEnumerable<BipedObjectFlag> slots)
    {
        BipedObjectFlag combined = 0;
        foreach (var slot in slots) combined |= slot;
        return combined;
    }

    private static string Describe(IArmorGetter armor) =>
        $"ARMO {armor.FormKey} '{armor.EditorID ?? armor.Name?.String ?? "<unnamed>"}'";

}
