#!/usr/bin/env python3
"""Generates the temporary, original placeholder art of the project (deterministic).

Usage:  python3 tools/generate_placeholder_art.py
Writes PNG files into content/assets/textures and mods/example-mod/assets/textures.
Requires Pillow. The game never runs this script: the PNGs are committed like any other asset
and can be replaced by real art without touching the code (see docs/MODDING.md).
"""
import math
import random
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
BASE = ROOT / "content" / "assets" / "textures"
MOD = ROOT / "mods" / "example-mod" / "assets" / "textures"

# Kit marker colours, replaced at load time by team colours (see content/assets.json "palette").
SHIRT, SHIRT_DARK = (255, 0, 0), (192, 0, 0)
SHORTS, SHORTS_DARK = (0, 0, 255), (0, 0, 192)
SOCKS, SOCKS_DARK = (0, 255, 0), (0, 192, 0)
SKIN, SKIN_DARK = (228, 180, 132), (190, 140, 100)
HAIR = (70, 46, 30)
BOOT = (24, 24, 24)
OUTLINE = (20, 24, 20)

FRAME = 32
ANCHOR = (16, 29)
# Row order used by the game: up, up-right, right, down-right, down, down-left, left, up-left.
DIRECTIONS = [(0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1)]
RUN_FRAMES = 6


def normalized(v):
    length = math.hypot(*v)
    return (v[0] / length, v[1] / length)


def outline(img):
    """Adds a 1-pixel dark outline around opaque pixels, for readability on grass."""
    src = img.copy()
    px, out = src.load(), img.load()
    w, h = img.size
    for y in range(h):
        for x in range(w):
            if px[x, y][3] != 0:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < w and 0 <= ny < h and px[nx, ny][3] != 0:
                    out[x, y] = OUTLINE + (255,)
                    break


