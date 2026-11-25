"""Paint the item icons: Items.png, 6 x 2 cells of 32x32 in ItemId order (ExcavatorClaw is cell 0).

Excavator Claw and Moth Eyes are drawn in the original Forest style; the rest use the shared style.
"""

from PIL import Image

from forest_pixels import Cel, OUT, INK, CREAM, GOLD, CYAN, PINK
from sprite_style import Pic

VIOLET = "#a183d2"
ROSE = "#d27457"
TAN = "#dba076"
WOOD = "#d69669"
MOSS = "#82b58a"


def claw(c):  # Excavator Claw: mole-cricket spade forearm with digging teeth
    c.line([(6, 26), (11, 20)], INK, 6)
    c.line([(6, 26), (11, 20)], "#a95e72", 3)
    c.oval((8, 13, 18, 23), ROSE, INK)
    c.line([(10, 16), (13, 15)], TAN)
    c.shape([(14, 10), (24, 6), (27, 10), (25, 15), (17, 19)], TAN, INK)
    for tip in ((29, 8), (29, 13), (27, 18), (23, 21)):
        c.shape([(tip[0] - 5, tip[1] - 3), tip, (tip[0] - 4, tip[1] + 1)], CREAM, INK)
    c.line([(16, 11), (23, 8)], "#f6d5a7")
    c.dot((20, 14), "#a9605a")


def moth_eyes(c):  # Moth Eyes: fluffy moth head, comb antennae, big faceted eyes
    for side in (-1, 1):
        base, tip = (16 + side * 2, 10), (16 + side * 11, 2)
        c.line([base, tip], INK, 3)
        c.line([base, tip], "#c4a8e0")
        for t in (0.35, 0.6, 0.85):
            x = round(base[0] + (tip[0] - base[0]) * t)
            y = round(base[1] + (tip[1] - base[1]) * t)
            c.line([(x, y), (x + side, y + 2)], INK)
            c.dot((x + side, y + 1), "#c4a8e0")
    c.oval((5, 18, 27, 28), "#f2e0c8", INK)
    for x in (8, 13, 19, 24):
        c.dot((x, 25), "#c4a8a0")
    c.oval((8, 9, 24, 24), VIOLET, INK)
    c.oval((6, 12, 15, 22), INK)
    c.oval((17, 12, 26, 22), INK)
    c.oval((7, 13, 14, 21), "#2f6a8a")
    c.oval((18, 13, 25, 21), "#2f6a8a")
    for x, y in ((9, 15), (11, 17), (9, 19), (20, 15), (22, 17), (20, 19)):
        c.dot((x, y), CYAN)
    c.dot((8, 14), CREAM)
    c.dot((19, 14), CREAM)
    c.line([(13, 11), (19, 11)], "#d6c2f0")


def seed_bomb(c):  # Seed Bomb: round seed pod with a stem cap and a lit fuse
    tones = c.oval((6, 11, 26, 29), MOSS)
    for p in ((11, 22), (20, 17), (21, 24), (15, 26), (10, 17)):
        c.dot(p, tones["shade"])
    c.stroke([(16, 10), (17, 6), (20, 4)], "#a77471")
    c.rect((12, 8, 20, 13), "#9a6762", glint=False)
    c.spark(21, 3)
    c.dot((24, 1), GOLD)
    c.dot((24, 5), GOLD)


def burrow_sense(c):  # Burrow Sense: worm rising from soil, sensing ripples around its head
    for r, color in ((9, CYAN), (12, "#3b9b8c")):
        c.arc((17 - r, 12 - r, 17 + r, 12 + r), 145, 200, color)
        c.arc((17 - r, 12 - r, 17 + r, 12 + r), 340, 35, color)
    tones = c.oval((9, 15, 20, 27), "#dd80a0", glint=False)
    c.line([(11, 19), (18, 18)], tones["shade"])
    tones = c.oval((12, 5, 23, 18), "#eb91a7")
    c.line([(13, 15), (21, 14)], tones["shade"])
    c.eye(18, 9)
    c.line([(20, 12), (21, 12)], tones["deep"])
    tones = c.poly([(3, 25), (9, 21), (23, 21), (29, 25), (27, 28), (5, 28)], "#694b64", glint=False)
    for p in ((8, 25), (14, 25), (21, 25), (25, 26)):
        c.dot(p, tones["light"])


def heartwood(c):  # Heartwood: wooden heart with a pink knot, sprouting a leaf
    c.stroke([(16, 10), (17, 4)], "#558d69")
    c.poly([(17, 5), (22, 1), (26, 2), (22, 6)], "#a0bf76", glint=False)
    tones = c.poly([(16, 28), (5, 17), (4, 11), (7, 7), (12, 7), (16, 11), (20, 7), (25, 7), (28, 11), (27, 17)],
                   WOOD)
    c.line([(8, 12), (13, 19)], tones["shade"])
    c.line([(19, 21), (24, 14)], tones["shade"])
    c.oval((13, 14, 19, 20), PINK, glint=False)
    c.dot((15, 16), CREAM)


