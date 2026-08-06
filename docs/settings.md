# Settings reference

[← back to the readme](../README.md)

Every setting is edited in the Synthesis settings panel for this patcher. Defaults are listed; you
only need to change something if the default does not suit your load order.

## Sound

### Heel footstep set
*Default: `0026D8:HighHeelSounds.esm` (`HHS_HeelFootstepSet`)*

The FootstepSet assigned to matching armor pieces. The generated patch will list whichever plugin
owns it as a master.

Point this somewhere else if you use a different heel sound mod — for example one of the sets in
`HHFootsteps.esp`.

### Footstep sets that may be replaced
*Default: the empty entry, and `03E091:Fallout4.esm` (`DefaultFootstepSetXXX`)*

An armor piece whose current footstep set is on this list gets the heel set anyway. Anything else
is left alone.

The two defaults are the cases where nobody chose anything:

- **the empty entry** — the piece has no footstep set at all
- **`DefaultFootstepSetXXX`** — Fallout 4's placeholder, which nearly all armor carries simply
  because it was never changed

**Why this is not just "replace everything".** Some mods ship their own footstep sounds and wire
them up themselves. IceStorm's Shoes, for instance, comes with `IceStormsShoeSounds.esl` and points
61 of its armor pieces at its own `AutumnHighHeelsFootstepSet`. Replacing those throws away a
deliberate choice and swaps a sound tuned for that outfit for a generic one.

**Add a set to overrule it.** If you would rather have one heel sound throughout, add
`AutumnHighHeelsFootstepSet` to the list and those 61 pieces get patched after all. That is the
point of it being a list rather than a rule baked into the patcher.

**Remove an entry to protect it.** Dropping the placeholder leaves only pieces with no set at all,
which is almost none of them — on one test load order that is 28 patched records down to 1.
Clearing the list entirely patches nothing.

Note that vanilla's barefoot and power armor sets are *not* on the list, and deliberately so.
Unlike the placeholder they say something real about how a piece should sound.

Armor already pointing at the configured heel set is skipped regardless.

When a piece is preserved the log names the set it kept, so it is never a mystery:

```
[SKIP  ] ARMO 000003:IceStormsShoes.esl 'AutumnOutfitShoesHadidHighHeels' -> ARMA 000011 ...
         already points at 000821:IceStormsShoeSounds.esl 'AutumnHighHeelsFootstepSet'
```

### Replace any footstep set
*Default: off*

Ignore the list above and take over every armor piece, whatever it points at. Off by default,
because it silently discards what individual mods chose.

## Detection

### Use the default detection order
*Default: on*

While this is on, the sources are consulted in the order HHS itself uses and the list below is
ignored. Turning it back on is how you undo a custom order — your list is left as you had it.

### Detection order
*Default: `HhsJson`, `HhsNif`, `HhsTxt`, `Ho3Script`*

Which places to look for a heel height, and in what order. The first source that has a height for a
piece of armor wins; the rest are not consulted for it. Only used when the setting above is off.

| Source | Where it reads from |
|---|---|
| `HhsJson` | json files in `Data\F4SE\Plugins\HHS` |
| `HhsNif` | a `NiFloatExtraData` block named `HHS` inside the mesh |
| `HhsTxt` | a `.txt` next to the mesh containing `Height=13.1` |
| `Ho3Script` | the `HHSHeight` property of the HO3 `HHSOutfit3` script |

