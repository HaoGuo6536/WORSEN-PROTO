---
name: worsen-audio
description: Local audio understanding with the shared MOSS-Audio install (describe or question SFX, speech, music clips), plus WORSEN audio-asset rules. Use when choosing, checking or describing audio clips for the project.
---

# MOSS-Audio and audio assets for WORSEN


Use [MOSS-Audio](https://github.com/OpenMOSS/MOSS-Audio) for local descriptions and questions about speech, environmental sounds, sound effects, and music. The existing native Windows installation is shared outside the checkout at `%USERPROFILE%\selfhosted\moss-audio` (`C:\Users\Hao Guo\selfhosted\moss-audio` on this machine). Set `MOSS_AUDIO_HOME` to override that location. Source, dependencies, weights, and caches belong there; only generated analysis evidence belongs under `Logs/`.

| Component | Location |
| --- | --- |
| Official source | `<MOSS_AUDIO_HOME>/source/` |
| Python environment | `<MOSS_AUDIO_HOME>/venv/Scripts/python.exe` |
| Model weights | `<MOSS_AUDIO_HOME>/weights/4B-Instruct/` |
| Package and inference caches | `<MOSS_AUDIO_HOME>/cache/` |
| Project scripts and repeat-run instructions | [tools/audio/README.md](../../../tools/audio/README.md) |
| Prior outputs and provenance | Audio selection evidence (local-only, git-ignored): `Logs/AgentValidation/Horror/audio-analysis/SELECTION.md` |

Here `<MOSS_AUDIO_HOME>` means the configured override or the default shared installation above. The installed model is `OpenMOSS-Team/MOSS-Audio-4B-Instruct`, revision `6907a499dc0e87cc77c8ae0fe23fd0eb5476a02d`; source commit is `66326e6e0db34f036c86a76ba005efa4830c69dd`. It was initially installed on 2026-09-15 and relocated on 2026-09-29. Python 3.11.15, PyTorch `2.9.1+cu128`, and Transformers `4.57.1` are pinned in [the runtime lock](../../../tools/audio/environment-lock.txt). Saved evidence records 12 completed clip analyses on 2026-09-15. Relocation verification checks imports, CUDA availability, processor/input preparation, and file hashes; it does not establish a fresh inference run. This is a native command-line installation; no MOSS Docker container or background inference service has been set up. Machine-local runtime files and ignored results are not supplied by a fresh clone or worktree; inspect the shared installation before downloading another copy.

Read the runner README before inference. Coordinate a GPU window, check free VRAM, and keep heavy Unity validation and inference separate; never interrupt another operator to free memory. The runner requires at least 11 GiB free, caps its allocator at 10.5 GiB, and processes one short clip at a time. Its `--coordinator-admitted` flag records admission; it does not acquire the Unity lease. Any protected Unity operation still requires the lease above. Preserve previous evidence before rerunning the fixed-output scripts, or set `MOSS_AUDIO_RESULTS_DIR` to a separate result directory as documented in the README. WAV decoding uses Soundfile and torchaudio resampling to avoid the Windows TorchCodec/FFmpeg dependency. Keep audio local, record prompt/input/model identity with results, and distinguish model descriptions from human listening or verified in-game mix quality. Historical evidence intentionally retains its original paths.


## Audio assets in this repository

- The GitHub repository is public. Clips from Asset Store packs, including clips cut or edited from them, stay in git-ignored or untracked folders (see [VENDOR.md](../../../VENDOR.md)); never commit them.
- Downloading audio from the web needs the owner's approval per file (name, source, licence, size). Prefer CC0 sources and record provenance with each selection.
- Record every clip chosen for a cue slot (slot id, file, source pack or URL, licence, MOSS description, human-listening status) in the selection evidence beside `Logs/AgentValidation/Horror/audio-analysis/SELECTION.md`. MOSS descriptions are model output, not a verified in-game mix.
