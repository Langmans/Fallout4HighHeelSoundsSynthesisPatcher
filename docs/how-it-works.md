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

| Source | Where it lives |
|---|---|
| **HHS json** | `Data\F4SE\Plugins\HHS\*.json`, keyed by mesh path or by plugin + ArmorAddon FormID |
| **HHS nif** | a `NiFloatExtraData` block named `HHS` inside the mesh, holding the height |
| **HHS txt** | `<mesh>.txt` next to the mesh containing `Height=13.1`, then `Data\F4SE\Plugins\HHS\<basename>.txt` |
| **HO3 script** | the `HHSHeight` float property of the `HHSOutfit3` script attached to an Armor record |

Loose files and BA2 archives are both searched.

### Why that order

It is the order HHS itself resolves them in, taken from
[its source](https://github.com/P-K-0/HHS): `Cache::Map::Find` reads the mesh extra data first and
only falls back to the txt file when that comes back zero, while json entries are pre-seeded into
that same cache at load time, where the first write wins. So json beats the mesh, which beats the
txt file.

Matching that order matters because the height feeds the minimum-height filter. Reporting a
different height than the one HHS actually applies would make the filter behave in ways you could
not predict from the mod's files.

A height of exactly `0` means "not a heel" throughout HHS, not "zero height".

The order is a setting, so you can override it — see
[Detection order](settings.md#detection-order). Only armor that records a height in more than one
place is affected, since otherwise there is nothing to choose between.

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

Reading meshes is the only expensive part, so it is avoided:

- Meshes are only opened when the cheaper sources found nothing for that piece.
- An opened mesh is only *fully* parsed when a cheap header scan shows its block type table
  mentions `NiFloatExtraData` and its string table mentions `HHS`. Almost no mesh does, and the
  header is a few kilobytes against a megabyte or more for the whole model.
- Results are cached per mesh path, since several armor pieces often share one mesh.
- BA2 archives are indexed once up front rather than reopened per lookup.

The log reports `Meshes opened: N, fully parsed: M` so you can see the effect.

## Adding heel data to a mod yourself

If a mod ships heels with no height recorded anywhere, the simplest fix is a text file: put
`Height=12.5` in a `.txt` next to the mesh with the same base name. That works for HHS and for this
patcher both.

The [HHS Resources for modders](https://www.nexusmods.com/fallout4/mods/39850) download has a
step-by-step guide for the in-mesh method, using NifSkope or Outfit Studio.
