"""Generates Terralayer's placeholder pixel art. Run from the repo root:  python tools/make_art.py

Writes:
  Assets/Runes/<Kind>.png   24x24 rune stones
  Assets/Rift.png           48x64 x 6 frames (vertical sheet), grayscale so the game can tint it
  Assets/Shrine.png         48x40 altar, grayscale-ish so it can be tinted
  Assets/Runeforge.png      80x64 the homestead's rune-inscribing machine
  icon.png                  80x80 mod icon
"""
import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
os.makedirs(os.path.join(ROOT, "Assets", "Runes"), exist_ok=True)

RUNES = {
    "Reaping": (170, 80, 255),
    "Splitting": (120, 220, 255),
    "Ember": (255, 120, 40),
    "Frost": (140, 220, 255),
    "Storm": (255, 245, 120),
    "Vampiric": (220, 40, 60),
    "Starfall": (255, 230, 150),
    "Hive": (250, 200, 40),
    "Venom": (120, 230, 80),
    "Midas": (255, 210, 60),
    "Quake": (200, 140, 90),
}


def glyph(d, kind, c):
    """Draws the rune's symbol onto a 24x24 canvas (coordinates in the 6..18 range)."""
    w = 2
    if kind == "Reaping":
        d.arc((6, 5, 18, 17), 200, 70, fill=c, width=w)
        d.line((12, 11, 12, 19), fill=c, width=w)
    elif kind == "Splitting":
        d.line((12, 19, 12, 12), fill=c, width=w)
        d.line((12, 12, 7, 6), fill=c, width=w)
        d.line((12, 12, 17, 6), fill=c, width=w)
    elif kind == "Ember":
        d.polygon([(12, 5), (17, 14), (15, 19), (9, 19), (7, 14)], outline=c, fill=None)
        d.polygon([(12, 10), (14, 15), (12, 18), (10, 15)], fill=c)
    elif kind == "Frost":
        for a in range(0, 180, 60):
            r = math.radians(a)
            d.line((12 - 6 * math.cos(r), 12 - 6 * math.sin(r), 12 + 6 * math.cos(r), 12 + 6 * math.sin(r)), fill=c, width=w)
    elif kind == "Storm":
        d.line([(14, 5), (9, 12), (14, 12), (9, 19)], fill=c, width=w)
    elif kind == "Vampiric":
        d.polygon([(12, 5), (17, 13), (16, 17), (12, 19), (8, 17), (7, 13)], fill=c)
    elif kind == "Starfall":
        pts = []
        for i in range(10):
            r = 7 if i % 2 == 0 else 3
            a = math.radians(-90 + i * 36)
            pts.append((12 + r * math.cos(a), 12 + r * math.sin(a)))
        d.polygon(pts, fill=c)
    elif kind == "Hive":
        pts = [(12 + 6 * math.cos(math.radians(a)), 12 + 6 * math.sin(math.radians(a))) for a in range(30, 390, 60)]
        d.polygon(pts, outline=c)
        d.ellipse((10, 10, 14, 14), fill=c)
    elif kind == "Venom":
        d.ellipse((8, 6, 16, 14), outline=c, width=w)
        d.line((10, 14, 10, 18), fill=c, width=1)
        d.line((14, 14, 14, 18), fill=c, width=1)
        d.point((10, 10), fill=c)
        d.point((14, 10), fill=c)
    elif kind == "Midas":
        d.ellipse((6, 6, 18, 18), outline=c, width=w)
        d.line((12, 8, 12, 16), fill=c, width=w)
    elif kind == "Quake":
        d.line([(6, 9), (10, 13), (8, 15), (13, 19)], fill=c, width=w)
        d.line([(18, 7), (14, 11), (16, 13), (12, 16)], fill=c, width=w)


