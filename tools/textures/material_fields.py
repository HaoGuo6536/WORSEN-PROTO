# ============================================================================
# material_fields.py
# PURPOSE:
#   Author recognisable construction and wear rather than colour-only noise.
#   Every feature is periodic over the two-metre repeat, and the same masks
#   drive colour, physical height and finish so relief follows visible damage.
# ARCHITECTURAL ROLE: Offline art Utility; outside Unity runtime layers; Textures.
# KEY RESPONSIBILITIES:
#   - Build masonry, timber, institutional finishes and industrial wear fields.
#   - Couple physical height and finish to construction and damage masks.
#   - Keep feature placement deterministic using the caller's seeded generator.
# DEPENDENCIES: NumPy only; generator supplies periodic noise and fleck fields.
# USAGE NOTES:
#   Heights returned in metres, colours in linear RGB. No I/O or engine calls.
#   Constants below are provisional art choices, not runtime configuration.
# ============================================================================
import numpy as np

TAU = 2 * np.pi


def ramp(low, high, value):
    t = np.clip((value - low) / (high - low), 0, 1)
    return t * t * (3 - 2 * t)


def blend(color, other, mask):
    return color * (1 - mask[..., None]) + np.asarray(other) * mask[..., None]


def edge(v):
    return np.minimum(v % 1, 1 - v % 1)


def stamps(size, rng, count, widths, lengths, angle=0):
    """Wrapped, feathered elliptical marks; useful for dents, scuffs and drips."""
    out = np.zeros((size, size))
    for _ in range(count):
        cx, cy = rng.integers(0, size, 2)
        a = rng.uniform(*widths) * size
        b = rng.uniform(*lengths) * size
        theta = angle + rng.uniform(-.15, .15)
        radius = int(np.ceil(max(a, b))) + 1
        yy, xx = np.mgrid[-radius:radius + 1, -radius:radius + 1]
        u = (xx * np.cos(theta) + yy * np.sin(theta)) / a
        v = (-xx * np.sin(theta) + yy * np.cos(theta)) / b
        weight = np.maximum(0, 1 - u * u - v * v) ** 2
        iy, ix = (cy + yy) % size, (cx + xx) % size
        np.maximum.at(out, (iy, ix), weight * rng.uniform(.5, 1))
    return out


def masonry(x, y, broad, fine, rng, rows, columns, width):
    """Running bond with irregular chipped bevels and stable per-block colour."""
    row = np.floor(y * rows).astype(int)
    u = x * columns + (row % 2) * .5
    col = np.floor(u).astype(int) % columns
    # Face distance is physical fraction of the whole image (not cell aspect).
    distance = np.minimum(edge(u) / columns, edge(y * rows) / rows)
    broken = .003 * ramp(-.2, .6, fine + broad)
    face = ramp(width, width + .007 + broken, distance)
    colors = rng.uniform(.65, 1.5, (rows, columns))
    variation = colors[row % rows, col]
    return face, distance, variation


