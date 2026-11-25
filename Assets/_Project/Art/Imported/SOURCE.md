# Imported art

Everything under `Cars/` comes from
[github.com/codingedger/car-game](https://github.com/codingedger/car-game),
pinned to commit `4a9dd6200b956ce76a3295c43a8c78e39a37b8fa`. That repository is
GPL-3.0, and its own README says the car models do not belong to its author, so
these files carry the same uncertainty. This project uses them only for a
non-commercial demo; they are not covered by the project's own licence.

The sky panorama (`Art/Sky/GoldenHour.png`, downscaled to 4096×2048) and the
Road and Grass surfaces (`Art/Textures/`) come from the same repository's
AllSkyFree sky set and its ground textures.

## What was taken

- `Cars/<Car>/<Car>.fbx` — the three bodies (Ferrari Testarossa, Nissan R35,
  Mercedes C63). The kart and the remaining source models are not used.
- `Cars/<Car>/Materials/` — the source materials, because the FBX names its
  materials but leaves most slots empty. Glass alpha, tyre rubber, chrome, and
  lamp textures live here.
- `Cars/R35/Textures/` — the R35's texture set with its original `.meta` files,
  so the material references keep resolving.
- `Cars/<Car>/PartMaterials.json` — the source prefabs' renderer-to-material
  assignment, extracted as data because the prefabs themselves depend on
  scripts and GUIDs this project does not import.

## Known gaps

- The C63 and Ferrari folders contain no textures, and two R35 material
  references (`vehiclelights128`'s base and emission map, and a normal map on
  the tyre and cabin materials) point at textures that are not in the source
  repository. Those parts render with flat colours.
- The C63's FBX has `headlight_l` and `plate` objects that its prefab no longer
  names (it calls them `lamp` and has no plate), so those two parts keep the
  FBX's empty slots and do not render.
- GTA-format `.dff`/`.txd` collision models, the source prefabs, their scripts,
  and their audio clips were not taken.
