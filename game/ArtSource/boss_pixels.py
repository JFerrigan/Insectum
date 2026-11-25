"""Drawing and preview helpers for frame-based boss pixel art."""

import math
from pathlib import Path

from PIL import Image, ImageDraw

from forest_pixels import OUT

PREVIEWS = Path(__file__).resolve().parent.parent / "Builds/BossPreviews"
PADDING = 3
CONTACT = 0.85


class BossPose:
    """Base for named pose parameters in local pixels with Y pointing up."""

    def __init__(self, **values):
        self.__dict__.update(values)


def rotate(point, pivot, degrees):
    a = math.radians(degrees)
    x, y = point[0] - pivot[0], point[1] - pivot[1]
    return (pivot[0] + x * math.cos(a) - y * math.sin(a), pivot[1] + x * math.sin(a) + y * math.cos(a))


def ellipse(cx, cy, rx, ry, start=0, end=360, steps=28):
    return [(cx + rx * math.cos(math.radians(start + (end - start) * i / steps)),
             cy + ry * math.sin(math.radians(start + (end - start) * i / steps))) for i in range(steps + 1)]


def effects():
    ink = (27, 16, 41, 255)
    bark = (104, 67, 38, 255)
    bark_light = (142, 95, 53, 255)
    moss = (100, 128, 52, 255)
    sheet = Image.new("RGBA", (256, 64), (0, 0, 0, 0))
    draw = ImageDraw.Draw(sheet)
    dust = (214, 196, 160, 255)
    dust_dark = (150, 128, 100, 255)
    # Row 0, 32 x 32 cells: dust puffs 0-3, clank sparks 4-6, bark shards 7.
    for f in range(4):
        ox, r = f * 32, 4 + f * 3
        draw.ellipse((ox + 16 - r, 24 - r, ox + 16 + r, 24 + r * 0.6), dust_dark if f == 3 else dust)
        if f < 3:
            draw.ellipse((ox + 12 - r // 2, 22 - r, ox + 12 + r // 2, 22), dust)
        if f >= 2:
            draw.ellipse((ox + 16 - r + 3, 24 - r + 3, ox + 16 + r - 3, 24 + r * 0.6 - 3), (0, 0, 0, 0))
    for f in range(3):
        ox = (4 + f) * 32
        reach = 5 + f * 4
        for angle in range(0, 360, 45):
            a = math.radians(angle + f * 20)
            inner = 2 + f * 3
            draw.line((ox + 16 + inner * math.cos(a), 16 + inner * math.sin(a),
                       ox + 16 + reach * math.cos(a), 16 + reach * math.sin(a)), (255, 244, 200, 255) if f < 2 else (255, 189, 103, 255))
        if f == 0:
            draw.rectangle((ox + 15, 15, ox + 17, 17), (255, 255, 255, 255))
    ox = 7 * 32
    draw.polygon([(ox + 10, 11), (ox + 20, 9), (ox + 23, 15), (ox + 17, 22), (ox + 9, 19)], bark, ink)
    draw.line((ox + 12, 13, ox + 19, 12), bark_light)
    draw.point((ox + 14, 17), moss)
    # Row 1 (y 32), 64 x 16 cells: ground shadow 0, dive target 1, shockwave 2-3.
    draw.ellipse((4, 38, 60, 46), (255, 255, 255, 255))
    draw.ellipse((68, 36, 124, 46), (255, 255, 255, 255))
    draw.ellipse((72, 38, 120, 44), (0, 0, 0, 0))
    for f in range(2):
        ox = (2 + f) * 64
        for i, h in enumerate((4, 8, 11, 9, 6, 3) if f == 0 else (3, 7, 12, 10, 7, 4)):
            draw.rectangle((ox + 20 + i * 4, 47 - h, ox + 23 + i * 4, 47), dust if i % 2 else dust_dark)
        draw.line((ox + 18, 47, ox + 46, 47), dust_dark)
    sheet.save(OUT / "BossFx.png")


def contact_sheet(table, atlas, *, title, filename, cell, columns, ground, key):
    CELL_W, CELL_H = cell
    COLUMNS, GROUND = columns, ground
    PREVIEWS.mkdir(parents=True, exist_ok=True)
    actors = Image.open(OUT / "Actors.png").convert("RGBA")
    player = actors.crop((0, 0, 32, 24))
    scale = 2
    label_h = 12
    total = sum(len(poses) for _, poses in table)
    cols = 8
    rows = math.ceil(total / cols)
    top_h = CELL_H * 3 + 24
    sheet = Image.new("RGBA", (cols * CELL_W * scale, top_h + rows * (CELL_H * scale + label_h)), (28, 22, 40, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((8, 4), title, fill=(255, 232, 187, 255))
    for i, index in enumerate(key):
        frame = atlas.crop((index % COLUMNS * CELL_W, index // COLUMNS * CELL_H,
                            index % COLUMNS * CELL_W + CELL_W, index // COLUMNS * CELL_H + CELL_H))
        x = 8 + i * (CELL_W + 10) * 3 // 2
        big = Image.new("RGBA", (CELL_W, CELL_H), (0, 0, 0, 0))
        big.alpha_composite(frame)
        big.alpha_composite(player, (4, GROUND - 22))
        big = big.resize((CELL_W * 3 // 2, CELL_H * 3 // 2), Image.NEAREST)
        sheet.alpha_composite(big, (x, 20))
    draw.line((0, top_h - 4, sheet.width, top_h - 4), (90, 70, 110, 255))
    index = 0
    for name, poses in table:
        for f in range(len(poses)):
            cx, cy = index % cols, index // cols
            frame = atlas.crop((index % COLUMNS * CELL_W, index // COLUMNS * CELL_H,
                                index % COLUMNS * CELL_W + CELL_W, index // COLUMNS * CELL_H + CELL_H))
            x, y = cx * CELL_W * scale, top_h + cy * (CELL_H * scale + label_h)
            draw.rectangle((x, y + label_h + (GROUND * scale), x + CELL_W * scale - 1, y + label_h + GROUND * scale), (90, 70, 110, 255))
            sheet.alpha_composite(frame.resize((CELL_W * scale, CELL_H * scale), Image.NEAREST), (x, y + label_h))
            draw.text((x + 4, y), f"{name} {f}", fill=(255, 232, 187, 255))
            index += 1
    sheet.save(PREVIEWS / filename)


def hurtbox_coverage(shell, parts, pivot):
    solid = covered = 0
    for py in range(shell.height):
        for px in range(shell.width):
            if shell.getpixel((px, py))[3] < 128:
                continue
            solid += 1
            wx, wy = px + 0.5 - pivot[0], pivot[1] - py - 0.5
            if any((wx - x * 16) ** 2 + (wy - y * 16) ** 2 <= (r * 16 + PADDING) ** 2
                   for x, y, r, _ in parts):
                covered += 1
    return covered / solid


def draw_hurtboxes(draw, parts, origin, pivot, scale, special_color):
    ox, oy = origin
    for x, y, r, special in parts:
        cx, cy = ox + (pivot[0] + x * 16) * scale, oy + (pivot[1] - y * 16) * scale
        hurt = (r * 16 + PADDING) * scale
        touch = r * 16 * CONTACT * scale
        draw.ellipse((cx - hurt, cy - hurt, cx + hurt, cy + hurt),
                     outline=special_color if special else (110, 230, 140, 255), width=2)
        draw.ellipse((cx - touch, cy - touch, cx + touch, cy + touch), outline=(240, 80, 80, 255))


def pose_preview(atlas, *, title, filename, cell, columns, ground, cards):
    """A compact sheet of review poses with the player at the same scale."""
    PREVIEWS.mkdir(parents=True, exist_ok=True)
    w, h = cell
    player = Image.open(OUT / "Actors.png").convert("RGBA").crop((0, 0, 32, 24))
    cols, scale, gap, label = 3, 2, 8, 18
    card_w, card_h = (w + 38) * scale + gap, (h - 2) * scale + label
    sheet = Image.new("RGBA", (cols * card_w, math.ceil(len(cards) / cols) * card_h + 28), (20, 25, 30, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((12, 8), title, fill=(240, 229, 201, 255))
    for i, (caption, index) in enumerate(cards):
        x, y = i % cols * card_w + 4, i // cols * card_h + 28
        draw.rectangle((x, y, x + card_w - 9, y + card_h - 4), fill=(38, 45, 47, 255))
        draw.text((x + 8, y + 4), caption, fill=(240, 229, 201, 255))
        px, py = index % columns * w, index // columns * h
        frame = atlas.crop((px, py, px + w, py + h))
        baseline = y + label + ground * scale
        draw.line((x + 8, baseline, x + card_w - 17, baseline), (86, 102, 91, 255))
        sheet.alpha_composite(player.resize((64, 48), Image.NEAREST), (x + 6, baseline - 44))
        sheet.alpha_composite(frame.resize((w * scale, h * scale), Image.NEAREST), (x + 70, y + label))
    sheet.save(PREVIEWS / filename)
