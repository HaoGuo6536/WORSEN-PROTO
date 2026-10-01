# ============================================================================
# inspect_kit_inputs.py
# PURPOSE:
#   Inspect the actual exported kit FBXs without editing their owners' files.
#   This catches missing world-scale UVs and texture slot coverage mismatches
#   before claiming that material assignment alone will produce usable rooms.
# ARCHITECTURAL ROLE: Offline art validation; outside Unity runtime layers.
# KEY RESPONSIBILITIES:
#   - Import manifest-listed FBXs into a disposable in-memory Blender scene.
#   - Record material slot coverage and UV availability in validation evidence.
# DEPENDENCIES: Blender 5.2 bpy and Python standard library.
# USAGE NOTES: Blender --background --factory-startup --python-exit-code 1
#   --python tools/textures/inspect_kit_inputs.py. Never saves a blend or FBX.
# ============================================================================
import json
from pathlib import Path

import bpy

ROOT = Path(__file__).resolve().parents[2]


def main():
    if bpy.app.version[:2] != (5, 2):
        raise RuntimeError('Use Blender 5.2')
    report = {}
    for theme in ('Castle', 'Hospital', 'School', 'Basement'):
        folder = ROOT / 'Assets/Art/Environment' / theme / 'Kit'
        manifest = json.loads((folder / f'{theme}Kit.manifest.json').read_text())
        rows = []
        for piece in manifest['pieces']:
            bpy.ops.wm.read_factory_settings(use_empty=True)
            bpy.ops.import_scene.fbx(filepath=str(folder / piece['file']))
            meshes = [o.data for o in bpy.context.scene.objects if o.type == 'MESH']
            slots = sorted({m.name for mesh in meshes for m in mesh.materials if m})
            missing = [s for s in slots if not (ROOT / 'Assets/Art/Textures' / theme / f'{s}_Albedo.png').exists()]
            uv_layers = sorted({uv.name for mesh in meshes for uv in mesh.uv_layers})
            has_surface_uv = bool(meshes) and all('SurfaceMetres' in mesh.uv_layers for mesh in meshes)
            rows.append({'file': piece['file'], 'slots': slots, 'missingTextures': missing,
                         'uvLayers': uv_layers, 'hasSurfaceMetres': has_surface_uv})
        report[theme] = rows
        print(f'KIT_INPUT {theme}: pieces={len(rows)} missingTextureSlots={sum(len(r["missingTextures"]) for r in rows)} '
              f'missingSurfaceMetres={sum(not r["hasSurfaceMetres"] for r in rows)}', flush=True)
    output = ROOT / 'Logs/AgentValidation/Art/Textures/kit-inputs.json'
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8', newline='\n')


if __name__ == '__main__':
    main()
