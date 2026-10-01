# Pass 2 visual review

Scope: CPU renders of the delivered PNGs, not Unity or owner acceptance.
The four theme sheets were loaded with the vision tool, followed by readable
lit-column crops covering every material and distance previews for the main
construction surfaces. Columns: albedo/normal/smoothness tiled 3x3, dim warm,
dim cold. Distance rows: warm/cold at 2 m, then warm/cold at 4 m.

## Evidence paths

All paths below are relative to this folder:

- Castle-contact.png
- Hospital-contact.png
- School-contact.png
- Basement-contact.png
- materials/<exact_slot>.png (individual sheet row)
- distance/<exact_slot>.png (fixed 60-degree FOV, 384 px viewport)
- generation.json (delivered PNG hashes)
- distance-metrics.json (normal relief after linear filtering)

## Castle

| Material | Observed result |
|---|---|
| castle_stone | Staggered ashlar blocks, recessed dark mortar and chipped bevel highlights; coarse surface remains readable in both 4 m views. |
| castle_stone_dark | Same construction at darker values; joints and silhouette of each stone remain clearer than interior pitting. |
| castle_wood | Uneven brown boards with coarse wandering grain, staggered end joints and paired nail holes; cold light suppresses brown but preserves grain. Nails weaken at 4 m. Grain is deliberately stylised and somewhat repetitive. |
| castle_metal | Orange-brown rust islands over almost-black iron, fine pits and descending corrosion marks; readable at 4 m mainly by rust colour, not bare-metal sheen. |
| castle_mortar | Matte granular grey with dark streaks; warm light makes it brown, cold makes it grey-blue. No false block pattern in this support slot. |
| castle_soot | Nearly black with a low dirty band and downward marks; intentionally low visibility, not a primary wall identification texture. |
| castle_ember | Amber mottled surface; the swatch is non-emissive and does not establish an actual glowing ember. |

## Hospital

| Material | Observed result |
|---|---|
| hospital_tile | Ivory glazed squares, dark dirty joints and small brown edge losses. Joint relief and tile boundaries survive 4 m; individual chips are principally near-view detail. |
| hospital_terrazzo | Dark, pale and ochre aggregate flecks in a warm grey matrix; speckled identity survives 4 m without gravel-like bumps. Light flecks are less prominent under the dim light than dark chips. |
| hospital_vinyl | Muted brown-grey squares with diagonal shoe-like scuffs and shallow joints. More subdued than ceramic; at 4 m the grid remains but small scuffs fade. |
| hospital_paint | Pale painted plaster with irregular exposed brown patches, raised peeling rims and fine damp trails. Peeling outlines catch both light colours. |
| hospital_curtain | Broad vertical cloth folds, cream/grey body and thin brown dirty trails; fold normals read at 4 m, woven detail does not. |
| hospital_linen | Similar folded pale cloth with a different stain arrangement; cooler and cleaner-looking than the curtain in the cold swatch. |
| hospital_acoustic | Pale perforated acoustic surface; small regular dark pinholes are visible near, merging into texture at distance. |
| hospital_door_enamel | Pale enamel with local orange corrosion, dark runs and small dent glints; cleaner paint remains between damaged areas. |
| hospital_rust | Dense orange/brown granular corrosion, pits and darker uneven areas; no falsely polished rust response. |
| hospital_stainless | Cool grey metal with muted tarnish and discrete dent glints; less corroded than iron, not mirror-like in these lights. |
| hospital_grout | Fine matte grey aggregate and dark streaks; designed as support material, not another tile grid. |
| hospital_rubber | Nearly black fine ribs; mostly a dark mass in the cold view, appropriate for trim rather than identifying the room. |
| hospital_glass | Blue-grey cloudy/streaked surface, with a high smoothness map. This opaque wall swatch cannot establish transparency or reflections. |
| hospital_light | Pale mottled diffuser surface; no emission was added or simulated. |

## School

