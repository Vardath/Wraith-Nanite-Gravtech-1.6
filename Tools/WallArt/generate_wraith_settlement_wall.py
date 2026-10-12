#!/usr/bin/env python3
"""WNG 1.6 Wraith living wall: native wall silhouette with a Wraith material skin.

The previous 2026-10-12 variants were rejected as decorative panels. This is
a *material*, not sixteen plaque paintings. The same continuous membrane skin
covers every native 1x1 wall state; only exposed faces receive chitin coping,
bevel and a short shadowed wall face. Linked edges contain no frame or motif.

This deliberately preserves the native AtlasPool layout, Wall XML, linkFlags,
CornerFiller, the 1x1 footprint, room/roof/door/airlock mechanics and the
separate gravship hull atlas. No tiny WebP crops or enlarged concept-sheet art.

Editable source of truth: this Python source and the Material Recipe in the
WNG wall-design directive. PNG textures are generated directly into the mod.
"""
from __future__ import annotations
import argparse
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
ATLAS = ROOT / "Textures/Things/Building/Wraith/Structure/WNG_LivingWall_Atlas.png"
ICON = ROOT / "Textures/UI/WNG/Build/WNG_WraithLivingWall.png"
LEGACY = ROOT / "Textures/Things/Building/Wraith/Structure/WNG_LivingWall.png"
S = 192
STRIDE = 256
INSET = 32

def save(image, dest):
    dest.parent.mkdir(parents=True, exist_ok=True)
    image.save(dest, format="PNG", optimize=True)

def membrane_skin():
    """A textured full-cell chitin/membrane roof: no central emblem or border.

    Four unequal, gently tapering organic fibres are subdued rather than
    painted as posts. Their highlights and scars are part of the surface.
    The first and last three rows/columns are equal for native linked seams.
    """
    yy, xx = np.indices((S, S), dtype=np.float32)
    x, y = xx / S, yy / S
    v = (.4 * np.cos(math.tau * y) +
         .25 * np.cos(math.tau * x) +
         .15 * np.sin(math.tau * (x + y)))
    r = 58. + 9 * v
    g = 49. + 7 * v
    b = 65. + 11 * v

    for center, width, drift, luminance in (
        (.18, .09, .025, 17),
        (.39, .065, -.022, -13),
        (.63, .075, .030, 17),
        (.83, .085, -.018, -8),
    ):
        cy = center + drift * np.sin(math.tau * y)
        crest = np.exp(-((x - cy) / width) ** 2)
        root = np.exp(-((x - (cy + .4 * width)) / (.45 * width)) ** 2)
        r = r + crest * luminance - root * 6
        g = g + crest * (.78 * luminance) - root * 4
        b = b + crest * (.88 * luminance) - root * 6

    for center, sign in ((.29, 1), (.74, -1)):
        cy = center + .024 * np.sin(math.tau * (y + .13 * sign))
        fibre = (np.exp(-((x - cy) / .012) ** 2) *
                 (.55 + .2 * np.cos(y * 5 * math.pi)))
        r, g, b = r + fibre * 12, g + fibre * 13, b + fibre * 8

    # Subsidiary cross-grain: fine, discontinuous growth rings, not geometric
    # lines marking out separate tiles.
    for k in range(5):
        center = (.10 + .21 * k) % 1
        line = np.exp(-((y - center - .015 * np.sin(4 * math.pi * x + k)) / .012) ** 2)
        r, g, b = r - line * 3, g - line * 3, b - line * 2

    colour = np.stack((r, g, b, np.full_like(r, 255)), axis=-1)
    colour = np.clip(colour, 0, 255).astype(np.uint8)
    # Periodic edge bands are mandatory, including for diagonals and masks
    # where neighbouring walls use different connection configurations.
    left = colour[:, 0, :].copy()
    top = colour[0, :, :].copy()
    for i in range(3):
        colour[:, i, :] = left
        colour[:, -i-1, :] = left
        colour[i, :, :] = top
        colour[-i-1, :, :] = top
    # Ensure corner agreement between both periodic directions.
    corner = colour[0, 0, :].copy()
    for ys in (slice(0,3),slice(-3,None)):
        for xs in (slice(0,3),slice(-3,None)):
            colour[ys,xs,:] = corner
    return Image.fromarray(colour, "RGBA")

BASE = None

