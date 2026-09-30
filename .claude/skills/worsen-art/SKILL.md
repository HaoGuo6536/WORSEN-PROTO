---
name: worsen-art
description: Create or change WORSEN 3D models with Blender 5.2 headless - ArtSource layout, FBX export axes, Unity import settings and the environment-kit contract. Use for any .blend, FBX, texture or kit work.
---

# 3D art for WORSEN


Keep the editable Blender file for every project-made 3D model in [ArtSource/](../../../ArtSource/README.md). This root-level folder sits outside `Assets/`, beside `PLANNING/`, `DOCUMENTATION/` and `tools/`. Its README owns the naming rules, the source inventory and the step-by-step workflow; read it before creating or changing a model.

- Mirror the export folder: `ArtSource/<Area>/<Asset>/<AssetName>.blend` exports to `Assets/Art/<Area>/<Asset>/<AssetName>.fbx`. Name the file after the asset it exports, never with a project-wide or generic name. For example, the former root `WORSEN.blend` is now `ArtSource/Horror/Cake/WORSEN_CakePickup.blend`.
- Never save a `.blend` into `Assets/`, the repository root, or only the ignored `Logs/` folder. Unity would import a `.blend` in `Assets/` through whichever Blender installation is associated with it, and runtime wiring uses the exported FBX. Blender's `.blend1` backups stay beside their source and are ignored by Git. Pre-change evidence backups belong under `Logs/`.
- Pack images into the `.blend`, or keep texture sources beside it with relative paths. `.blend` and `.fbx` files are stored with Git Large File Storage (LFS).
- Use Blender 5.2 (`C:/Program Files/Blender Foundation/Blender 5.2/blender.exe`), headless with `--background --factory-startup --python-exit-code 1`. Blender 4.5 is also installed but is not the project version. Prefer reproducible generator scripts under [tools/blender/](../../../tools/blender/README.md). In a connected Blender MCP session, check `bpy.data.filepath` before saving.
- Writing FBX files or textures into `Assets/` is a protected Unity write: hold the Unity lease, let Unity generate `.meta` files, and wire importers and materials through a deterministic §10 setup tool. Put previews and validation evidence under `Logs/AgentValidation/Art/<Asset>/`. Add each new source to the ArtSource inventory.


## Unity import and axes

- Export FBX with -Z forward, Y up and apply transform (`bake_space_transform=True`). Unity importers for these files must keep `bakeAxisConversion = false`; baking again rotates models 180 degrees, which caused the "raptor arms" first-person bug.
- Generators live in `tools/blender/` with a validator beside each one (see the blocky character generator and the environment kit generators). Previews and validation evidence go to `Logs/AgentValidation/Art/<Asset>/`.
- The environment kit contract (2 m module, per-theme wall heights, 3.2 m doors, piece ids, manifest format) is recorded in the kit generators' docstrings and `tools/blender/README.md`.
- Project-made art (our generators' output and `ArtSource/`) may be committed. Store-bought or downloaded models are vendor content (see [VENDOR.md](../../../VENDOR.md)) and are never committed; a download needs the owner's approval.