| Material | Observed result |
|---|---|
| school_parquet | Actual interlocking herringbone, alternating warm board values, shallow seams and softer grain than rough timber. Pattern remains clear at 4 m under both lights; varnish variation is subtler than plank boundaries. |
| school_mustard | Mustard plaster with peeled patches and a horizontal dark dado division; cold light shifts it olive. Peeling lips visibly shade. |
| school_cream | Cream counterpart with exposed plaster and the same dado logic, cooler grey under cold light. |
| school_chalk_green | Deep green board with pale diagonal/overlapping erased sweeps and thin chalk traces. Dusty marks remain legible at 4 m without readable text. |
| school_teal | Teal paint with broken orange corrosion, descending runs and localized dent highlights. It reads as worn painted metal; locker seams/vents are the mesh's job. |
| school_fire_red | Dark red painted metal with brown damaged patches and small dent glints; colder lighting strongly darkens the red. |
| school_wood | Brown boarded timber with pronounced grain and small nails; joint pattern remains visible in cold light. |
| school_door_laminate | Yellow-brown wood-pattern surface; it currently shares the boarded/nail recipe, so is more rustic than a single smooth laminate slab. |
| school_lino | Brown tiled resilient floor with short diagonal scuffs; lower contrast than parquet. |
| school_steel | Dark tarnished grey metal with scattered shallow-looking dent highlights. |
| school_mortar_upper | Pale granular grey support material with thin damp trails. |
| school_mortar_lower | Dark teal-grey support material; streaks are present but quieter in cold light. |
| school_scuff | Brown-grey grime with a dark foot band and long thin marks. |
| school_stain | Lighter grey-brown grime variation with the same low-band logic. |
| school_rubber | Very dark fine ribs; not over-amplified merely to colour the normal column. |
| school_glass | Blue-grey smeared glass colour and smooth finish; reflection/transmission still need Unity context. |
| school_paper | Warm off-white fibrous/mottled paper; no unrelated masonry or corrosion patterns. |
| school_tube | Light cream mottled diffuser colour, no simulated emission. |
| school_dead_tube | Darker grey aged diffuser colour, remains visibly duller than the live tube slot. |

## Basement

| Material | Observed result |
|---|---|
| basement_concrete | Horizontal board-form joints, small tie holes, irregular grain impressions and vertical water marks. Final grain is broken and much shallower than the rejected corrugated-looking iteration. Joints and damp marks survive 4 m. |
| basement_brick | Staggered rusty-brown bricks with dark soot variation, recessed joints and pale irregular salt deposits. Cold lighting reveals the grey efflorescence more clearly; courses stay legible at 4 m. |
| basement_rust | Rough orange/brown corrosion with granular pits and darker areas, matte rather than uniformly shiny. |
| basement_steel | Almost-black iron with substantial orange rust patches and descending runs; bare areas are difficult to read without reflection context. |
| basement_door_steel | Separate rust arrangement on the same dark iron recipe, with pitting and sparse dent detail. |
| basement_galvanised | Tarnished grey metal, broad subdued variations and small dent glints. A distinctive zinc-spangle pattern is not authored. |
| basement_damp | Dark grey, thin descending wet marks and an obvious low grime band; wet streaks are distinct in the smoothness column. |
| basement_insulation | Beige/grey folded fabric-like wrap with fine weave and dark stains. |
| basement_hazard | Diagonal yellow/black stripes broken by rust and wear, readable under warm and cold light. |
| basement_sodium | Orange mottled lamp surface; emission remains owned by the existing material/light setup. |
| basement_water | Very dark smooth surface with low ripple relief; almost black in the wall preview because there is no reflected environment. |

## Iteration and limits

- The first pass-2 seam run found a genuine herringbone colour discontinuity.
  Board IDs repeated over 16 grid cells while the UV translation was 8; IDs
  now share the geometry's period. Seam limits were not relaxed.
- Added grain large enough to shade at distance, then reduced parquet's grain
  to distinguish worn varnish from rough structural timber.
- Reduced blanket rust coverage on painted metal and increased dent relief.
- Reduced concrete's original regular high-frequency ridges and increased
  visible brick efflorescence after inspecting the dim-light comparisons.
- Periodic motifs are apparent over 3x3 repeats; this pass does not add stochastic
  runtime tiling. No artificial border seams were apparent in inspected final
  crops. Pixel tests separately validate all saved maps.
- Dado and foot grime repeat every two vertical metres. Upright, consistently
  phased kit UVs are necessary; true floor-relative dirt would require a separate
  world-space shader/decal system outside this task.
- Some wet streaks look narrow/scratch-like. Real Unity reflections, compression,
  scale/orientation across separate meshes and owner quality judgement remain
  acceptance checks. The previews establish material detail, not a gameplay pass.
