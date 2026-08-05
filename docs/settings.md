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

### Only patch addons without a footstep set
*Default: off — leave it off*

Nearly every Fallout 4 armor piece already points at the vanilla `DefaultFootstepSetXXX`. Turning
this on therefore skips almost everything, which is not what it sounds like it does.

It exists for the case where another patcher has deliberately assigned footstep sets you want to
preserve.

Armor that already points at the configured heel set is skipped either way.

## Detection

### Source toggles
*Default: all on*

One switch per place a heel height can be recorded — the HO3 script, HHS `.txt` files, HHS `.json`
files, and HHS data inside meshes. See [How it works](how-it-works.md) for what each one is.

Turning off **Read HHS extra data inside meshes** is the one worth considering: it is the only
source that has to open mesh files, so it is the only one that costs noticeable time.

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
