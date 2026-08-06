# Development

[← back to the readme](../README.md)

## Requirements

- .NET 10 SDK
- An IDE with C# support (Visual Studio, Rider, or VS Code with the C# extension)

## Building

```bash
dotnet build
```

The build is configured to be strict:

- Nullable warnings are errors.
- SDK analyzers run at `Recommended`, with the culture rules (CA1304/1305/1310/1311) raised to
  warning. Heel heights are parsed from files written on machines with any decimal separator and
  compared against user-typed thresholds, so culture-sensitive parsing or formatting is a bug here,
  not a style preference. All number formatting goes through `Num`.
- NuGet Audit runs over direct *and* transitive packages, and NU1901–NU1904 are errors, so a
  package with a known advisory fails the build.

```bash
dotnet format --verify-no-changes    # what CI checks
dotnet format                        # fix it
```

## Running it

Running from your IDE is much faster than going through the Synthesis UI, and gives you a debugger.

Copy `.vscode/launch.example.json` to `.vscode/launch.json` and fill in your paths — `launch.json`
is gitignored so personal paths never get committed. Or run it directly:

```bash
dotnet run --project FO4HeelSoundPatcher -- run-patcher --GameRelease Fallout4 --DataFolderPath "<Data folder>" --LoadOrderFilePath "<plugins.txt>" --OutputPath "<output>/HeelSounds.esp"
```

Add `--ExtraDataFolder "<folder containing settings.json>"` to run against a specific settings file
instead of the defaults.

If you use Mod Organizer 2, launch your IDE through it so the virtual file system is active —
otherwise `DataFolderPath` only sees the bare game files and none of the mod meshes, `.txt` or
`.json` files are found.

### A small test load order

Running against a full modlist is slow and non-repeatable. A useful alternative is a hand-built
`Data` folder containing only the plugins and meshes you care about, with the plugins hardlinked
from their real locations so nothing is duplicated on disk, plus a matching `plugins.txt`. Point
`--DataFolderPath` and `--LoadOrderFilePath` at it and a full run takes well under a second.

Do not use a directory junction to a live mod folder if you intend to modify any of the files in
it — you would be editing the real mod.

## Testing

```bash
dotnet test
```

## CI

`.github/workflows/ci.yml` runs the same four steps on every push and pull request, on
`windows-latest` — the tests build real paths with backslashes, which is a separator on Windows and
an ordinary filename character elsewhere.

Restore is also where NuGet Audit runs, and `Directory.Build.props` promotes its findings to
errors, so a dependency advisory fails the build. The workflow also runs weekly on a schedule, so
an advisory published after a merge still surfaces rather than waiting for the next commit.

Dependabot is configured for NuGet and for the actions themselves. Mutagen, Synthesis and Noggog
are grouped into one pull request because they release in lockstep and would not build separately.

## Verifying the output

The generated plugin can be checked in xEdit, but for a quick automated check a binary record
walker is enough — the `bethesda-plugin-records` skill in `.claude/skills/` has a ready-made one,
along with notes on the plugin format.

The strongest check available is comparing against a hand-made patch for the same mod, where one
exists. That tells you not just that records were written, but that the right ones were.

## Layout

```
FO4HeelSoundPatcher/
  Program.cs               Synthesis pipeline setup and the runnability check
  HeelSoundPatcher.cs      the main loop: filter, detect, decide, write
  Settings.cs              everything exposed in the Synthesis settings UI
  Num.cs                   invariant number formatting
  Assets/                  loose file + BA2 lookup
  Detection/               one class per heel height source, plus the order handling
  Filtering/               regex blacklists
  Logging/                 levelled console + file logging
  Nif/                     mesh header scan and extra data reading
docs/                      the documentation you are reading
.claude/skills/            notes on reading plugin and mesh files
```

See [How it works](how-it-works.md) for the reasoning behind the detection order and the biped slot
handling — most of it comes from reading the HHS and HO3 sources rather than from guesswork, and it
is worth knowing before changing any of it.

## Publishing

Synthesis lists patchers by scraping GitHub's dependency graph for projects referencing
`Mutagen.Bethesda.Synthesis`, so a repository shows up on its own once pushed. The requirements are
a solution at the top level of the repository and at least one project referencing that package.

`SynthesisMeta.json` controls the name, description and visibility in the patcher browser. Set
`"Visibility": "Exclude"` to keep it unlisted — the `.synth` installer and the Git Repository
Patcher route still work.

Tag commits to give users stable versions to pick from.
