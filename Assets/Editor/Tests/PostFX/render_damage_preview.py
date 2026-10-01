"""CPU design preview of the configured URP damage vignette, NOT a Unity screenshot.

Reads production config defaults. Transcribes URP ApplyVignette and parameter
scaling (intensity * 3, smoothness * 5, non-rounded UVs). The generated grid is
only a neutral contrast target; it is not game content or native rendering proof.
Run from the worktree root; output stays in ignored local verification evidence.
"""
from pathlib import Path
import math
import re
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[4]
source = (ROOT / "Assets/Scripts/Presentation/PostFX/Config/PostFXDriverConfig.cs").read_text()


def value(name):
    return float(re.search(rf"{name}\s*=\s*([0-9.]+)f", source).group(1))


peak = value("_damageVignettePeak")
full = value("_damageFullStrengthHealthFraction")
fade = value("_damageFadeSeconds")
smoothness = value("_damageVignetteSmoothness")
threshold = value("_lowHealthFraction")
low_max = value("_lowHealthVignette")
pulse = value("_lowHealthPulseMultiplier")
tint = tuple(float(x.rstrip("f")) for x in re.search(
    r"_damageVignetteColor\s*=\s*new Color\(([^)]+)\)", source).group(1).split(",")[:3])


def strength(health, damage, seconds, heartbeat=0):
    hit = min(1, damage / full) * peak * max(0, 1 - seconds / fade)
    low = math.sqrt(max(0, min(1, (threshold - health) / threshold))) * low_max * (1 + pulse * heartbeat)
    return max(hit, low) if health < 1 else 0


cases = [
    ("Full health / no damage vignette", 1, 0, 0, 0),
    ("Echo 25 hit / t=0", .75, .25, 0, 0),
    (f"Echo 25 hit / t={fade / 2:g}s", .75, .25, fade / 2, 0),
    (f"Echo hit cleared / t={fade:g}s", .75, .25, fade, 0),
    ("25 health / after fade / between beats", .25, .75, 10, 0),
    ("25 health / injected heartbeat peak", .25, .75, 10, 1),
]
w, h, label = 480, 270, 34
sheet = Image.new("RGB", (w * 3, (h + label) * 2 + 35), (18, 18, 18))
draw = ImageDraw.Draw(sheet)
draw.text((10, 10), "CPU URP formula preview on generated neutral grid - NOT Unity / not heartbeat routing proof", fill="white")
for index, (title, health, damage, seconds, heartbeat) in enumerate(cases):
    intensity = strength(health, damage, seconds, heartbeat)
    tile = Image.new("RGB", (w, h))
    pixels = tile.load()
    for y in range(h):
        for x in range(w):
            # Synthetic neutral test pattern, deliberately identical in every panel.
            base = .45 if ((x // 48) + (y // 45)) % 2 else .65
            if x % 48 == 0 or y % 45 == 0:
                base = .8
            dx, dy = (x / (w - 1) - .5) * intensity * 3, (y / (h - 1) - .5) * intensity * 3
            vf = max(0, min(1, 1 - dx * dx - dy * dy)) ** (smoothness * 5)
            pixels[x, y] = tuple(round(255 * base * (c + (1 - c) * vf)) for c in tint)
    ox, oy = (index % 3) * w, (index // 3) * (h + label) + 35
    sheet.paste(tile, (ox, oy))
    draw.text((ox + 6, oy + h + 4), f"{title}   intensity={intensity:.5f}", fill="white")
    print(f"{title}: intensity={intensity:.6f}")
output = ROOT / "Logs/AgentValidation/hit-feedback/damage-preview.png"
output.parent.mkdir(parents=True, exist_ok=True)
sheet.save(output)
print(output)
