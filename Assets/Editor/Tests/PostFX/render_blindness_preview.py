"""CPU contrast preview from real headless PostFXPresenter output, NOT Unity.

Consumes BLIND_PREVIEW lines in the NUnit XML from PostFXBlindnessFeedbackTests.
The target is a generated lamp/large-shape chart, not game content. Transcribes
camcorder corner and URP damage-vignette formulas; the sRGB color filter is converted
to linear like URP's ColorGradingLutPass, without simulating the rest of the LUT.
Gaussian blur is a CPU proxy, not URP depth of field.
No grain, tape, tonemapping, scene lighting or native renderer proof is implied.
Run from the worktree root with --pure-run <successful pure evidence directory>.
"""
import argparse
import math
from pathlib import Path
import re
import xml.etree.ElementTree as ET
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[4]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--pure-run", required=True)
args = parser.parse_args()
run = ROOT / "Logs/AgentValidation/PLAN-002/offline-compile" / args.pure_run
samples = {}
# Parse only this checkout's locally generated NUnit evidence, never downloaded XML.
for xml_path in run.glob("*.nunit.xml"):
    tree = ET.parse(xml_path)
    for case in tree.iter("test-case"):
        if "PreviewSamplesExportProductionParameters" not in case.get("fullname", ""):
            continue
        if case.get("result") != "Passed":
            raise RuntimeError("Production sample test did not pass")
        for line in (case.findtext("output") or "").splitlines():
            if not line.startswith("BLIND_PREVIEW "):
                continue
            parts = line.split()
            samples[parts[1]] = dict(item.split("=", 1) for item in parts[2:])
labels = ["baseline", "onset", "blinded", "blinded_damage", "mid_recovery", "recovered"]
if set(samples) != set(labels):
    raise RuntimeError(f"Missing or unexpected production samples: {list(samples)}")
config = (ROOT / "Assets/Scripts/Presentation/PostFX/Config/PostFXDriverConfig.cs").read_text()
red = tuple(float(x.strip().rstrip("f")) for x in re.search(
    r"_damageVignetteColor\s*=\s*new Color\(([^)]+)\)", config).group(1).split(",")[:3])


def smooth(a, b, value):
    t = max(0, min(1, (value - a) / (b - a)))
    return t * t * (3 - 2 * t)


def linear(value):
    value /= 255
    return value / 12.92 if value <= .04045 else ((value + .055) / 1.055) ** 2.4


def srgb(value):
    return round(255 * (12.92 * value if value <= .0031308 else 1.055 * value ** (1 / 2.4) - .055))


w, h = 480, 270
base = Image.new("RGB", (w, h), (40, 44, 48))
draw = ImageDraw.Draw(base)
# Deliberate contrast target: large blocks, an opening, a floor and a lit lamp.
draw.polygon([(0, 0), (170, 72), (170, 190), (0, h)], fill=(90, 94, 100))
draw.polygon([(w, 0), (310, 72), (310, 190), (w, h)], fill=(78, 82, 88))
draw.polygon([(0, h), (170, 190), (310, 190), (w, h)], fill=(66, 70, 74))
draw.rectangle((216, 96, 272, 190), fill=(22, 24, 27), outline=(108, 112, 115), width=4)
draw.rectangle((42, 104, 102, 237), fill=(118, 120, 124))
draw.rectangle((366, 91, 417, 220), fill=(108, 111, 114))
draw.ellipse((294, 50, 344, 100), fill=(235, 195, 113))
draw.ellipse((307, 62, 331, 86), fill=(255, 242, 206))
sheet = Image.new("RGB", (w * 3, (h + 42) * 2 + 50), (18, 18, 18))
draw = ImageDraw.Draw(sheet)
draw.text((8, 5), "CPU PROXY - production presenter samples / generated contrast chart - NOT a Unity screenshot", fill="white")
draw.text((8, 24), "sRGB filter -> linear transmission; proxy Gaussian blur; no scene lighting / tonemapping / GPU proof", fill="white")
for index, label in enumerate(labels):
    sample = samples[label]
    tint = float(sample["tint"])
    # URP passes ColorAdjustments.colorFilter.value.linear into its grading LUT.
    transmission = linear(tint * 255)
    strength, radius, softness, edge_blur = map(float, sample["lens"].split(","))
    damage, damage_soft = float(sample["damage"]), float(sample["smoothness"])
    edge_start = float(sample["edge"])
    # Whole-frame CPU blur approximates the output radius only, never native CoC.
    source = base.filter(ImageFilter.GaussianBlur(max(0, float(sample["blur"]) - .5)))
    edge_source = source.filter(ImageFilter.GaussianBlur(edge_blur))
    tile = Image.new("RGB", (w, h))
    src, blurred, output = source.load(), edge_source.load(), tile.load()
    for y in range(h):
        for x in range(w):
            u, v = x / (w - 1), y / (h - 1)
            px, py = abs(u * 2 - 1), abs(v * 2 - 1)
            qx, qy = px - (1 - radius), py - (1 - radius)
            distance = math.hypot(max(qx, 0), max(qy, 0)) + min(max(qx, qy), 0) - radius
            corner = smooth(-max(.01, softness), 0, distance)
            edge = smooth(edge_start, 1, max(px, py))
            dx, dy = (u - .5) * damage * 3, (v - .5) * damage * 3
            vf = max(0, min(1, 1 - dx * dx - dy * dy)) ** (damage_soft * 5)
            channels = []
            for channel in range(3):
                value = linear(src[x, y][channel]) * (1 - edge) + linear(blurred[x, y][channel]) * edge
                value *= transmission * (red[channel] + (1 - red[channel]) * vf) * (1 - corner * strength)
                channels.append(srgb(value))
            output[x, y] = tuple(channels)
    ox, oy = (index % 3) * w, (index // 3) * (h + 42) + 50
    sheet.paste(tile, (ox, oy))
    draw.text((ox + 6, oy + h + 4), f"{label}  tint={tint:.3f}  dark border={strength:.3f}", fill="white")
    draw.text((ox + 6, oy + h + 20), f"damage={damage:.3f}  blur radius={float(sample['blur']):.3f}", fill="white")
    print(f"{label}: {sample}; lamp={output[319, 74]} shape={output[70, 160]}")
path = ROOT / "Logs/AgentValidation/hit-feedback/blindness-preview.png"
path.parent.mkdir(parents=True, exist_ok=True)
sheet.save(path)
print(path)
