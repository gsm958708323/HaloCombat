# Stickman art sources

## Imported source

- Source: [Stick Figure Character Sprites 2D](https://opengameart.org/content/animated-stick-figure-character-2d-free-cc0)
- Local folder: `Assets/Art/Stickman/Source/OpenGameArt_CC0/Stick Figure Character Sprites 2D`
- License: CC0. The author states that commercial use is allowed and attribution is not required.
- Contents: fighter, sword and pistol animation frames, hit effects, and a few environment props.

The original archive and its `License.txt` are kept in the source folder. Do not remove that file when reorganising the art.

## Other candidates reviewed

- [Stickman Pack](https://octopyte.itch.io/stickman-pack): commercial and non-commercial use is allowed; attribution is appreciated. Includes idle, run, jump, punch and death sprites.
- [2D Modular Stickman Animation Pack](https://stableimage.itch.io/2d-modular-stickman-animation-pack-50-animation): commercial and personal use is allowed; attribution is optional; redistribution or reselling the source pack is not allowed.
- [Stick man runner](https://opengameart.org/content/stick-man-runner): CC0, includes a 12-frame run animation.
- [Stickman skateboarder](https://opengameart.org/content/stickman-skateboarder): CC0, 2D side-scrolling animation.

## Intended use in HaloCombat

Use the imported CC0 sprites as the base player/enemy animation set. Enemy identity should come from palette, equipment, silhouette, and behaviour rather than duplicating a separate art pack for every enemy. Keep the source sprites unchanged and put generated variants under `Assets/Art/Stickman/Generated`.

## Generating enemies in Unity

1. Open the project in Unity 6.
2. Run `HaloCombat/Stickman/Configure CC0 Sprite Import` once.
3. Run `HaloCombat/Stickman/Generate Enemy Palette Sprites`.
4. Run `HaloCombat/Stickman/Generate Sprite Animator Views`.

The editor tool creates Crimson, Toxic, Void, and Gold variants for the fighter, sword, and pistol frames. Existing generated files are skipped so the tool does not overwrite manual edits. A small idle preview is already in `Assets/Art/Stickman/Generated/Preview`.

The Sprite Animator step creates views for `fighter`, `melee_ai`, `melee_guard`, and `ranged_ai`, writes the view table to `Assets/Resources/Stickman/StickmanViews.asset`, and the `Arena` and `Boot` scenes reference that table directly. A missing prefab produces no character view and an explicit scene/configuration error; there is no procedural character fallback.
