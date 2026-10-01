# ============================================================================
# analyze_candidates.py
# PURPOSE:
#   Run bounded offline MOSS inference against the prospective candidate manifest. Preserve input identities and raw outputs for model-assisted sound selection.
# ARCHITECTURAL ROLE:
#   Offline audio tooling utility; outside Unity runtime and editor assemblies.
# KEY RESPONSIBILITIES:
#   Loads one model, checks input hashes, runs sequential inference, and releases GPU memory.
#   Records optional per-candidate fit questions alongside each unmodified response.
# DEPENDENCIES:
#   PyTorch, torchaudio, Transformers, NumPy, Soundfile, official MOSS source, and moss_paths.
# USAGE NOTES:
#   No Unity lifecycle or asset writes. See README.md for GPU admission and
#   fixed-output overwrite behavior; preserve prior evidence before rerunning.
# ============================================================================
"""Offline, bounded, real MOSS-Audio inference on a prospective WAV sample."""
import argparse
import datetime
import gc
import hashlib
import importlib.metadata
import json
import os
from pathlib import Path
import subprocess
import sys
import time
import traceback

from moss_paths import ROOT, SOURCE, MODEL, RESULT, CACHE

os.environ["HF_HOME"] = str(CACHE / "huggingface")
os.environ["HF_HUB_OFFLINE"] = "1"
os.environ["TRANSFORMERS_OFFLINE"] = "1"
os.environ["HF_HUB_DISABLE_TELEMETRY"] = "1"
sys.path.insert(0, str(SOURCE))

import numpy as np
import soundfile as sf
import torch
import torchaudio
from src.configuration_moss_audio import MossAudioConfig
from src.modeling_moss_audio import MossAudioModel
from src.processing_moss_audio import MossAudioProcessor

PROMPT = (
    "Describe only what you hear in this short audio, without guessing its filename. "
    "Assess its use in a dark first-person horror game. Return one concise JSON object with keys: "
    "description (audible sources and texture), onset (sharp or gradual), mood, "
    "best_role (one of attack_warning, monster_growl, physical_impact, ui_confirm, pickup, unsuitable), "
    "horror_fit (integer 1 to 5), repeated_use_risk, reason (one sentence). "
    "Prefer clear perceptual evidence; mention uncertainty if the source is ambiguous."
)

def utc():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()

