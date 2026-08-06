# FO4 High Heel Sounds — Synthesis patcher

Gives high heeled armor its own footstep sound, automatically, for your whole load order.

Fallout 4 has no built-in link between "this armor is a heel" and "it should sound like one".
[High Heel Sounds](https://www.nexusmods.com/fallout4/mods/45345) provides the sound, but every
outfit has to be wired up to it by hand, one plugin at a time — which is why there are only a
handful of patches for it.

Plenty of mods already record how high their heels are, though: that is what the
[Fallout 4 High Heels System](https://www.nexusmods.com/fallout4/mods/39850) (HHS) and
[HO3](https://www.nexusmods.com/fallout4/mods/82318) use to lift your character. This patcher reads
that and does the wiring for you.

## What you need

- [Synthesis](https://github.com/Mutagen-Modding/Synthesis)
- [High Heel Sounds](https://www.nexusmods.com/fallout4/mods/45345) — install the **Packed Master
  Resource** file (`HighHeelSounds.esm` + its BA2) and leave it enabled

You do not need HHS or HO3 themselves for the patcher to run, but without one of them installed
your heels will not actually be raised in game, which rather defeats the point.

## Setup

1. Add the patcher in Synthesis — see [Installing](docs/installing.md).
2. Run the pipeline. That's it.

Run Synthesis through your mod manager (Mod Organizer 2, Vortex) the way you normally would, so it
can see your mods' meshes and files.

## What it does

It looks at every armor in your load order, works out whether it is a heel and how high, and points
the matching armor pieces at the high heel footstep set. Anything that is not a heel is left alone.

Heel heights are picked up from all the places mods put them — text files next to the mesh, HHS json
files, data inside the mesh itself, and HO3's script — including inside BA2 archives.

## Settings

Everything is adjustable in the Synthesis settings panel. The ones people usually touch:

- **Minimum heel height** — how high is high enough to click. Default is `5.0`, which skips flats
  and low heels. Set it lower if you want more armor to make the sound.
- **Armor name blacklist** — regular expressions matched against the armor's name. For example
  `/\bboots$/i` stops anything ending in "boots" from getting heel sounds.
- **Heel footstep set** — swap in a different sound if you use another heel sound mod.
- **Detection order** — which places to read heel heights from, and which wins when a mod records
  more than one. The default follows HHS itself; removing a source stops it being read at all.
- **Dry run** — see exactly what would happen without writing anything.

Full list, including the detection toggles and biped slot options:
[Settings reference](docs/settings.md).

## When it doesn't do what you expected

The patcher explains itself in the Synthesis output, and writes a full log next to the generated
patch. Turn **Verbosity** up to `Detailed` and it will name every armor it skipped and why.

If nothing at all was patched, the summary at the end names the most common reason, which is
usually the answer.

[Troubleshooting](docs/troubleshooting.md) covers the common cases.

## Documentation

- [Installing](docs/installing.md) — adding the patcher to Synthesis
- [Settings reference](docs/settings.md) — every option, and what it changes
- [Troubleshooting](docs/troubleshooting.md) — when the result is not what you expected
- [How it works](docs/how-it-works.md) — the detection sources and the reasoning behind them
- [Development](docs/development.md) — building, running, testing, contributing

## Credits

- Sounds and the `HighHeelSounds.esm` resource: Carreau,
  [High Heel Sounds](https://www.nexusmods.com/fallout4/mods/45345)
- HHS: PK0, [Fallout 4 High Heels System](https://www.nexusmods.com/fallout4/mods/39850)
- HO3: niston, [HHSOutfit3](https://www.nexusmods.com/fallout4/mods/82318)
- [Mutagen and Synthesis](https://github.com/Mutagen-Modding/Synthesis) by Noggog
- [NiflySharp](https://github.com/ousnius/NiflySharp) by ousnius, for reading meshes
- The Skyrim equivalent [SynHeelsSoundAdd](https://github.com/TokcDK/SynHeelsSoundAdd) by TokcDK,
  which this borrows its overall shape from