def timber(x, y, medium, fine, rng, parquet):
    if parquet:
        # Exact interlocking 4:1 herringbone tessellation, rotated 45 degrees.
        a, b = (x + y) * 8, (y - x) * 8
        ia, ib = np.floor(a), np.floor(b)
        horizontal = (ia - ib) % 8 < 4
        along = np.where(horizontal, (a - ib) % 8, (b - ia - 1) % 8)
        across = np.where(horizontal, b % 1, a % 1)
        # Translation by one texture shifts a,b by +/-8: IDs must share that
        # period, not 16, or geometry tiles but the plank colours do not.
        id_a = np.where(horizontal, ia - (ia - ib) % 8, ia).astype(int) % 8
        id_b = np.where(horizontal, ib, ib - (ib - ia - 1) % 8).astype(int) % 8
        distance = np.minimum(np.minimum(along, 4 - along), edge(across)) / (8 * np.sqrt(2))
        face = ramp(.0007, .0035, distance)
    else:
        across = (x * 6) % 1
        along = (y * 2 + (np.floor(x * 6) % 2) * .5) % 1
        id_a, id_b = np.floor(x * 6).astype(int), np.floor(y * 2).astype(int)
        face = ramp(.0015, .005, np.minimum(edge(x * 6) / 6, edge(along) / 2))
    variations = rng.uniform(.55, 1.65, (16, 16))[id_b, id_a]
    knot = np.sin(TAU * along) * np.exp(-((across - .52) / .18) ** 2)
    phase = across * 8 + .65 * np.sin(TAU * along) + .8 * knot
    grain = np.sin(TAU * phase)
    pores = ramp(.65, .99, np.sin(TAU * (phase * 3 + .2 * fine)))
    grain_contrast = .18 if parquet else .34
    value = variations * (.96 + grain_contrast * grain - .23 * pores + .12 * medium)
    nails = np.zeros_like(x)
    if not parquet:
        for u in (.13, .87):
            for v in (.035, .965):
                nails = np.maximum(nails, 1 - ramp(.35, 1, ((across - u) / .029) ** 2 + ((along - v) / .010) ** 2))
    grain_depth = .07 if parquet else .20
    height = face * (1 + grain_depth * grain - .09 * pores) - nails * .6
    return face, value, height, nails