def flame_glands(c):  # Flame Glands: swollen gland with a flame venting from its duct
    tones = c.oval((6, 12, 26, 29), "#d0505a")
    c.line([(10, 22), (13, 25), (17, 25)], tones["deep"])
    c.line([(20, 16), (21, 19)], tones["deep"])
    c.rect((13, 8, 19, 14), "#a9384a", glint=False)
    c.poly([(16, 0), (21, 6), (20, 9), (12, 9), (11, 6), (14, 3)], GOLD, glint=False)
    c.poly([(16, 4), (18, 7), (16, 9), (14, 7)], CREAM, outline=False, light=False, glint=False)


def volatile_blood(c):  # Volatile Blood: fizzing drop of blood
    tones = c.poly([(16, 4), (22, 13), (24, 19), (21, 26), (16, 28), (11, 26), (8, 19), (10, 13)], "#e0584a")
    c.oval((17, 19, 21, 23), tones["shade"], light=False, glint=False, outline=False)
    c.dot((18, 20), tones["light"])
    c.dot((13, 23), tones["light"])
    c.spark(25, 8)
    c.spark(6, 11)
    c.spark(26, 25)


def reboot(c):  # Emergency Reboot: silk chrysalis around a glowing power cell
    c.line([(16, 0), (16, 4)], "#e8e0d0")
    tones = c.poly([(16, 4), (22, 9), (24, 17), (21, 26), (16, 30), (11, 26), (8, 17), (10, 9)], "#d8c8b0")
    for y in (10, 25):
        c.line([(11, y), (21, y + 1)], tones["shade"])
    c.oval((11, 12, 21, 23), "#284357", glint=False)
    c.arc((13, 14, 19, 21), 300, 240, CYAN)
    c.line([(16, 13), (16, 17)], CYAN)


def reed_flask(c):  # Reed Flask: corked reed stalk holding cyan sap, leaf tied on
    tones = c.poly([(8, 26), (20, 5), (26, 9), (14, 29)], "#c4b06a")
    c.poly([(11, 24), (16, 15), (21, 18), (15, 27)], "#5ae0d0", outline=False)
    c.line([(15, 13), (22, 17)], tones["deep"])
    c.poly([(19, 5), (22, 1), (28, 5), (26, 9)], "#a77471", glint=False)
    c.poly([(4, 13), (10, 13), (14, 18), (8, 18)], MOSS, glint=False)


def prism_fang(c):  # Prism Fang: crystal fang on a cord
    c.arc((7, 1, 25, 17), 180, 360, "#a77471")
    c.line([(7, 9), (11, 11)], "#a77471")
    c.line([(25, 9), (21, 11)], "#a77471")
    c.poly([(11, 13), (21, 13), (20, 19), (16, 25), (10, 30), (12, 22)], CYAN)
    c.poly([(17, 14), (20, 14), (19, 19), (15, 25), (13, 27), (16, 19)], VIOLET, outline=False, glint=False)
    c.rect((10, 10, 22, 14), WOOD, glint=False)


def dune_mantle(c):  # Dune Mantle: scarab-shell cloak spread open, gem clasp
    for side in (-1, 1):
        tones = c.poly([(16, 6), (16 + side * 9, 5), (16 + side * 14, 14), (16 + side * 12, 27), (16 + side * 3, 23)],
                       TAN)
        c.line([(16 + side * 4, 9), (16 + side * 8, 22)], tones["shade"])
        c.line([(16 + side * 8, 8), (16 + side * 11, 15)], tones["shade"])
    c.line([(16, 6), (16, 23)], INK)
    c.oval((13, 3, 19, 9), CYAN, glint=False)
    c.dot((15, 5), CREAM)


def flux_core(c):  # Flux Core: charged orb in a brass ring
    c.oval((5, 5, 27, 27), GOLD, glint=False)
    c.fill_oval((8, 8, 24, 24), INK)
    orb = c.oval((10, 10, 22, 22), PINK)
    c.arc((12, 12, 20, 20), 200, 330, orb["light"])
    for x, y in ((16, 5), (16, 27), (5, 16), (27, 16)):
        c.rect((x - 2, y - 2, x + 2, y + 2), GOLD, glint=False)


# In ItemId order. Cel-drawn icons are the original Forest style; the rest use Pic.
ITEMS = [(claw, Cel), (seed_bomb, Pic), (moth_eyes, Cel), (burrow_sense, Pic), (heartwood, Pic),
         (flame_glands, Pic), (volatile_blood, Pic), (reboot, Pic), (reed_flask, Pic), (prism_fang, Pic),
         (dune_mantle, Pic), (flux_core, Pic)]


def icon(index):
    paint, canvas = ITEMS[index]
    cel = canvas(32, 32)
    paint(cel)
    return cel.image


def items():
    atlas = Image.new("RGBA", (6 * 32, 2 * 32))
    for index in range(len(ITEMS)):
        atlas.paste(icon(index), ((index % 6) * 32, (index // 6) * 32))
    atlas.save(OUT / "Items.png")


if __name__ == "__main__":
    items()
