# Troubleshooting

[← back to the readme](../README.md)

The patcher is built to explain itself. Before anything else, turn **Verbosity** up to `Detailed`
and read the log — it writes one line per armor it skipped, with the reason, and a summary at the
end grouping those reasons by count.

The full log is written next to the generated patch as `HeelSoundPatcher.log`, and always contains
the maximum level of detail regardless of the verbosity setting, so you can go back to a run that
already happened.

## Nothing was patched at all

The summary will say so explicitly and name the most common skip reason. The usual causes:

**Synthesis was not run through your mod manager.** The patcher reads meshes and side-car files
from disk. Without Mod Organizer 2's virtual file system it only sees the bare game `Data` folder.
Check the `Data folder:` line near the top of the log — if it points at your plain game install and
`Applicable BA2 archives` is low, this is it.

**`HighHeelSounds.esm` is missing or disabled.** The patcher refuses to start without the plugin
that owns the configured footstep set, and says which one it wanted.

**Minimum heel height is too high.** If the skip summary is dominated by `below minimum height`,
lower it.

## A specific outfit did not get the sound

Search the log for its name or Editor ID. One of these will be there:

| Reason in the log | What it means |
|---|---|
| `below minimum height` | It has a heel height, but under your threshold. |
| `name blacklisted` / `editor id blacklisted` | One of your regexes matched. The log names which. |
| `no matching slot` | The armor is marked as a heel but none of its pieces use a heel biped slot. See [Settings](settings.md#heel-biped-slots). |
| `footstep set already present` | Only possible if you turned on *Only patch addons without a footstep set*. Turn it back off. |
| `already set` | It already points at the heel footstep set. Nothing to do. |

If it does not appear at all, the mod records no heel height anywhere the patcher can find. That is
a gap in the mod, not in the patcher — it would not be raised by HHS either. You can add a `.txt`
file next to the mesh yourself; see [How it works](how-it-works.md).

## Something got the sound that should not have

Add it to the **Armor name blacklist** or **Editor ID blacklist**. Both take regular expressions,
in either plain form (`\bboots$`) or with delimiters and flags (`/\bboots$/i`).

If a whole mod is wrong, use the **Plugin blacklist** instead.

Use **Dry run** to check your filter before committing to it — it does all the detection and
logging but writes nothing.

## Warnings in the log

**`nif has an 'HHS' extra data block that is not attached to any node`** — the mesh author inserted
the heel height but never linked it into the model, so HHS ignores it in game too. The mesh needs
fixing; the patcher correctly does not add sound.

**`invalid regex ... ignored`** — one of your blacklist patterns does not compile. The rest still
work. The message says what is wrong with it.

**`could not parse ... in <file>.txt`** — a mod ships a malformed HHS text file.

**`malformed HHS json ... skipped`** — a json file in `Data\F4SE\Plugins\HHS` is not valid json.
Other json files are still loaded.

## It is slow

Reading meshes is the expensive part. The patcher only opens a mesh when the cheaper sources found
nothing, only parses it fully when its header actually mentions HHS data, and caches per mesh path.
The log reports `Meshes opened: N, fully parsed: M` so you can see whether that is where the time
went.

If you do not use the in-mesh method at all, removing `HhsNif` from the
[detection order](settings.md#detection-order) skips it entirely.
