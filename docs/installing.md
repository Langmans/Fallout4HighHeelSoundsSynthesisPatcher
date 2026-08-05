# Installing

[← back to the readme](../README.md)

There are three ways to add this patcher to Synthesis. Pick one.

## Git Repository Patcher (recommended)

The normal route for using someone else's patcher. Synthesis clones the repository and builds it
for you, and can update it later without you doing anything.

1. In Synthesis, add a patcher to your group and choose **Git Repository**.
2. Paste the repository URL.
3. Pick `FO4HeelSoundPatcher/FO4HeelSoundPatcher.csproj` as the project.
4. Confirm.

Synthesis will then let you pick a version — a tag for a stable release, or a branch if you want to
follow along with development.

## .synth installer file

`FO4HeelSoundPatcher.synth` in the repository root does the above in one step. Select the group you
want the patcher added to in the Synthesis UI, then double click the file.

## Local Solution Patcher

For working on the patcher itself, or running a copy you have checked out locally.

1. Clone or download the repository.
2. In Synthesis, add a patcher and choose **Solution** → **Existing**.
3. Point it at `FO4HeelSoundPatcher.sln`, then pick the project inside it.

See [Development](development.md) if you plan to change the code — running from your IDE is faster
than going through the Synthesis UI.

## Running it

Run Synthesis the way you normally run tools against your load order. If you use Mod Organizer 2,
that means launching Synthesis **through MO2**, so its virtual file system is active.

This matters more than usual here. The patcher reads mod files from disk — meshes, `.txt` and
`.json` files — not just plugin records. Without the virtual file system it only sees the bare game
`Data` folder, finds none of your mods' files, and quietly patches almost nothing.

If a run comes back with far fewer patches than you expected, this is the first thing to check.

## Load order

The generated patch lists `HighHeelSounds.esm` as a master, so that plugin has to stay enabled.
Synthesis places its output at the end of your load order, which is where it belongs.
