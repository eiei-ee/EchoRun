# Orange Echo authored art

Historical source kit for the retained roads, head/hand fragments and material
bindings. The active art direction and outfit are Layered Memory / Memory Courier;
see `ArtSource/Blender/LayeredMemory/MemoryCourier.md`. Orange names are retained
for asset and runtime binding compatibility.

`Tools/Blender/build_orange_echo.py` authors the outfit and three road modules
offline in Blender 5.2.1. Editable `.blend` sources are retained here; exported
FBXs are in `Assets/Art/OrangeEcho/Models`. No Blender mesh generation runs in
the player. See `asset-statistics.json` for generated garment and road counts.

The outfit adds new garment geometry to the existing Mixamo skeleton. Original
head and hand fragments are retained from Exo Gray. These fragments and the
armature retain the Adobe Mixamo provenance documented in
`THIRD_PARTY_NOTICES.md`; the combined outfit FBX is not wholly original MIT
content. The new road meshes and garment construction script are project art.

Unity installation: `OrangeEchoArtInstaller.Install` preserves the scene's
original Avatar, Animator controller, player controller and collider. It assigns
the new meshes to the existing bone transforms, hides the old full-body
renderers, and saves material/mesh assets through the Unity APIs. Re-running
updates existing mesh assets without changing their GUIDs.

Road art is instantiated once per existing pooled segment by
`OrangeEchoRoadVisuals`. The art has no colliders. It replaces only visible
surfaces and rail renderers. Straight geometry spans x=[-5.5,5.5], z=[-10,10].
Turns enter at z=0, bend at z=10, and exit at x=+/-10, matching the existing
route contract. Blender's X axis is compensated in the exported road geometry.

The baseline scene snapshot, installation logs, regression results and real
player captures are kept in `TestResults/OrangeEcho-20260917`. Static editor
clip samples are not evidence of real running/jumping/sliding; the optional
`-echo-orange-art-review` flag extends the existing isolated development-player
visual diagnostic with queued controller inputs and labelled frame captures.

Current work is a Windows visual iteration. WeChat export, device performance
and human acceptance must be recorded separately; no such claim follows from
a Windows build or asset statistics.

## Current outfit

`Tools/Blender/build_orange_echo.py` and `OrangeEchoArtInstaller.Install` rebuild
the historical base kit, including its original wider outfit. After rebuilding
that base, run `LayeredMemoryPalette.Install` and
`LayeredMemoryRunnerInstaller.Install` to restore the current palette and courier.
Do not use the old installer as the final character installation step.

Superseded slim candidates and their local review captures are not runtime
dependencies and are not part of this delivery. The current character source,
reproduction steps and validation limits are documented in
`ArtSource/Blender/LayeredMemory/MemoryCourier.md` and
`docs/2026-09-24-memory-courier-form.md`.