def draw_player(direction, phase, kicking=False):
    """One 32x32 frame of a footballer seen from above at an angle. phase in [-1, 1] = stride."""
    img = Image.new("RGBA", (FRAME, FRAME), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    fx, fy = normalized(direction)
    side = abs(fx)  # 1 = profile, 0 = front/back
    cx, feet = ANCHOR[0], ANCHOR[1]
    facing_up = fy < -0.3

    # Legs (drawn first; the torso covers their top). Stride moves feet along the facing direction.
    stride = 3.0 * phase
    spread = 2.5 * (1 - side) + 0.5
    for leg, sign in ((0, 1), (1, -1)):
        lateral = -spread if leg == 0 else spread
        offset = stride * sign
        if kicking and leg == 0:
            offset = 4.5
        foot_x = cx + lateral * (1 - side) + fx * offset
        foot_y = feet + fy * offset * 0.5 - side * (0.6 if leg == 0 else -0.6)
        hip_x, hip_y = cx + lateral * 0.8, feet - 8
        d.line([(hip_x, hip_y), (foot_x, foot_y - 3)], fill=SKIN, width=2)
        d.line([(foot_x, foot_y - 3), (foot_x, foot_y - 1)], fill=SOCKS if leg == 0 else SOCKS_DARK, width=2)
        d.rectangle([foot_x - 1, foot_y - 1, foot_x + 1, foot_y], fill=BOOT)

    # Shorts and torso.
    half = 4.5 - 1.5 * side
    d.rectangle([cx - half, feet - 11, cx + half, feet - 8], fill=SHORTS)
    d.rectangle([cx - half, feet - 9, cx - half + 1, feet - 8], fill=SHORTS_DARK)
    torso_top = feet - 20
    d.rounded_rectangle([cx - half - 0.5, torso_top, cx + half + 0.5, feet - 10], radius=2, fill=SHIRT)
    shade_x = cx + half - 1 if fx <= 0 else cx - half - 0.5
    d.rectangle([shade_x, torso_top + 2, shade_x + 1, feet - 11], fill=SHIRT_DARK)

    # Arms swing opposite to the legs.
    for sign in (-1, 1):
        swing = -stride * sign * 0.8
        ax = cx + sign * (half + 1.5) * (1 - 0.6 * side) + fx * swing * 0.6
        ay = torso_top + 2
        hx, hy = ax + fx * swing * 0.4, ay + 6 + fy * swing * 0.4
        d.line([(ax, ay), (hx, hy)], fill=SHIRT if abs(swing) < 1.5 else SHIRT_DARK, width=2)
        d.point((hx, hy + 1), fill=SKIN_DARK)

    # Head with hair on the back side.
    hx, hy = cx + fx * 0.8, torso_top - 4
    d.ellipse([hx - 3.5, hy - 3.5, hx + 3.5, hy + 3.5], fill=SKIN)
    hair = Image.new("RGBA", (FRAME, FRAME), (0, 0, 0, 0))
    hd = ImageDraw.Draw(hair)
    hcx, hcy = hx - fx * 2.2, hy - fy * 2.2 - (1.2 if not facing_up else 0)
    hd.ellipse([hcx - 3.6, hcy - 3.6, hcx + 3.6, hcy + 3.6], fill=HAIR + (255,))
    mask = Image.new("L", (FRAME, FRAME), 0)
    ImageDraw.Draw(mask).ellipse([hx - 3.5, hy - 3.5, hx + 3.5, hy + 3.5], fill=255)
    img.paste(hair, (0, 0), Image.composite(hair.getchannel("A"), Image.new("L", (FRAME, FRAME), 0), mask))
    if fy > 0.3:  # face visible: two eyes
        for ex in (-1.5, 1.5):
            d.point((round(hx + ex + fx), round(hy + 0.5)), fill=OUTLINE)
    outline(img)
    return img


def player_sheet():
    columns = 1 + RUN_FRAMES + 1  # idle, run cycle, kick
    sheet = Image.new("RGBA", (FRAME * columns, FRAME * len(DIRECTIONS)), (0, 0, 0, 0))
    for row, direction in enumerate(DIRECTIONS):
        sheet.paste(draw_player(direction, 0.0), (0, row * FRAME))
        for i in range(RUN_FRAMES):
            phase = math.sin(2 * math.pi * i / RUN_FRAMES)
            sheet.paste(draw_player(direction, phase), ((1 + i) * FRAME, row * FRAME))
        sheet.paste(draw_player(direction, 0.0, kicking=True), ((1 + RUN_FRAMES) * FRAME, row * FRAME))
    return sheet


def ball_sheet(light, dark):
    """4 frames of a 7x7 rolling ball: a patch moves across it."""
    size, frames = 7, 4
    sheet = Image.new("RGBA", (size * frames, size), (0, 0, 0, 0))
    for f in range(frames):
        frame = Image.new("RGBA", (size, size), (0, 0, 0, 0))
        d = ImageDraw.Draw(frame)
        d.ellipse([0, 0, size - 1, size - 1], fill=light + (255,), outline=OUTLINE + (255,))
        px = 1 + (f * 1.25)
        d.rectangle([px, 2, px + 1, 4], fill=dark + (255,))
        d.point((2, 1), fill=(255, 255, 255, 255))
        sheet.paste(frame, (f * size, 0))
    return sheet


def shadow():
    img = Image.new("RGBA", (16, 8), (0, 0, 0, 0))
    ImageDraw.Draw(img).ellipse([0, 0, 15, 7], fill=(0, 0, 0, 90))
    return img


def tile(base, noise, seed, specks=None, streaks=None, grid=None):
    rng = random.Random(seed)
    img = Image.new("RGBA", (32, 32), base + (255,))
    px = img.load()
    for y in range(32):
        for x in range(32):
            v = rng.randint(-noise, noise)
            r, g, b, a = px[x, y]
            px[x, y] = (max(0, min(255, r + v)), max(0, min(255, g + v)), max(0, min(255, b + v)), 255)
    if specks:
        colour, count = specks
        for _ in range(count):
            px[rng.randrange(32), rng.randrange(32)] = colour + (255,)
    if streaks:
        colour, count = streaks
        d = ImageDraw.Draw(img)
        for _ in range(count):
            x, y = rng.randrange(32), rng.randrange(32)
            d.line([(x, y), (x + rng.randint(3, 8), y + rng.randint(-1, 1))], fill=colour + (255,))
    if grid:
        for i in range(0, 32, 4):
            for j in range(32):
                r, g, b, a = px[i, j]
                px[i, j] = (r + grid, min(255, g + grid), b + grid, 255)
    return img


def net():
    img = Image.new("RGBA", (6, 6), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.line([(0, 0), (5, 0)], fill=(235, 235, 235, 150))
    d.line([(0, 0), (0, 5)], fill=(235, 235, 235, 150))
    return img


def selection_marker():
    img = Image.new("RGBA", (9, 6), (0, 0, 0, 0))
    ImageDraw.Draw(img).polygon([(0, 0), (8, 0), (4, 5)], fill=(255, 255, 255, 255), outline=OUTLINE + (255,))
    return img


def main():
    BASE.mkdir(parents=True, exist_ok=True)
    MOD.mkdir(parents=True, exist_ok=True)
    player_sheet().save(BASE / "player.png")
    ball_sheet((245, 245, 245), (40, 40, 40)).save(BASE / "ball.png")
    shadow().save(BASE / "shadow.png")
    net().save(BASE / "net.png")
    selection_marker().save(BASE / "selection.png")
    tiles = {
        "grass_normal": tile((62, 140, 58), 6, 1, specks=((80, 160, 70), 30)),
        "grass_wet": tile((50, 122, 66), 5, 2, specks=((150, 200, 190), 18)),
        "grass_soggy": tile((74, 108, 52), 9, 3, specks=((60, 80, 40), 60)),
        "grass_artificial": tile((48, 160, 72), 2, 4, grid=10),
        "grass_icy": tile((160, 200, 200), 6, 5, streaks=((235, 250, 250), 10)),
        "grass_muddy": tile((104, 88, 54), 10, 6, specks=((70, 120, 50), 40)),
        "grass_bumpy": tile((72, 136, 56), 12, 7, specks=((96, 160, 74), 50)),
        "surround": tile((44, 104, 46), 6, 8, specks=((36, 90, 40), 40)),
    }
    for name, image in tiles.items():
        image.save(BASE / f"{name}.png")
    # Example mod: a neon pitch and a golden ball, to show that textures really come from the mod.
    tile((40, 34, 96), 6, 9, specks=((90, 220, 230), 25), grid=14).save(MOD / "grass_neon.png")
    ball_sheet((250, 200, 40), (150, 90, 10)).save(MOD / "ball_gold.png")


if __name__ == "__main__":
    main()