def draw_native_wall(mask, source):
    """All sixteen native NESW masks, with visible wall faces ONLY when open."""
    open_n = not bool(mask & 1)
    open_e = not bool(mask & 2)
    open_s = not bool(mask & 4)
    open_w = not bool(mask & 8)
    l = 7 if open_w else 0
    r = 185 if open_e else S
    t = 7 if open_n else 0
    b = 185 if open_s else S

    trim = Image.new("RGBA", (S, S))
    d = ImageDraw.Draw(trim)
    if open_n:
        d.polygon(((l,t),(r,t),(r,t+5),(l,t+5)), fill=(148,133,133,180))
        d.line(((l+2,t+2),(r-2,t+2)), fill=(211,195,169,140), width=2)
        d.polygon(((l,t+5),(r,t+5),(r,t+14),(l,t+14)), fill=(32,25,38,85))
        d.line(((l,t+14),(r,t+14)), fill=(97,80,90,115), width=2)
    if open_s:
        d.polygon(((l,b-17),(r,b-17),(r,b),(l,b)), fill=(22,17,29,145))
        d.line(((l,b-17),(r,b-17)), fill=(146,116,124,160), width=2)
        d.line(((l+1,b-4),(r-1,b-4)), fill=(9,11,18,108), width=3)
    if open_w:
        d.polygon(((l,t),(l+10,t),(l+10,b),(l,b)), fill=(167,147,143,92))
        d.line(((l+2,t+2),(l+2,b-2)), fill=(190,177,155,148), width=2)
        d.line(((l+10,t),(l+10,b)), fill=(27,23,37,115), width=3)
    if open_e:
        d.polygon(((r-13,t),(r,t),(r,b),(r-13,b)), fill=(23,21,33,130))
        d.line(((r-13,t+2),(r-13,b-2)), fill=(117,95,108,160), width=2)
        d.line(((r-3,t+2),(r-3,b-2)), fill=(10,9,16,110), width=3)

    result = Image.alpha_composite(source, trim)
    px = np.asarray(result, dtype=np.uint8).copy()
    # Each missing neighbour removes seven pixels of the perimeter. This
    # yields 85.94% occupied tile for an isolated wall, and 100% if linked on
    # all sides, the same architectural mass as a vanilla linked wall.
    if open_n: px[:7, :, 3] = 0
    if open_e: px[:, -7:, 3] = 0
    if open_s: px[-7:, :, 3] = 0
    if open_w: px[:, :7, 3] = 0
    return Image.fromarray(px, "RGBA")

def bake():
    skin = membrane_skin()
    states = [draw_native_wall(mask, skin) for mask in range(16)]
    atlas = Image.new("RGBA", (S + 2*INSET + 3*STRIDE,)*2)
    for mask, tile in enumerate(states):
        # Exact repeat of native 32px padding: no blurred bleed between masks.
        bordered = np.pad(np.asarray(tile),
                          ((INSET,INSET),(INSET,INSET),(0,0)), mode="edge")
        atlas.paste(Image.fromarray(bordered, "RGBA"),
                    (mask%4*STRIDE, (3-mask//4)*STRIDE))
    save(atlas, ATLAS)
    save(states[0], ICON)
    save(states[0], LEGACY)
    return states, atlas

def preview(states, destination):
    """A connected-wall room test at RimWorld-like 48px-on-screen scale."""
    scene = [
        "..#########.......", "..#.......#.......",
        "..#.####..#.......", "..#.#..#..#.......",
        "..#.#..#..#.......", "..#.####..#.......",
        "..#.......#.......", "..#########.......",
        "......#...........", "....#####.........", "......#..........."
    ]
    size = 48
    out = Image.new("RGBA", (len(scene[0])*size,len(scene)*size),
                    (83,89,81,255))
    d = ImageDraw.Draw(out)
    for y, line in enumerate(scene):
        for x, _ in enumerate(line):
            d.rectangle((x*size,y*size,x*size+size-1,y*size+size-1),
                fill=(89,93,85,255) if (x+y)%2==0 else (84,88,81,255))
    for y, line in enumerate(scene):
        for x, item in enumerate(line):
            if item != "#": continue
            links = sum(bit for bit, dx, dy in
                        ((1,0,-1),(2,1,0),(4,0,1),(8,-1,0))
                        if 0 <= y+dy < len(scene)
                        and 0 <= x+dx < len(scene[y+dy])
                        and scene[y+dy][x+dx] == "#")
            out.alpha_composite(states[links].resize((size,size),
                Image.Resampling.LANCZOS), (x*size,y*size))
    save(out, destination / "Wraith-Living-Wall-Room-Test.png")
    contact = Image.new("RGBA",(4*S,4*S))
    for mask, state in enumerate(states):
        contact.alpha_composite(state, (mask%4*S,(3-mask//4)*S))
    save(contact,destination/"Wraith-Living-Wall-16-States.png")

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", type=Path)
    args = parser.parse_args()
    states, atlas = bake()
    if args.preview: preview(states, args.preview)
    opaque = [float((np.asarray(t)[:,:,3]>=250).mean()) for t in states]
    assert atlas.size == (1024, 1024)
    assert opaque[0] > .855, opaque[0]
    assert opaque[15] > .995, opaque[15]
    print("Wraith living wall mod texture updated:",ATLAS)
    print("Native mask 0 coverage:",round(opaque[0],4),
          "mask 15:",round(opaque[15],4),"states:",len(states))

if __name__ == "__main__":
    main()