def persist(name, value):
    (RESULT / name).write_text(json.dumps(value, indent=2, ensure_ascii=False), encoding="utf-8")

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--coordinator-admitted", action="store_true", help="Only after the coordinator reserves a non-Playtest GPU window.")
    parser.add_argument("--limit", type=int, default=12)
    parser.add_argument("--questions", type=Path, help="Optional JSON candidate-id to fit-question map; selected ids must all be present.")
    args = parser.parse_args()
    if not args.coordinator_admitted:
        raise RuntimeError("GPU admission must be coordinated before model loading.")
    if not (RESULT / "model-files.json").exists():
        raise RuntimeError("Pinned model download/hash verification has not completed.")
    candidates = json.loads((RESULT / "candidates.json").read_text(encoding="utf-8"))["candidates"][:args.limit]
    questions = json.loads(args.questions.read_text(encoding="utf-8-sig")) if args.questions else {}
    if args.questions and (not isinstance(questions, dict) or any(
            not isinstance(questions.get(c["id"]), str) or not questions[c["id"]].strip() for c in candidates)):
        raise ValueError("Every selected candidate needs a nonempty fit question.")
    torch.set_num_threads(4)
    torch.set_num_interop_threads(2)
    torch.manual_seed(0)
    torch.cuda.init()
    free, total = torch.cuda.mem_get_info()
    cap = int(10.5 * 1024 ** 3)
    if free < cap + 512 * 1024 ** 2:
        raise RuntimeError(f"Need >=11 GiB available for bounded model load; free={free / 1024 ** 3:.2f} GiB")
    torch.cuda.set_per_process_memory_fraction(cap / total, 0)
    provenance = {
        "started_utc": utc(), "model_id": "OpenMOSS-Team/MOSS-Audio-4B-Instruct",
        "model_revision": "6907a499dc0e87cc77c8ae0fe23fd0eb5476a02d",
        "source_commit": subprocess.check_output(["git", "-C", str(SOURCE), "rev-parse", "HEAD"], text=True).strip(),
        "source_dirty": bool(subprocess.check_output(["git", "-C", str(SOURCE), "status", "--porcelain"], text=True).strip()),
        "source_sha256": {str(path.relative_to(SOURCE)): hashlib.sha256(path.read_bytes()).hexdigest()
                          for path in (SOURCE / "src").glob("*.py")},
        "python": sys.version, "packages": {name: importlib.metadata.version(name)
            for name in ["torch", "torchaudio", "transformers", "accelerate", "numpy", "soundfile", "safetensors"]},
        "gpu": torch.cuda.get_device_name(0), "gpu_arch": torch.cuda.get_device_capability(0),
        "cuda_runtime": torch.version.cuda, "free_gpu_before_bytes": free, "allocator_limit_bytes": cap,
        "dtype": "bfloat16", "language_attention": "sdpa", "audio_attention": "eager",
        "batch_size": 1, "max_new_tokens": 220, "do_sample": False, "seed": 0,
        "prompt": PROMPT, "audio_io": "soundfile float32 WAV decode; mean channels; torchaudio functional resample to processor mel_sr",
        "candidate_questions": questions,
        "audio_upload": False, "human_listened": False,
    }
    persist("inference-provenance.json", provenance)
    persist("inference-status.json", {"state": "loading", "started_utc": utc()})
    print("LOADING_MOSS_AUDIO", flush=True)
    started = time.perf_counter()
    config = MossAudioConfig.from_pretrained(str(MODEL), local_files_only=True)
    config.language_config._attn_implementation = "sdpa"
    config.audio_config._attn_implementation = "eager"
    model = MossAudioModel.from_pretrained(str(MODEL), config=config, dtype=torch.bfloat16,
        device_map="cuda:0", attn_implementation="sdpa", local_files_only=True)
    model.eval()
    processor = MossAudioProcessor.from_pretrained(str(MODEL), enable_time_marker=True, local_files_only=True)
    provenance["model_load_seconds"] = time.perf_counter() - started
    provenance["parameter_count"] = sum(value.numel() for value in model.parameters())
    provenance["allocated_after_load_bytes"] = torch.cuda.memory_allocated()
    persist("inference-provenance.json", provenance)
    print(f"MODEL_READY allocated={torch.cuda.memory_allocated() / 1024 ** 3:.3f} GiB", flush=True)
    results = []
    for index, candidate in enumerate(candidates):
        print(f"ANALYZE {index+1}/{len(candidates)} {candidate['id']}", flush=True)
        persist("inference-status.json", {"state": "analyzing", "id": candidate["id"], "completed": index, "utc": utc()})
        path = Path(candidate["absolute_path"])
        if hashlib.sha256(path.read_bytes()).hexdigest() != candidate["sha256"]:
            raise RuntimeError("Candidate changed since prospective sample: " + candidate["id"])
        decoded, rate = sf.read(path, dtype="float32", always_2d=True)
        waveform = torch.from_numpy(decoded.mean(axis=1))
        if rate != processor.config.mel_sr:
            waveform = torchaudio.functional.resample(waveform, rate, processor.config.mel_sr)
        raw = waveform.numpy()
        prompt = questions.get(candidate["id"], PROMPT)
        inputs = processor(text=prompt, audios=[raw], return_tensors="pt").to(model.device)
        if inputs.get("audio_data") is not None:
            inputs["audio_data"] = inputs["audio_data"].to(model.dtype)
        inputs["audio_input_mask"] = inputs["input_ids"] == processor.audio_token_id
        torch.cuda.reset_peak_memory_stats()
        start = time.perf_counter()
        with torch.inference_mode():
            generated = model.generate(**inputs, max_new_tokens=220, do_sample=False, num_beams=1, use_cache=True)
        torch.cuda.synchronize()
        input_len = inputs["input_ids"].shape[1]
        text = processor.decode(generated[0, input_len:], skip_special_tokens=True)
        try:
            parsed = json.loads(text[text.index("{"):text.rindex("}")+1])
        except (ValueError, json.JSONDecodeError):
            parsed = None
        result = dict(candidate)
        result.update({"model_output": text, "parsed_output": parsed, "prompt": prompt,
            "model_revision": provenance["model_revision"], "source_commit": provenance["source_commit"],
            "processed_mono_float32_sha256": hashlib.sha256(raw.tobytes()).hexdigest(),
            "processed_sample_rate": processor.config.mel_sr, "input_tokens": input_len,
            "output_tokens": generated.shape[1] - input_len, "seconds": time.perf_counter() - start,
            "peak_allocated_bytes": torch.cuda.max_memory_allocated(), "peak_reserved_bytes": torch.cuda.max_memory_reserved(),
            "completed_utc": utc(), "human_listened": False})
        results.append(result)
        persist(f"output-{candidate['id']}.json", result)
        persist("inference-results.json", {"provenance": provenance, "results": results})
        print(json.dumps({"id": candidate["id"], "seconds": result["seconds"], "output": text}, ensure_ascii=False), flush=True)
        del generated, inputs, raw, waveform, decoded
        gc.collect()
        torch.cuda.empty_cache()
    del model, processor
    gc.collect()
    torch.cuda.empty_cache()
    persist("inference-status.json", {"state": "completed", "completed": len(results), "utc": utc(), "gpu_memory_released": True})
    print("INFERENCE_COMPLETE GPU_RELEASED", flush=True)

if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        persist("inference-status.json", {"state": "failed", "error": repr(error), "traceback": traceback.format_exc(), "utc": utc()})
        raise
