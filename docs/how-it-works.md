# How it works

[← back to the readme](../README.md)

## The problem

In Fallout 4, footstep sounds come from a **FootstepSet** (`FSTS`) linked from an **ArmorAddon**
(`ARMA`) record's `FootstepSound` field (`SNDD`). To give heels their own sound you point that
field at the heel footstep set.

[High Heel Sounds](https://www.nexusmods.com/fallout4/mods/45345) supplies exactly one such set,
`HHS_HeelFootstepSet` (`0026D8` in `HighHeelSounds.esm`). Nothing links to it automatically, so
every outfit needs a hand-made patch.

Meanwhile HHS and HO3 already know which armor is a heel, because they need a *height* to raise
your character by. This patcher reads that height and does the linking.

## Where heel heights come from

Four places, consulted in this order by default. The first one with an answer for a given piece of
armor wins, and the rest are not asked. **The order is a setting** — see
[Detection order](settings.md#detection-order) — and removing a source from it stops that source
being read at all.

| Source | Where it lives |
|---|---|
| [`HhsJson`](#hhsjson) | `Data\F4SE\Plugins\HHS\*.json` |
| [`HhsNif`](#hhsnif) | inside the mesh |
| [`HhsTxt`](#hhstxt) | a `.txt` beside the mesh |
| [`Ho3Script`](#ho3script) | a script property on the Armor record |

Loose files and BA2 archives are both searched, for every source.

A height of exactly `0` means "not a heel" throughout HHS, not "zero height".

### HhsJson

One or more json files in `Data\F4SE\Plugins\HHS`, which exist so a mod does not need a pile of
`.txt` files. Every top level key is a group holding an array of entries, and entries come in two
shapes:

```json
{
  "Myshoes1"   : [ { "key"    : "MyShoes1\\MyShoes.nif", "value" : 10 } ],
  "MyShoes.esp": [ { "formid" : "00800", "gender" : 1,   "value" : 10 } ]
}
```

An entry with a non-empty `key` gives the mesh path directly. Otherwise `formid` is looked up in the
plugin named by the group — and it is an **ArmorAddon**, not an Armor, despite what the grouping
suggests. Its world model path is then used, so both shapes end up meaning "this mesh is this high".

`gender` picks which model: `0` male, `1` female, `2` both. `3` means an object modification's
material swap model, which this patcher does not handle and logs instead.

When the same mesh appears more than once, the highest value is kept.

### HhsNif

A `NiFloatExtraData` block named `HHS` inside the mesh itself, with the height as its float data.
The name is matched case insensitively.

Mod authors add this with NifSkope or Outfit Studio. It is the rarest of the four in practice.

**The block has to be attached to a node** — see [below](#in-mesh-data-has-to-be-attached).

### HhsTxt

The classic HHS method: a `.txt` beside the mesh with the same base name, containing a line like

```
Height=13.1
```

Two locations are tried, in this order:

1. `meshes\<path>\<name>.txt`, next to the mesh
2. `Data\F4SE\Plugins\HHS\<name>.txt`, keyed on the file name alone

HHS matches the key case insensitively, tolerates whitespace around the `=`, and allows a negative
value. Parsing is always invariant, so a file written on a machine with a comma decimal separator
is read the same way everywhere.

This is the easiest source to add yourself — see
[Adding heel data to a mod yourself](#adding-heel-data-to-a-mod-yourself).

### Ho3Script

[HO3](https://www.nexusmods.com/fallout4/mods/82318) attaches a Papyrus script to the Armor record
rather than putting anything in the mesh. In xEdit the VMAD subrecord shows the script as
`HHSOutfit3:HHSOutfit3` with two float properties:

- `HHSHeight` — the height, which is what this patcher reads
- `GroundClipAllowance` — how far the shoe may sink into the ground; not relevant here

Both are declared `Float` in the compiled script. A whole number stored as an int or a string is
read anyway, and noted in the log.

Unlike the other three this marks the *whole armor*, which is why it needs the
[biped slots](#which-armor-pieces-get-the-sound) to decide which piece makes the sound. HO3 also
deliberately uses `HHSHeight = 0` for flat shoes, which is why the minimum height should stay above
zero.

### Why this order

It is the order HHS itself resolves them in, taken from
[its source](https://github.com/P-K-0/HHS): `Cache::Map::Find` reads the mesh extra data first and
only falls back to the txt file when that comes back zero, while json entries are pre-seeded into
that same cache at load time, where the first write wins. So json beats the mesh, which beats the
txt file. HO3 comes last because mesh data is the more precise answer when both exist.

Matching that order matters because the height feeds the minimum-height filter. Reporting a
different height than the one HHS actually applies would make the filter behave in ways you could
not predict from the mod's files.

Changing the order only affects armor that records a height in more than one place — otherwise
there is nothing to choose between.

## Which armor pieces get the sound

The three HHS sources all hang off a specific mesh, so they identify one specific ArmorAddon and
exactly that piece is patched. This includes the json `formid` form, which despite appearances
resolves to an **ArmorAddon** (not an Armor) and then uses its mesh path — so it too ends up being
about a mesh.

The HO3 script is different: it marks the Armor record as a whole, which says "this outfit is a
heel" without saying which of its pieces is the shoe.

Skyrim solves this with a dedicated `Feet` biped slot. **Fallout 4 has no such slot** — heels
occupy the Body slot (33) or the leg slots. So for HO3 the configured
[heel biped slots](settings.md#heel-biped-slots) decide, falling back to every piece with a model
if none match.

That default was chosen by checking it against hand-made patches: in an existing hand-made heel
sound patch, exactly the two pieces marked Body got the sound, while the choker, stockings and
torso variants did not.

## In-mesh data has to be attached

HHS walks the model's node tree and reads the extra data hanging off each node. A `NiFloatExtraData`
block that exists in the file but is not linked into any node is invisible to it — and that is
exactly what NifSkope's "insert block" leaves behind if you forget to link it.

The patcher checks for this and reports it as a warning rather than adding sound for a height the
game will never apply.

## Performance

`HhsNif` is the only source that has to open mesh files, and meshes run to a megabyte or more, so
it is the only part with a real cost. Note that in the default order it comes *before* `HhsTxt`, so
a mesh is opened even when a `.txt` sits right next to it — that is the price of matching how HHS
resolves things.

What that costs is kept small:

- A mesh is only *fully* parsed when a cheap header scan shows its block type table mentions
  `NiFloatExtraData` and its string table mentions `HHS`. Almost none do. On one test load order
  that is 2 full parses out of 65 meshes opened.
- The header scan reads only the header. Where the file can rewind — which loose files always can —
  nothing more than that is read for a mesh that gets rejected.
- Results are cached per mesh path, since several armor pieces often share one mesh.

The log reports `Meshes opened: N, fully parsed: M` so you can see the effect.

If you do not use the in-mesh method, removing `HhsNif` from the
[detection order](settings.md#detection-order) skips all of this.

### Archives

All three HHS sources can live inside a BA2, so archives are searched for all of them — removing
`HhsNif` does not avoid it. To look a path up without reopening archives every time, the file
tables are read once into an index, on the first file that is not found loose.

Which archives count is not obvious. Mutagen's plain "applicable archives" call returns only the
ones listed in the ini — for Fallout 4 that is the seven vanilla base game archives, and nothing
else. A mod's archive loads because its name matches an enabled plugin (`SomeMod - Main.ba2` for
`SomeMod.esp`), not because it is in the ini, and so does every DLC archive. Both are collected
here, in load order, so a later plugin's assets outrank an earlier one's the way they do in game.

The index then keeps only paths that a lookup could ever ask for. Lookups are driven by ArmorAddon
world model paths, so that means `.nif` and side-car `.txt` under `meshes\`, and `.json` or `.txt`
in the HHS folder. Everything else — textures, sounds, materials, interface — cannot be a hit.

One more exclusion is worth naming: `meshes\AnimTextData\` holds animation text data, over 14000
`.txt` files in vanilla alone, named by hash. They sit under `meshes\` but no armor model points at
them, so they are skipped explicitly.

The log reports what was indexed and where files were then read from:

```
[INFO  ] Indexed 224366 relevant files from 37/37 BA2 archives, 21 of them Next-Gen format
[INFO  ] Files read: 0 loose, 1231 from BA2, 2279 not found
```

A high "not found" count is normal — most armor has no heel data, and a miss is what establishes
that. The `loose` versus `from BA2` split is also the quickest way to tell whether a mod manager's
virtual file system is active: `0 loose` in a modded setup means it is not.

Archive searching can be skipped entirely with
[Search inside BA2 archives](settings.md#search-inside-ba2-archives), at the cost of missing any
mod that packs its heel data.

### Why some entries need inflating

Files inside a BA2 are usually stored compressed, with zlib. Getting at the contents means
decompressing them — *inflating*, in zlib's own terminology. Normally the archive reader does this
for you, based on a flag in the archive's file table.

Fallout 4's Next-Gen BA2 format — version 8, which is also what the backported Archive2 produces —
records its entry sizes in a different layout. Mutagen 0.54 reads a compressed entry in one of
those as though it were uncompressed, and hands back the raw zlib data instead of the file. Left
alone, nothing out of such an archive is usable: a mesh is not a mesh, a `.txt` is binary noise.

So the patcher checks whether what it got back still looks like a zlib stream, and decompresses it
itself if so. An entry that genuinely is not compressed does not carry a zlib header, so archives
that already worked are untouched.

The log says how many of your archives are in that format, read from each archive's own header:

```
... from 37/37 BA2 archives, 21 of them Next-Gen format
```

A count above zero is normal and not a problem — it just means those archives took the extra step.

This is worth knowing about mainly because it is invisible when it goes wrong: the patcher simply
finds no heel data in any archived mod, with nothing obviously broken.

#### Inflating only what is needed

Decompressing is not free, and the header scan only wants the first few kilobytes of a mesh. So an
archived entry is inflated up to 256 KB for the scan — far more than any real NIF header — and only
a mesh that passes the scan is reopened and inflated in full.

Without that cap, scanning vanilla's archived meshes meant inflating close to a gigabyte to read a
few kilobytes of each, which took the run from 3.7 to 9.3 seconds. With it, 4.1 seconds, reading
850 meshes that previously could not be read at all.

## Adding heel data to a mod yourself

If a mod ships heels with no height recorded anywhere, the simplest fix is a text file: put
`Height=12.5` in a `.txt` next to the mesh with the same base name. That works for HHS and for this
patcher both.

The [HHS Resources for modders](https://www.nexusmods.com/fallout4/mods/39850) download has a
step-by-step guide for the in-mesh method, using NifSkope or Outfit Studio.
