# FO4 High Heel Sounds — Synthesis patcher

Automatically gives high heeled armor its own footstep sound in Fallout 4.

Fallout 4 has no built-in link between "this armor is a heel" and "it should sound like one".
[High Heel Sounds](https://www.nexusmods.com/fallout4/mods/45345) provides the sound as a
FootstepSet, but every ArmorAddon has to be pointed at it by hand, one plugin at a time.

Plenty of mods already record a *heel height*, though — that is what the
[Fallout 4 High Heels System](https://www.nexusmods.com/fallout4/mods/39850) (HHS) and
[HO3 / HHSOutfit3](https://www.nexusmods.com/fallout4/mods/82318) use to lift the character. This
patcher reads that height and assigns the footstep set to the matching ArmorAddon records.

## Requirements

- [Synthesis](https://github.com/Mutagen-Modding/Synthesis)
- .NET 10 SDK (to build)
- `HighHeelSounds.esm` from [High Heel Sounds](https://www.nexusmods.com/fallout4/mods/45345),
  for the default footstep set. Any other FootstepSet can be selected in the settings instead.

The generated patch lists the plugin owning the chosen footstep set as a master — this is
"Option 1" from that mod's description.

## Where heel heights come from

All four sources are read, and all four can be switched off individually. Loose files and BA2
archives are both searched.

| Source | What it looks at |
|---|---|
| **HHS txt** | `meshes\<path>\<name>.txt` next to the mesh, containing `Height=13.1` |
| **HHS json** | `Data\F4SE\Plugins\HHS\*.json`, keyed by mesh path or by plugin + FormID |
| **HHS nif** | a `NiFloatExtraData` block named `HHS` inside the mesh itself |
| **HO3 script** | the `HHSHeight` float property of the `HHSOutfit3` script on an Armor record |

The first three identify one specific ArmorAddon, because they hang off that addon's world model,
so exactly that addon gets the sound.

The HO3 script (and a json entry keyed by FormID) only says "this Armor is a heel". Fallout 4 has
no dedicated feet biped slot, so in that case the configured **heel slots** decide which addons
make the sound — by default Body (33) and the four leg slots. If no addon covers one of those,
the patcher falls back to every addon with a world model.

## Settings

**Sound**
- *Heel footstep set* — defaults to `HHS_HeelFootstepSet` (`0026D8`) from `HighHeelSounds.esm`.
- *Only patch addons without a footstep set* — off by default, and it should stay off: nearly every
  Fallout 4 armor addon already points at the vanilla `DefaultFootstepSetXXX`, so turning it on
  skips almost everything.

**Detection**
- One toggle per source.
- *Minimum heel height* — default `5.0`. Keep it above zero: HO3 deliberately uses `HHSHeight = 0`
  to mark flat shoes, and its own "Zero HHSHeight" test ring would otherwise start clicking.
- *Maximum heel height* — `0` means no upper bound.
- *Heel biped slots*, *fall back to all addons*, and which world models to check.

**Filtering**
- *Armor name blacklist* and *Editor ID blacklist* take regular expressions. Both plain .NET
  patterns (`\bboots$`) and `/pattern/flags` notation (`/\bboots$/i`) work; supported flags are
  `i`, `m`, `s` and `x`. A pattern that does not compile is reported as a warning and skipped
  rather than aborting the run.
- Plugin and individual Armor blacklists.

**Logging**
- *Verbosity* — `Quiet` / `Normal` / `Detailed` / `Debug`. `Detailed` adds a line with the reason
  for every record that was considered and left alone.
- *Write a log file* — writes the full run next to the generated patch, always at Debug detail
  regardless of the console verbosity, so a bad run can be diagnosed afterwards.
- *Dry run* — do all the detection and logging, write no records.

## Reading the log

Every decision is one line:

```
[PATCH ] HHS-txt   h=17.40  ARMO 028101:SomeMod.esp '_NR_BunnyHeels'  ->  ARMA 0280FF:SomeMod.esp '_AA_NR_BunnyHeels'  (meshes\some path\bunnyheels.txt)
[PATCH ] HO3       h=7.50   ARMO 006BEC:SomeMod.esp 'SomeArmor'       ->  ARMA 006BED:SomeMod.esp 'SomeArmor_AA'       (script HHSOutfit3:HHSOutfit3, slot match)
[SKIP  ] ARMO 000401:HHSOutfit3.esl 'HO3_ZeroHeightRing' - HO3 height 0.00 < minimum 5.00
[SKIP  ] ARMO 0595F5:SomeMod.esp '_NR_Nisha_Boots' - name matches /\bboots$/i
[WARN  ] Editor ID blacklist: invalid regex 'invalid[regex' ignored - Unterminated [] set.
```

The run ends with a summary: how many armors were examined, how many heights were found per
source, how many addons were patched, and the skip reasons grouped by count. If nothing was
patched it names the most common reason, which is usually the diagnosis.

## Building and running

```bash
dotnet build
```

To run it standalone against a specific load order, copy `.vscode/launch.example.json` to
`.vscode/launch.json` and fill in your paths, or run it directly:

```bash
dotnet run --project FO4HeelSoundPatcher -- run-patcher --GameRelease Fallout4 --DataFolderPath "<Data folder>" --LoadOrderFilePath "<plugins.txt>" --OutputPath "<output>/HeelSounds.esp"
```

If you use a mod manager with a virtual file system (MO2), launch through it — otherwise the
Data folder only contains the bare game files and none of the mod meshes, txt or json files will
be found.

Add `--ExtraDataFolder "<folder containing settings.json>"` to test a specific settings file.

## Adding it to Synthesis

During development, add it as a **Local Solution Patcher** pointing at
`FO4HeelSoundPatcher/FO4HeelSoundPatcher.csproj`.

`FO4HeelSoundPatcher.synth` is an installer file for sharing the patcher once the repository is on
GitHub — fill in the repository URL before handing it out.

## Credits

- Sounds and the `HighHeelSounds.esm` resource: Carreau,
  [High Heel Sounds](https://www.nexusmods.com/fallout4/mods/45345)
- HHS: PK0, [Fallout 4 High Heels System](https://www.nexusmods.com/fallout4/mods/39850)
- HO3: niston, [HHSOutfit3](https://www.nexusmods.com/fallout4/mods/82318)
- [Mutagen and Synthesis](https://github.com/Mutagen-Modding/Synthesis) by Noggog
- [NiflySharp](https://github.com/ousnius/NiflySharp) by ousnius, for reading meshes
- The Skyrim equivalent [SynHeelsSoundAdd](https://github.com/TokcDK/SynHeelsSoundAdd) by TokcDK,
  which this borrows its overall shape from
