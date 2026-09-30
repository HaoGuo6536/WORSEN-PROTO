# Local MOSS-Audio tools

These scripts analyze project audio locally without changing Unity assets or uploading audio. They use a shared native Windows installation outside the checkout. They do not start a background service or Docker container.

## Locations

The default installation is `%USERPROFILE%\selfhosted\moss-audio` (on this machine, `C:\Users\Hao Guo\selfhosted\moss-audio`). Set `MOSS_AUDIO_HOME` to use another installation. It must contain:

| Directory | Content |
| --- | --- |
| `source/` | Official OpenMOSS/MOSS-Audio repository |
| `venv/` | Isolated Python 3.11 environment |
| `weights/4B-Instruct/` | Pinned model, processor, and tokenizer files |
| `cache/` | Package cache (`uv/`) and inference cache (`huggingface/`) |

Reusable scripts are versioned in this `tools/audio/` directory. Results default to `Logs/AgentValidation/Horror/audio-analysis` in the current checkout. Set `MOSS_AUDIO_RESULTS_DIR` to use a different directory; relative paths resolve against the project root, independently of the shell working directory. Keep ambience result directories inside the checkout because the ambience runner records project-relative excerpt paths.

Historical results stay in [audio-analysis](../../Logs/AgentValidation/Horror/audio-analysis/). They include `SELECTION.md`, raw outputs, input hashes, timing, and source/model provenance. Those ignored files are machine-local and may be absent from a fresh clone or worktree. Older records intentionally retain their original installation paths.

## Runtime and provenance

- Official source: [OpenMOSS/MOSS-Audio](https://github.com/OpenMOSS/MOSS-Audio), commit `66326e6e0db34f036c86a76ba005efa4830c69dd`.
- Model: [OpenMOSS-Team/MOSS-Audio-4B-Instruct](https://huggingface.co/OpenMOSS-Team/MOSS-Audio-4B-Instruct/tree/6907a499dc0e87cc77c8ae0fe23fd0eb5476a02d), revision `6907a499dc0e87cc77c8ae0fe23fd0eb5476a02d`.
- Python 3.11.15, PyTorch 2.9.1 CUDA 12.8, Transformers 4.57.1. Exact package versions are in [environment-lock.txt](environment-lock.txt).
- WAV input uses Soundfile and torchaudio resampling to 16 kHz, avoiding the optional Windows TorchCodec/FFmpeg dependency. Language attention uses SDPA; the audio encoder uses eager attention. The upstream model/processor source is unchanged.

The Python environment was recreated at its new location using the existing package cache, so its launchers do not depend on `Logs/LocalTools`. To recreate a missing environment after placing the pinned upstream source at `source/`:

```powershell
$mossRoot = Join-Path $env:USERPROFILE 'selfhosted\moss-audio'
if ($env:MOSS_AUDIO_HOME) { $mossRoot = $env:MOSS_AUDIO_HOME }
uv venv --python 3.11 --relocatable (Join-Path $mossRoot 'venv')
$mossPython = Join-Path $mossRoot 'venv\Scripts\python.exe'
uv pip sync --python $mossPython --cache-dir (Join-Path $mossRoot 'cache\uv') --index-url https://pypi.org/simple --extra-index-url https://download.pytorch.org/whl/cu128 tools/audio/environment-lock.txt
```

Do not recreate an existing environment merely to run the tool. The package lock covers the local WAV evaluation scripts, not optional upstream Gradio or TorchCodec demos. `download_model.py` fetches the pinned weights if needed, verifies existing files before downloading, and writes model manifests to the selected result directory.

## Running the existing evaluation

Coordinate a GPU window first. Keep Unity out of Play Mode and heavy verification during inference; do not interrupt another operator to make room. The runner requires at least 11 GiB of free VRAM, limits PyTorch's allocator to 10.5 GiB, processes one short clip at a time, and unloads the model afterward. `--coordinator-admitted` records admission; it does not acquire the [Unity lease](../coordination/README.md). Protected Unity operations still require that lease.

Run from the project root with the shared interpreter. These scripts replace their latest output files on rerun. Preserve an earlier result directory first, or use a new `MOSS_AUDIO_RESULTS_DIR` with a copy of the verified `model-files.json` manifest. `analyze_ambience.py` additionally expects the authored WAV at `Logs/AgentStaging/Horror/audio/WORSEN_RoomPressure.wav`.

```powershell
$mossRoot = Join-Path $env:USERPROFILE 'selfhosted\moss-audio'
if ($env:MOSS_AUDIO_HOME) { $mossRoot = $env:MOSS_AUDIO_HOME }
$mossPython = Join-Path $mossRoot 'venv\Scripts\python.exe'
& $mossPython tools/audio/prepare_candidates.py
& $mossPython tools/audio/analyze_candidates.py --coordinator-admitted
& $mossPython tools/audio/finalize_selection.py
# Optional: analyze the existing authored ambience excerpt.
& $mossPython tools/audio/analyze_ambience.py --coordinator-admitted
```

The candidate list and selection rationale reproduce the existing twelve-clip evaluation; they are not a general batch-selection interface. Inspect them before adapting the sample. Model descriptions and objective measurements do not establish human listening, perceptual loop quality, or final in-game mix acceptance.
