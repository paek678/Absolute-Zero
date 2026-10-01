# PLAN_038 - Generated texture prompts

Tool: built-in `image_gen`, opaque background. Output textures are stored under `Assets/Art/Environments/NightForest/Textures/`.

## NightForestPanorama.png

```text
Create a production game environment texture: ONE seamless horizontally wrapping panoramic NIGHT FOREST BACKDROP, extremely wide landscape 4:1 panorama if possible (at least 3:1). This image will wrap around four vertical distant walls surrounding a small 2.5D game arena, not a complete scene screenshot. Camera level, straight horizon, no perspective floor. Calm Korean summer woodland at midnight with tall slender deciduous trees, distant layered forest hills, softly glowing blue mist between trunks, tiny restrained fireflies. Deep desaturated navy and teal palette with painterly realistic textured foliage; clean large silhouettes readable behind cute outlined sprite characters. Top 35 percent mostly open indigo night sky with very sparse stars, wisps of thin cloud, NO moon disc so repetition doesn't create multiple moons; silver blue moonlight softly catches treetops from above. Middle 45 percent layered trees with ample pale blue atmosphere in gaps, bottom 20 percent darker continuous distant bushes and grasses with almost level base. Gentle luminous blue-gray mist band behind lower tree trunks makes silhouettes readable. Low contrast background, not black, no bright hotspot. Left and right edges MUST match in lighting, horizon and vegetation density for seamless wrap. Flat full-bleed texture, opaque background. NO characters, NO people, NO text, NO labels, NO borders, NO UI, NO weapons, NO buildings, NO furniture, NO platforms, NO ground tiles, NO framing foreground tree trunks. Consistent hand-painted digital illustration suited for a game with cartoon sprite characters.
```

The generator returned 2172 x 724 (3:1). Final Unity assembly uses the full image on each wall with alternating mirrored U coordinates to join matching edges; the texture does not represent four distinct compass directions.

## NightDeckAlbedo.png

```text
Production game diffuse ALBEDO texture, seamless square tile, top-down orthographic closeup of a traditional Korean wooden pavilion deck. Weathered broad wooden planks, elegant restrained woodgrain, muted neutral gray-brown timber, subtle natural fine scratches, consistent plank widths and tidy thin seams. Surface fills whole image edge to edge. Perfectly flat uniform diffuse lighting so Unity will add night lighting later, NO cast shadows, NO directional highlights, NO perspective, NO borders, NO objects, NO scenery, NO labels, NO text. Natural realistic painted surface, subtle variation, not photoreal photographic noise. Seamless repeating both axes, offset staggered join pattern. Ground texture for a small cute 2.5D strategy game. Square 1024x1024 or higher.
```

The generator returned 1254 x 1254. The shared source has neutral illumination; the new Unity material and scene lights supply the night tint.
