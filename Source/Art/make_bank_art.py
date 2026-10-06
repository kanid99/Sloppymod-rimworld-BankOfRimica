#!/usr/bin/env python3
"""Generates the Bank of Rimica textures (requires Pillow):

  Common/Textures/Things/Building/BoR_BankShuttle.png                 fallback bank shuttle art (Odyssey's passenger shuttle is used when found)

The silver bars reuse vanilla gold's stack-count textures, desaturated at load (see Graphics.cs),
and the bank crate uses Vanilla Quests Expanded - Deadlife's military crate art.
"""
import math
import os
from PIL import Image, ImageDraw

SCALE = 4
OUTLINE = (34, 30, 28, 255)
LW = 7

WOOD_TOP = (176, 132, 84, 255)
WOOD_SEAM = (146, 106, 64, 255)
WOOD_FRONT = (124, 88, 54, 255)
WOOD_DARK = (78, 56, 34, 255)

BAR_TOP = (226, 231, 237, 255)
BAR_TOP_HI = (247, 249, 252, 255)
BAR_FRONT = (166, 174, 185, 255)
BAR_FRONT_DARK = (128, 136, 148, 255)

BANK_BLUE = (52, 92, 170, 255)
BANK_BLUE_DARK = (34, 60, 116, 255)
BANK_GOLD = (232, 200, 104, 255)
HULL = (232, 236, 240, 255)
HULL_SHADE = (190, 198, 208, 255)
GLASS = (40, 70, 110, 255)
GLASS_HI = (120, 170, 220, 255)
ENGINE = (255, 170, 80, 255)

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def poly(d, pts, fill, width=LW):
    d.polygon(pts, fill=fill)
    d.line(pts + [pts[0]], fill=OUTLINE, width=width, joint="curve")


def finish(img, size):
    return img.resize((size, size), Image.LANCZOS)


# ------------------------------------------------------------------ shuttle

def make_shuttle():
    S = 256 * SCALE
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    cx = S // 2
    # wings
    for side in (-1, 1):
        wing = [(cx + side * 110, 420), (cx + side * 430, 660), (cx + side * 440, 760),
                (cx + side * 400, 790), (cx + side * 110, 720)]
        poly(d, wing, HULL_SHADE, width=10)
        d.line([(cx + side * 150, 520), (cx + side * 410, 720)], fill=BANK_BLUE, width=22)
        # wingtip lights
        d.ellipse([cx + side * 415 - 14, 745, cx + side * 415 + 14, 773], fill=BANK_GOLD if side < 0 else BANK_BLUE)
    # engines
    for ex in (cx - 120, cx + 120):
        poly(d, [(ex - 50, 760), (ex + 50, 760), (ex + 44, 930), (ex - 44, 930)], HULL_SHADE, width=10)
        d.ellipse([ex - 34, 900, ex + 34, 960], fill=ENGINE, outline=OUTLINE, width=8)
    # fuselage
    body = [(cx, 60), (cx + 80, 140), (cx + 150, 330), (cx + 165, 620), (cx + 150, 880),
            (cx + 90, 950), (cx - 90, 950), (cx - 150, 880), (cx - 165, 620), (cx - 150, 330), (cx - 80, 140)]
    poly(d, body, HULL, width=10)
    # shading down one side
    d.polygon([(cx + 60, 200), (cx + 140, 340), (cx + 152, 620), (cx + 138, 860), (cx + 90, 920), (cx + 70, 920),
               (cx + 110, 860), (cx + 122, 620), (cx + 110, 360)], fill=HULL_SHADE)
    # cockpit
    poly(d, [(cx, 120), (cx + 62, 200), (cx + 70, 270), (cx - 70, 270), (cx - 62, 200)], GLASS, width=8)
    d.line([(cx - 30, 170), (cx + 10, 150)], fill=GLASS_HI, width=12)
    # blue racing stripes
    for sx in (cx - 60, cx + 36):
        d.rectangle([sx, 300, sx + 24, 900], fill=BANK_BLUE)
    # cargo hatch outline
    d.rounded_rectangle([cx - 100, 600, cx + 100, 840], radius=18, outline=OUTLINE, width=8)
    # bank emblem
    ey = 450
    d.ellipse([cx - 80, ey - 80, cx + 80, ey + 80], fill=BANK_GOLD, outline=OUTLINE, width=10)
    d.ellipse([cx - 56, ey - 56, cx + 56, ey + 56], outline=BANK_BLUE_DARK, width=8)
    d.line([(cx - 26, ey - 16), (cx + 26, ey - 16)], fill=BANK_BLUE_DARK, width=12)
    d.line([(cx - 26, ey + 16), (cx + 14, ey + 16)], fill=BANK_BLUE_DARK, width=12)
    out = os.path.join(ROOT, "Common", "Textures", "Things", "Building")
    os.makedirs(out, exist_ok=True)
    finish(img, 256).save(os.path.join(out, "BoR_BankShuttle.png"))


if __name__ == "__main__":
    make_shuttle()
    print("done")