Each is described in full in
[Where heel heights come from](how-it-works.md#where-heel-heights-come-from).

**Removing a source stops it being read at all.** That is also how you speed up a run: `HhsNif` is
the only source that has to open mesh files. Leaving the list completely empty falls back to the
default order rather than detecting nothing, on the assumption that an empty list is an accident.

**Where you put `Ho3Script` matters.** The three HHS sources read data attached to a specific mesh,
so they name one armor piece. HO3 marks the whole armor and lets the
[heel biped slots](#heel-biped-slots) pick the pieces. Putting `Ho3Script` first means HO3 wins
whenever it has a height; putting it last (the default) means it only fills the gaps where no mesh
data exists.

The log names the order in effect on every run, and says which sources are not being consulted.

The default reflects how HHS resolves its own sources — see
[How it works](how-it-works.md#why-this-order). Changing it changes which height wins for armor
that records more than one, which in turn changes what the minimum height filter does.

### Minimum heel height
*Default: `5.0`*

Below this, no sound. Keep it above zero: HO3 deliberately uses a height of `0` to mark *flat*
shoes, and ships a "Zero HHSHeight" test ring that would otherwise start clicking.

For scale, HO3 describes 0–13 as the normal range, and heel meshes in the wild run from roughly 8
to 17.

### Maximum heel height
*Default: `0`, meaning no upper limit*

### Heel biped slots
*Default: Body (33), and the four leg slots (39, 40, 44, 45)*

Only used when the heel height was found on the armor record itself rather than on a particular
mesh — that is, via the HO3 script. In that case the patcher knows the outfit is a heel but not
which of its pieces is the shoe, so the biped slots decide.

Fallout 4 has no dedicated feet slot, unlike Skyrim, which is why this setting exists at all. Heels
in practice occupy the Body slot or one of the leg slots.

### Fall back to all addons when no slot matches
*Default: on*

If an armor has a heel height but none of its pieces use one of the slots above, patch every piece
that has a model instead of skipping the armor. Turn it off to be strict.

### Search inside BA2 archives
*Default: on*

Whether to look for `.txt`, `.json` and mesh files inside BA2 archives as well as loose on disk.

Turning it off makes a run faster, because the archives' file tables have to be read to build a
lookup index. On a vanilla install that is about a second; with a big modlist it is more.

**It can also make the patcher miss things.** HHS reads all three from archives too — that is its
default behaviour, and only its legacy `bAltRead` mode is loose-files-only — so a mod that packs its
heel data into a BA2 will be raised in game but get no sound from this patcher. Only turn this off
if you know your load order ships everything loose.

The index is built once, on the first file that is not found loose, and only covers paths a lookup
could ever ask for. The log reports how many archives were searched, how many files were indexed,
and where files were eventually read from:

```
[INFO  ] Indexed 224366 relevant files from 37/37 BA2 archives, 21 of them Next-Gen format
[INFO  ] Files read: 0 loose, 1231 from BA2, 2279 not found
```

Next-Gen archives need an extra decompression step that the patcher handles itself — see
[Why some entries need inflating](how-it-works.md#why-some-entries-need-inflating). The count is
informational; any number is fine.

### Check female / male world model
*Default: both on*

Which model paths to look at when searching for `.txt`, in-mesh, or json-by-mesh data. Female is
tried first.

## Filtering

### Armor name blacklist
*Default: empty*

Regular expressions matched against the armor's display name. A match means no sound.

Both plain .NET patterns and `/pattern/flags` notation work:

```
\bboots$
/\bboots$/i
```

Supported flags: `i` ignore case, `m` multiline, `s` dot matches newline, `x` ignore whitespace.
Matching is case insensitive by default — see below.

A pattern that does not compile is reported as a warning in the log and then ignored, so one typo
does not take the run down.

### Editor ID blacklist
*Default: empty*

The same, matched against the armor's Editor ID instead of its display name. Useful when a mod's
in-game names are inconsistent but its Editor IDs follow a pattern.

### Match regexes case sensitively
*Default: off*

Off, every pattern gets ignore-case. On, patterns are case sensitive unless they carry an explicit
`/i` flag.

### Plugin blacklist
*Default: empty*

Armor from these plugins is never patched.

### Armor blacklist
*Default: empty*

Individual armor records that are never patched.

## Logging

### Verbosity
*Default: `Normal`*

| Level | Shows |
|---|---|
| `Quiet` | Warnings, errors and the summary only |
| `Normal` | Also one line per patched record |
| `Detailed` | Also one line per skipped record, with the reason |
| `Debug` | Everything, including individual file lookups and cache hits |

`Detailed` is the one to reach for when something is missing.

### Write a log file
*Default: on*

Writes the full run to `HeelSoundPatcher.log` next to the generated patch. The file always contains
`Debug` level detail regardless of the verbosity above, so there is something to read after a run
that went wrong without having to repeat it.

### Log file name
*Default: `HeelSoundPatcher.log`*

### Dry run
*Default: off*

Do all the detection and logging, write no records. Useful for checking a blacklist before
committing to it.