def recipe_fields(theme, recipe, base, x, y, broad, medium, fine, micro, aggregate, rng, profile):
    gloss, relief, metal = profile
    size = x.shape[0]
    dirt = ramp(-.15, .55, broad + .2 * medium)
    color = base * (1 + .20 * medium + .07 * fine - .25 * dirt)[..., None]
    height = relief * (.12 * medium + .16 * fine + .018 * micro)
    smooth = gloss + .06 * medium - .10 * dirt
    metallic = np.full_like(x, metal)
    # Periodic rising-damp band: dirty foot at V=.15, clean central wall.
    foot = np.exp(-((np.sin(np.pi * (y - .85))) / .34) ** 2)
    drips = stamps(size, rng, 34, (.003, .014), (.055, .23))
    wet = ramp(.13, .65, drips) * (.55 + .45 * dirt)

    if recipe in ('stone', 'brick'):
        face, distance, variation = masonry(x, y, broad, fine, rng,
                                            6 if recipe == 'stone' else 10,
                                            3 if recipe == 'stone' else 5,
                                            .005 if recipe == 'stone' else .003)
        face_color = base * (variation * (.92 + .24 * medium + .12 * fine))[..., None]
        mortar = np.array([.022, .025, .022]) if recipe == 'stone' else np.array([.075, .07, .055])
        color = blend(np.broadcast_to(mortar, color.shape), face_color, face)
        height = relief * (face * (.86 + .12 * medium + .11 * fine) - .1 * aggregate)
        smooth = .08 + .08 * face - .06 * aggregate
        if recipe == 'stone':
            chips = ramp(.2, .65, medium + fine) * (1 - ramp(.015, .038, distance)) * face
            color *= (1 - .30 * chips)[..., None]
            height -= .009 * chips
        else:
            soot = np.clip(.18 + .60 * dirt + .40 * foot, 0, .85)
            color *= (1 - soot)[..., None]
            salt = ramp(-.08, .45, broad - .7 * medium) * (.25 + .75 * drips)
            color = blend(color, [.42, .40, .33], salt * .85)
        color *= (1 - .25 * wet)[..., None]
        smooth += .52 * wet
    elif recipe in ('wood', 'parquet'):
        face, value, grain_height, nails = timber(x, y, medium, fine, rng, recipe == 'parquet')
        color = blend(np.broadcast_to(base * .13, color.shape), base * value[..., None], face)
        color = blend(color, [.013, .010, .008], nails)
        height = relief * grain_height
        wear = stamps(size, rng, 42, (.003, .015), (.025, .14), angle=.65)
        color = blend(color, base * 1.8, wear * .35 * face)
        smooth = np.where(face > .5, gloss + .20 * (1 - dirt) - .27 * wear, .06)
        height -= .0006 * wear
    elif recipe in ('tile', 'vinyl', 'terrazzo'):
        distance = np.minimum(edge(x * 4), edge(y * 4)) / 4
        face = ramp(.0015, .0045, distance)
        if recipe == 'tile':
            # Original slot palette is retained in metadata for adoption; glaze
            # is aged ivory with a trace of the hospital mint, not solid green.
            glaze = .85 * np.array([.72, .70, .62]) + .15 * base
            variation = rng.uniform(.78, 1.12, (4, 4))[(y * 4).astype(int), (x * 4).astype(int)]
            chips = ramp(.05, .65, fine + .5 * medium) * (1 - ramp(.003, .014, distance)) * face
            color = blend(np.broadcast_to([.035, .032, .022], color.shape), glaze * variation[..., None], face)
            color = blend(color, [.13, .095, .055], chips)
            height = relief * face - .0025 * chips + .00008 * fine
            smooth = .68 * face - .53 * chips - .23 * wet
            color *= (1 - .35 * wet)[..., None]
        elif recipe == 'terrazzo':
            # Multi-colour angular aggregate stays flush: polished stone, not gravel.
            color = base * (1.1 + .1 * medium)[..., None]
            dark = ramp(.33, .52, aggregate) * (1 - ramp(.65, .78, aggregate))
            light = ramp(.75, .9, aggregate)
            ochre = ramp(.52, .65, aggregate) * (1 - ramp(.72, .8, aggregate))
            color = blend(color, [.035, .043, .039], dark)
            color = blend(color, [.68, .65, .53], light)
            color = blend(color, [.22, .13, .065], ochre)
            color *= (.65 + .35 * face)[..., None]
            height = .0012 * face + .00016 * aggregate
            smooth = .55 - .12 * aggregate - .15 * dirt
        else:
            scuffs = stamps(size, rng, 100, (.001, .004), (.012, .065), angle=.75)
            color *= (face * .25 + .75 - .50 * scuffs)[..., None]
            color += base * aggregate[..., None] * .18
            height = .001 * face - .00035 * scuffs + .00015 * fine
            smooth = .46 - .30 * scuffs - .13 * dirt
    elif recipe in ('plaster', 'concrete', 'mortar', 'grime', 'acoustic'):
        if recipe == 'plaster':
            peeled = ramp(.23, .35, medium + .65 * broad + .16 * fine)
            lip = ramp(.17, .24, medium + .65 * broad + .16 * fine) - peeled
            color = blend(color, [.16, .13, .085], peeled * .8)
            height = .0018 * (1 - peeled) + .0025 * lip + .0005 * fine
            smooth = .13 - .07 * peeled
            if theme == 'School':
                dado = ramp(.48, .49, y) * (1 - ramp(.965, .985, y))
                color *= (1 - .48 * dado)[..., None]
                line = ramp(.474, .477, y) * (1 - ramp(.489, .492, y))
                color *= (1 - .65 * line)[..., None]
                height += .0007 * line
        elif recipe == 'concrete':
            board = 1 - ramp(.001, .004, edge(y * 8) / 8)
            grain = np.sin(TAU * (y * 80 + 1.5 * medium + .5 * np.sin(TAU * x)))
            grain *= .4 + .6 * ramp(-.4, .4, medium)
            ties = (1 - ramp(.000015, .00006, (edge(x * 4) / 4) ** 2 + (edge(y * 4) / 4) ** 2))
            color *= (1 - .26 * board - .55 * ties + .06 * grain)[..., None]
            height += .0006 * grain - .005 * board - .006 * ties
            smooth = .16 + .5 * wet
        elif recipe == 'acoustic':
            perforations = 1 - ramp(.12, .23, np.sqrt(edge(x * 48) ** 2 + edge(y * 48) ** 2))
            color *= (1 - .72 * perforations)[..., None]
            height -= .003 * perforations
        elif recipe == 'grime':
            color *= (.8 - .5 * foot - .3 * wet)[..., None]
            height += .001 * drips
            smooth = .06 + .60 * wet
        color *= (1 - .42 * wet - .18 * foot)[..., None]
        height -= relief * .35 * aggregate
        if recipe == 'plaster':
            smooth += .35 * wet
    elif recipe in ('iron', 'steel', 'rust', 'painted_metal', 'hazard'):
        corrosion = ramp(-.06, .27, medium + .55 * broad + .14 * fine + .65 * drips)
        if recipe == 'steel':
            corrosion *= .25
        elif recipe == 'painted_metal':
            corrosion = ramp(.18, .43, medium + .4 * broad + .1 * fine + .8 * drips)
        elif recipe == 'rust':
            corrosion = .5 + .5 * corrosion
        pits = aggregate * corrosion
        rust_color = np.stack((.16 + .13 * fine, .040 + .035 * fine, .012 + .012 * fine), axis=-1)
        color = blend(color, rust_color, corrosion)
        brush = np.sin(TAU * (y * 210 + .14 * np.sin(TAU * x)))
        height += relief * (.3 * corrosion - .7 * pits) + .00012 * brush
        dents = stamps(size, rng, 16, (.012, .045), (.018, .075), angle=.45)
        scratches = stamps(size, rng, 70, (.0008, .002), (.013, .05), angle=.6)
        height -= .014 * dents + .0007 * scratches
        color *= (1 - .23 * scratches)[..., None]
        smooth = gloss + .12 * (1 - dirt) - .32 * corrosion + .15 * wet
        metallic *= 1 - .97 * corrosion
        if recipe in ('painted_metal', 'hazard'):
            exposed = ramp(.12, .6, corrosion * scratches * 4)
            color = blend(color, [.15, .17, .16], exposed)
            metallic = .025 + .7 * exposed
        if recipe == 'hazard':
            stripes = ((x + y) * 4) % 1 < .5
            color *= np.where(stripes, .07, 1)[..., None]
    elif recipe == 'fabric':
        folds = np.cos(TAU * (x * 10 + .10 * np.sin(TAU * y)))
        weave = np.sin(TAU * x * 180) * np.sin(TAU * y * 180)
        color *= (1 + .13 * folds + .07 * weave - .5 * wet - .25 * foot)[..., None]
        color = blend(color, [.13, .075, .028], wet * .38)
        height = .007 * folds + .00045 * weave
        smooth = .11 - .06 * dirt
    elif recipe == 'chalkboard':
        wiped = stamps(size, rng, 52, (.015, .035), (.05, .18), angle=1.2)
        strokes = stamps(size, rng, 34, (.001, .0025), (.012, .06), angle=.5)
        chalk = np.clip(wiped * .38 + strokes * .48, 0, .75)
        color = blend(color, [.42, .46, .37], chalk)
        height = .0003 * fine + .00025 * chalk
        smooth = .22 - .16 * chalk
    elif recipe in ('glass', 'water', 'light', 'rubber', 'paper'):
        height *= .15
        if recipe == 'water':
            height = .001 * np.sin(TAU * (x * 5 + .2 * np.sin(TAU * y * 2)))
        elif recipe == 'rubber':
            rib = .5 + .5 * np.cos(TAU * x * 48)
            color *= (.83 + .17 * rib)[..., None]
            height += .0005 * rib
        elif recipe == 'paper':
            fibers = np.sin(TAU * (y * 190 + .3 * medium))
            height += .00008 * fibers
            color *= (1 + .04 * fibers)[..., None]
        elif recipe == 'glass':
            color *= (1 - .15 * drips)[..., None]
            smooth -= .2 * drips
    return np.clip(color, 0, 1), height, np.clip(smooth, .02, .96), np.clip(metallic, 0, 1)
