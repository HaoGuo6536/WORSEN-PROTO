# Third-party content

The GitHub repository is **public**, so third-party packages and Asset Store content are never committed (owner decision, 2026-09-30); they are git-ignored and restored per machine from their original source. Project-made assets are not committed yet either: scenes, prefabs, audio selections and art under `Assets/` currently exist only in the owner's working copy. **A fresh clone therefore does not compile or run.** The tracked assembly definitions reference two packages below.

| Package | Location | Version | Source and licence | Referenced by tracked code |
|---|---|---|---|---|
| Lumen: Stylized Light FX 2 (Distant Lands) | `Packages/com.distantlands.lumen/` (embedded) | 2.1.3 | Unity Asset Store; Standard EULA | **Yes**: `DistantLands.Lumen.Runtime` (Domain, Presentation and test asmdefs) |
| Weird: Dither Fog (Fronkon Games) | `Assets/External/FronkonGames/` | see package | Unity Asset Store; Standard EULA | **Yes**: `FronkonGames.Weird.DitherFog` |
| Steam Audio 4.8.1 (Valve) | `Assets/Plugins/SteamAudio/` | 4.8.1 | github.com/ValveSoftware/steam-audio release v4.8.1, `steamaudio_unity_4.8.1.zip` (138,708,392 bytes, sha256 `9b7e3689561670e6b4b8e0c579a2d951bb4a0589f7d46c23bc9df923d62940f0`; inner `SteamAudio.unitypackage` sha256 `a080b5c9…e965e2`); Apache-2.0; owner-approved 2026-09-30 | No (HRTF spatializer; routing pending) |
| PrimeTween | `Packages/com.kyrylokuzyk.primetween.tgz` (manifest `file:` entry), `Assets/Plugins/PrimeTween/` | see tarball | Asset Store / OpenUPM; MIT | No |
| PlayMaker | `Assets/Plugins/PlayMaker/` | see package | Unity Asset Store; Standard EULA | No |
| Synaptic AI Pro (Unity bridge for agents) | `Assets/Synaptic AI Pro/` | 1.2.26 | Unity Asset Store; Standard EULA | No (editor tooling) |
| Final IK, UMotion and art, VFX and SFX packs | `Assets/External/*` | per pack | Unity Asset Store; Standard EULA | No; some project assets reference their content |

## Restoring on a new machine

1. Install the packs from the owner's Asset Store account (Package Manager, My Assets) into the locations above.
2. Open the project; Unity regenerates `Library/`.
3. Check with `tools/integration/compile.ps1 -Worktree . -RunName restore-check`.

## Rules

- Never commit files under the ignored vendor paths, and never copy Asset Store content (including clips cut from SFX packs) into tracked folders.
- A new package needs the owner's approval before download; record it here with its source, version and licence.
- Project code that references vendor code goes through the assembly definitions listed above.