def rune(kind, color):
    img = Image.new("RGBA", (24, 24), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    # Stone body: dark outline, mid fill, a lighter bevel on the top-left.
    d.rounded_rectangle((2, 1, 21, 22), radius=6, fill=(38, 36, 48, 255))
    d.rounded_rectangle((3, 2, 20, 21), radius=5, fill=(92, 90, 108, 255))
    d.rounded_rectangle((3, 2, 18, 18), radius=5, fill=(112, 110, 130, 255))
    d.rounded_rectangle((5, 4, 19, 20), radius=4, fill=(82, 80, 98, 255))
    # Glow behind the glyph.
    glow = Image.new("RGBA", (24, 24), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    glyph(gd, kind, color + (255,))
    glow = glow.filter(ImageFilter.GaussianBlur(1.2))
    img = Image.alpha_composite(img, glow)
    d = ImageDraw.Draw(img)
    bright = tuple(min(255, int(v * 0.6 + 110)) for v in color) + (255,)
    glyph(d, kind, bright)
    # Keep the silhouette crisp.
    mask = Image.new("L", (24, 24), 0)
    ImageDraw.Draw(mask).rounded_rectangle((2, 1, 21, 22), radius=6, fill=255)
    out = Image.new("RGBA", (24, 24), (0, 0, 0, 0))
    out.paste(img, (0, 0), mask)
    out.save(os.path.join(ROOT, "Assets", "Runes", f"{kind}.png"))


ARMOR_RUNES = {
    "Fortitude": (170, 170, 200), "Vigor": (230, 70, 80), "Swiftness": (120, 230, 200), "Warding": (110, 160, 255),
    "Renewal": (120, 230, 110), "Fury": (255, 120, 60), "Zephyr": (200, 240, 255), "Focus": (160, 120, 255),
    "Fortune": (255, 210, 60), "Thorns": (160, 200, 90),
}


def armor_glyph(d, kind, c):
    if kind == "Fortitude":
        d.rectangle((8, 8, 16, 16), outline=c, width=2)
    elif kind == "Vigor":
        d.polygon([(12, 18), (6, 11), (8, 7), (12, 9), (16, 7), (18, 11)], fill=c)
    elif kind == "Swiftness":
        d.line([(7, 8), (12, 12), (7, 16)], fill=c, width=2)
        d.line([(12, 8), (17, 12), (12, 16)], fill=c, width=2)
    elif kind == "Warding":
        d.arc((6, 6, 18, 18), 180, 360, fill=c, width=2)
        d.line((12, 6, 12, 18), fill=c, width=2)
    elif kind == "Renewal":
        d.line((12, 7, 12, 17), fill=c, width=3)
        d.line((7, 12, 17, 12), fill=c, width=3)
    elif kind == "Fury":
        d.polygon([(12, 6), (16, 12), (12, 18), (8, 12)], outline=c)
        d.point((12, 12), fill=c)
    elif kind == "Zephyr":
        d.arc((6, 8, 14, 14), 180, 20, fill=c, width=2)
        d.arc((10, 11, 18, 17), 180, 20, fill=c, width=2)
    elif kind == "Focus":
        d.ellipse((8, 8, 16, 16), outline=c, width=2)
        d.ellipse((11, 11, 13, 13), fill=c)
    elif kind == "Fortune":
        d.ellipse((7, 7, 17, 17), outline=c, width=2)
        d.line((10, 12, 14, 12), fill=c, width=2)
    elif kind == "Thorns":
        for x in (8, 12, 16):
            d.polygon([(x - 2, 17), (x, 8), (x + 2, 17)], fill=c)


def armor_rune(kind, color):
    os.makedirs(os.path.join(ROOT, "Assets", "ArmorRunes"), exist_ok=True)
    img = Image.new("RGBA", (24, 24), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    shield = [(3, 3), (21, 3), (21, 12), (12, 22), (3, 12)]
    d.polygon(shield, fill=(36, 34, 46, 255))
    d.polygon([(4, 4), (20, 4), (20, 12), (12, 20), (4, 12)], fill=(96, 98, 116, 255))
    d.polygon([(5, 5), (17, 5), (17, 11), (11, 17), (5, 11)], fill=(116, 118, 138, 255))
    glow = Image.new("RGBA", (24, 24), (0, 0, 0, 0))
    armor_glyph(ImageDraw.Draw(glow), kind, color + (255,))
    glow = glow.filter(ImageFilter.GaussianBlur(1.1))
    img = Image.alpha_composite(img, glow)
    armor_glyph(ImageDraw.Draw(img), kind, tuple(min(255, int(v * 0.6 + 110)) for v in color) + (255,))
    mask = Image.new("L", (24, 24), 0)
    ImageDraw.Draw(mask).polygon(shield, fill=255)
    out = Image.new("RGBA", (24, 24), (0, 0, 0, 0))
    out.paste(img, (0, 0), mask)
    out.save(os.path.join(ROOT, "Assets", "ArmorRunes", f"{kind}.png"))


def rift():
    frames = 6
    w, h = 48, 64
    sheet = Image.new("RGBA", (w, h * frames), (0, 0, 0, 0))
    for f in range(frames):
        img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        px = img.load()
        cx, cy = w / 2 - 0.5, h / 2 - 0.5
        rx, ry = 20.0, 29.0
        phase = f / frames * math.tau
        for y in range(h):
            for x in range(w):
                dx = (x - cx) / rx
                dy = (y - cy) / ry
                r = math.hypot(dx, dy)
                if r > 1.0:
                    continue
                ang = math.atan2(dy, dx)
                swirl = 0.5 + 0.5 * math.sin(ang * 3 + r * 9 - phase)
                core = max(0.0, 1.0 - r * 1.6)
                edge = max(0.0, 1.0 - abs(r - 0.92) * 9)
                v = min(1.0, 0.25 + swirl * 0.45 * (1 - r) + core * 0.9 + edge * 0.8)
                a = min(1.0, (1.0 - r) * 3.5 + edge)
                g = int(255 * v)
                px[x, y] = (g, g, g, int(255 * a * 0.95))
        sheet.paste(img, (0, f * h))
    sheet.save(os.path.join(ROOT, "Assets", "Rift.png"))


def shrine():
    img = Image.new("RGBA", (48, 40), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rectangle((6, 30, 41, 39), fill=(70, 66, 80, 255))
    d.rectangle((8, 28, 39, 31), fill=(110, 104, 124, 255))
    d.rectangle((14, 14, 33, 28), fill=(88, 84, 100, 255))
    d.rectangle((14, 14, 33, 16), fill=(130, 124, 146, 255))
    d.polygon([(10, 14), (37, 14), (33, 8), (14, 8)], fill=(150, 146, 166, 255))
    d.ellipse((16, 3, 31, 12), fill=(235, 235, 235, 255))
    d.ellipse((19, 5, 28, 10), fill=(255, 255, 255, 255))
    for x in (17, 22, 27, 31):
        d.line((x, 17, x, 26), fill=(66, 62, 76, 255))
    img.save(os.path.join(ROOT, "Assets", "Shrine.png"))


def runeforge():
    """80x64: a stone anvil-altar with a floating, glowing rune circle above it."""
    img = Image.new("RGBA", (80, 64), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    # Base and pillar.
    d.rectangle((14, 54, 65, 63), fill=(52, 48, 62, 255))
    d.rectangle((18, 50, 61, 55), fill=(84, 80, 98, 255))
    d.rectangle((28, 34, 51, 50), fill=(70, 66, 82, 255))
    d.rectangle((28, 34, 51, 36), fill=(104, 100, 120, 255))
    # Anvil top.
    d.polygon([(10, 26), (70, 26), (64, 34), (16, 34)], fill=(96, 92, 112, 255))
    d.rectangle((10, 24, 70, 27), fill=(130, 126, 148, 255))
    # Glowing rune ring above.
    glow = Image.new("RGBA", (80, 64), (0, 0, 0, 0))
    g = ImageDraw.Draw(glow)
    g.ellipse((22, 0, 58, 20), outline=(200, 120, 255, 255), width=3)
    g.ellipse((33, 5, 47, 15), fill=(240, 200, 255, 255))
    for i in range(6):
        a = math.radians(i * 60)
        g.rectangle((40 + 16 * math.cos(a) - 1, 10 + 8 * math.sin(a) - 1, 40 + 16 * math.cos(a) + 1, 10 + 8 * math.sin(a) + 1), fill=(255, 220, 255, 255))
    glow = glow.filter(ImageFilter.GaussianBlur(1.3))
    img = Image.alpha_composite(img, glow)
    d = ImageDraw.Draw(img)
    d.ellipse((24, 2, 56, 18), outline=(230, 180, 255, 255), width=1)
    # Rune glyphs glowing on the pillar.
    for x in (33, 39, 45):
        d.line((x, 39, x + 2, 46), fill=(210, 140, 255, 255))
    img.save(os.path.join(ROOT, "Assets", "Runeforge.png"))


def icon():
    img = Image.new("RGBA", (80, 80), (24, 20, 40, 255))
    d = ImageDraw.Draw(img)
    for i in range(80):
        c = int(30 + i * 0.6)
        d.line((0, i, 79, i), fill=(c // 2, c // 3, c, 255))
    # Gatehouse silhouette.
    d.rectangle((14, 30, 66, 74), fill=(70, 68, 84, 255))
    for x in range(14, 67, 8):
        d.rectangle((x, 24, x + 4, 30), fill=(70, 68, 84, 255))
    d.rectangle((10, 18, 22, 74), fill=(84, 82, 98, 255))
    d.rectangle((58, 18, 70, 74), fill=(84, 82, 98, 255))
    for x in (10, 14, 18, 58, 62, 66):
        d.rectangle((x, 13, x + 2, 18), fill=(84, 82, 98, 255))
    # The rift in the archway.
    d.rounded_rectangle((28, 40, 52, 74), radius=12, fill=(20, 14, 30, 255))
    glow = Image.new("RGBA", (80, 80), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    gd.ellipse((30, 44, 50, 74), fill=(150, 90, 255, 255))
    gd.ellipse((35, 50, 45, 70), fill=(230, 210, 255, 255))
    glow = glow.filter(ImageFilter.GaussianBlur(2.5))
    img = Image.alpha_composite(img, glow)
    d = ImageDraw.Draw(img)
    d.rectangle((0, 74, 79, 79), fill=(40, 90, 40, 255))
    img.save(os.path.join(ROOT, "icon.png"))


if __name__ == "__main__":
    random.seed(7)
    for k, c in RUNES.items():
        rune(k, c)
    for k, c in ARMOR_RUNES.items():
        armor_rune(k, c)
    rift()
    shrine()
    runeforge()
    icon()
    print("art written")
