"""Contact sheet for the sprite refresh: every changed actor's 6 frames beside the art at a git ref, the item
icons, the pickups and chests, and the shop pedestal with an item at each bob height.

Usage: python3 sprite_preview.py [git-ref]   (default HEAD; the last commit before the refresh is 62af2fc)
Writes Builds/SpritePreviews/sprites_preview.png. Draws straight from the ArtSource functions, so it shows
the scripts' current output whether or not the PNGs in Assets/Resources/PixelArt have been regenerated.
"""

import io
import subprocess
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

import alien_pixels
import caves_pixels
import desert_pixels
import forest_pixels
import item_pixels
import swamp_pixels

REF = sys.argv[1] if len(sys.argv) > 1 else "HEAD"
ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "Builds/SpritePreviews"
CREAM = "#ffe8bb"
BG = (42, 31, 58, 255)


def at_ref(sheet, row):
    data = subprocess.run(["git", "show", f"{REF}:game/Assets/Resources/PixelArt/{sheet}.png"],
                          cwd=ROOT, capture_output=True, check=True).stdout
    image = Image.open(io.BytesIO(data)).convert("RGBA")
    return [image.crop((f * 32, row * 24, f * 32 + 32, row * 24 + 24)) for f in range(6)]


ACTORS = [  # name, frame painter, (sheet, row) at REF, note
    ("Jumping Spider", lambda f: forest_pixels.insect(0, f), ("Actors", 0), "unchanged"),
    ("Beetle", lambda f: forest_pixels.insect(1, f), ("Actors", 1), "current art + eye"),
    ("Dragonfly", forest_pixels.dragonfly, ("Actors", 2), "new"),
    ("Fire Ant", lambda f: forest_pixels.insect(3, f), ("Actors", 3), "unchanged"),
    ("Worm", forest_pixels.worm, ("Actors", 4), "new"),
    ("Crawler", forest_pixels.crawler, ("Actors", 5), "new"),
    ("Spore Moth", forest_pixels.spore_moth, ("Actors", 6), "new"),
    ("Reed Runner", lambda f: swamp_pixels.styled_actor(0, f), ("SwampActors", 0), "new"),
    ("Skimmer", lambda f: swamp_pixels.styled_actor(1, f), ("SwampActors", 1), "new"),
    ("Cave Hopper", lambda f: caves_pixels.styled_actor(0, f), ("CavesActors", 0), "new"),
    ("Glow Fly", lambda f: caves_pixels.styled_actor(1, f), ("CavesActors", 1), "new"),
    ("Dune Beetle", lambda f: desert_pixels.styled_actor(0, f), ("DesertActors", 0), "new"),
    ("Sand Wasp", lambda f: desert_pixels.styled_actor(1, f), ("DesertActors", 1), "new"),
    ("Lattice Tick", lambda f: alien_pixels.styled_actor(0, f), ("AlienActors", 0), "new"),
    ("Arc Mote", lambda f: alien_pixels.styled_actor(1, f), ("AlienActors", 1), "new"),
]
ITEM_NAMES = ["Excavator Claw (v3)", "Seed Bomb", "Moth Eyes (v3)", "Burrow Sense", "Heartwood", "Flame Glands",
              "Volatile Blood", "Emergency Reboot", "Reed Flask", "Prism Fang", "Dune Mantle", "Flux Core"]
PROPS = [(0, "Nektar"), (1, "Heart Sap"), (2, "Energy Sap"), (4, "Chest"), (16, "Chest (opened)"),
         (5, "Boss Reward"), (17, "Boss Reward (opened)")]


def main():
    font = ImageFont.truetype("/System/Library/Fonts/Menlo.ttc", 14)
    small = ImageFont.truetype("/System/Library/Fonts/Menlo.ttc", 11)
    big = ImageFont.truetype("/System/Library/Fonts/Menlo.ttc", 22)
    zoom, cw = 3, 32 * 4 + 40
    strip = 6 * 32 * zoom + 5 * 4
    height = 130 + len(ACTORS) * (24 * zoom + 14) + 2 * (32 * 4 + 26) + (32 * 4 + 26) + 58 * 4 + 120
    sheet = Image.new("RGBA", (20 + 150 + strip * 2 + 40, height), (30, 22, 42, 255))
    d = ImageDraw.Draw(sheet)
    d.text((12, 12), "Insectum sprite refresh - all 6 frames", fill=CREAM, font=big)

    y = 52
    x_new, x_old = 170, 170 + strip + 40
    d.text((x_new, y), "ArtSource scripts now (frames 0-5, shown at 3x)", fill="#a8a0c0", font=font)
    d.text((x_old, y), f"Game art at {REF}", fill="#6f6788", font=font)
    d.text((x_new, y + 18), "player: 0 idle  1-2 move  3 air  4 attack  5 heavy/ability    "
           "enemy: 1-2 walk  4 attack  5 warning", fill="#6f6788", font=small)
    y += 40
    for name, paint, (sheet_name, row), note in ACTORS:
        d.text((12, y + 20), name, fill=CREAM, font=font)
        d.text((12, y + 38), note, fill="#8a82a0", font=small)
        for f, (new, old) in enumerate(zip([paint(f) for f in range(6)], at_ref(sheet_name, row))):
            for x0, image, bg in ((x_new, new, BG), (x_old, old, (36, 27, 50, 255))):
                x = x0 + f * (32 * zoom + 4)
                d.rectangle((x, y, x + 32 * zoom - 1, y + 24 * zoom - 1), fill=bg)
                sheet.alpha_composite(image.resize((32 * zoom, 24 * zoom), Image.NEAREST), (x, y))
        y += 24 * zoom + 14

    def tile(x, y, image, name):
        d.rectangle((x, y, x + cw - 8, y + 32 * 4 + 20), fill=BG)
        sheet.alpha_composite(image.resize((128, 128), Image.NEAREST), (x + (cw - 8 - 128) // 2, y))
        w = d.textlength(name, font=small)
        d.text((x + (cw - 8 - w) / 2, y + 130), name, fill=CREAM, font=small)

    y += 10
    d.text((12, y), "Items (Items.png, ItemId order, shown at 4x)", fill="#a8a0c0", font=font)
    y += 26
    for i, name in enumerate(ITEM_NAMES):
        tile(12 + (i % 6) * cw, y + (i // 6) * (32 * 4 + 26), item_pixels.icon(i), name)
    y += 2 * (32 * 4 + 26) + 14

    d.text((12, y), "Pickups & chests (Props.png, shown at 4x)", fill="#a8a0c0", font=font)
    y += 26
    for i, (index, name) in enumerate(PROPS):
        tile(12 + i * cw, y, forest_pixels.styled_prop(index), name)
    y += 32 * 4 + 40

    d.text((12, y), "Shop pedestal: the item floats 22px above it and bobs 0-3px (first four: each bob height)",
           fill="#a8a0c0", font=font)
    y += 26
    pedestal = forest_pixels.styled_prop(3)
    for i, (item, bob) in enumerate(((4, 0), (4, 1), (4, 2), (4, 3), (2, 1), (7, 0))):
        cell = Image.new("RGBA", (32, 58), BG)
        cell.alpha_composite(pedestal, (0, 26))
        cell.alpha_composite(item_pixels.icon(item), (0, 4 - bob))
        sheet.alpha_composite(cell.resize((32 * 4, 58 * 4), Image.NEAREST), (12 + i * cw + (cw - 8 - 128) // 2, y))

    OUT.mkdir(parents=True, exist_ok=True)
    sheet.save(OUT / "sprites_preview.png")
    print(OUT / "sprites_preview.png")


if __name__ == "__main__":
    main()
