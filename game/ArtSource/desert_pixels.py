"""Paint wind-cut dunes, sandstone and Desert sprites at native size."""

import random

from forest_pixels import Cel, OUT
from sprite_style import Pic, limb

INK = "#352035"
GOLD = "#ffd17b"
CYAN = "#74dbd5"
CORAL = "#e98965"


def backdrop():
    cel = Cel(384, 192, "#49313e")
    cel.shape([(0, 0), (384, 0), (384, 124), (271, 105), (151, 113), (0, 140)], "#684742")
    cel.oval((133, 19, 239, 127), "#996048")
    cel.oval((143, 29, 229, 117), "#b98053")
    cel.shape([(0, 121), (48, 89), (81, 92), (122, 117), (161, 113), (224, 83), (256, 91),
               (307, 128), (384, 109), (384, 192), (0, 192)], "#855545")
    cel.shape([(0, 145), (65, 123), (105, 126), (160, 148), (203, 134), (262, 124),
               (316, 139), (384, 141), (384, 192), (0, 192)], "#725047")
    cel.shape([(0, 166), (51, 158), (112, 149), (175, 167), (218, 172), (287, 154),
               (335, 152), (384, 164), (384, 192), (0, 192)], "#54404a")
    for x, y, w in [(38, 114, 17), (85, 135, 9), (258, 104, 19), (296, 120, 15), (338, 122, 9)]:
        cel.shape([(x - w, 175), (x - w + 4, y), (x + w - 8, y), (x + w - 8, y + 5),
                   (x + w, y + 5), (x + w, 175)], "#544047", "#473746")
        cel.line([(x - 4, y + 6), (x - 4, y + 37), (x + 6, y + 43)], "#695052", 2)
        cel.box((x + 1, y + 12, x + 4, y + 13), "#5e9f9a")
        cel.box((x + 1, y + 17, x + 2, y + 18), "#63918b")
    for x, y in [(19, 158), (112, 153), (192, 171), (219, 160), (359, 155)]:
        cel.line([(x, 183), (x + 1, y + 5), (x + 5, y)], "#413543", 2)
        cel.line([(x + 1, y + 12), (x - 4, y + 8), (x - 5, y + 3)], "#413543", 2)
    rng = random.Random(2131)
    for _ in range(140):
        x, y = rng.randrange(384), rng.randrange(118, 192)
        cel.line([(x, y), (min(383, x + rng.randrange(1, 5)), y)],
                 rng.choice(["#885e52", "#9a6d55", "#5a4953"]))
    cel.image.save(OUT / "DesertBackdrop.png")


def terrain():
    atlas = Cel(112, 352)
    colors = [
        ("#a77a4f", "#dbaa64"), ("#815843", "#b77d50"), ("#785644", "#9e7549"),
        ("#895840", "#ae784c"), ("#905747", "#b97b59"), ("#7c6455", "#a47b57"),
        ("#7e574a", "#a97559"), ("#8b5144", "#c27e57"), ("#54656b", "#779491"),
        ("#71494a", "#a06750"),
    ]
    for row, (shade, light) in enumerate(colors):
        for col in range(4):
            tile = Cel(28, 32, "#503443")
            tile.shape([(0, 8), (10, 1), (22, 3), (27, 8), (26, 23), (19, 30), (8, 31), (1, 22)], shade)
            tile.shape([(0, 8), (10, 1), (22, 3), (27, 8), (23, 11), (16, 8), (9, 11), (2, 13)], light)
            for offset in (0, 7, 13):
                y = 12 + offset + col % 2
                tile.line([(2, y), (9, y - 2), (16, y + 1), (24, y - 1)], "#684449")
                if row in (0, 7, 9):
                    tile.line([(4, y+1), (9, y), (15, y+2), (23, y)], light)
            rng = random.Random(1206 + row * 4 + col)
            for _ in range(32):
                tile.dot((rng.randrange(28), rng.randrange(32)), rng.choice([shade, light, "#a37153"]))
            atlas.image.paste(tile.image, (col * 28, row * 32))

    for col in range(4):
        growth = Cel(28, 32)
        if col == 0:
            for x in range(28):
                growth.line([(x, 0), (x, 9 + x * 3 % 8)], "#b4784d")
                growth.line([(x, 0), (x, 4 + x % 5)], "#dba364")
        elif col == 3:
            growth.shape([(3, 31), (5, 21), (15, 21), (15, 14), (19, 11), (25, 14), (26, 31)], "#52676a", INK)
            growth.line([(7, 24), (16, 24), (21, 18)], "#74a9a4", 2)
            growth.box((19, 14, 23, 15), CYAN)
        else:
            for x, y in [(6, 24), (13, 13 + col), (22, 20)]:
                growth.line([(x, 31), (x + 1, y + 5), (x + 4, y)], "#aa855c", 2)
                growth.line([(x + 1, y + 5), (x - 3, y + 1), (x - 6, y)], "#d2ab6c")
        atlas.image.paste(growth.image, (col * 28, 320))
    atlas.image.save(OUT / "DesertTerrain.png")


def styled_actor(kind, frame):
    """Dune Beetle and Sand Wasp in the shared style, animated like the originals."""
    pic = Pic(32, 24, INK)
    strike = frame in (4, 5)
    if kind == 0:
        for x in (8, 13, 19, 23):
            toe = x + frame % 3 - 1
            limb(pic, [(x, 13), (toe - 2, 17), (toe + 2, 19)], "#be8256")
        pic.oval((3, 6, 23, 17), "#ad603f")
        pic.poly([(5, 11), (9, 8), (16, 7), (20, 9), (18, 12), (11, 14)], "#d18f54")
        pic.oval((20, 9, 28, 17), "#68413f")
        pic.eye(24, 11, CYAN)
        pic.stroke([(27, 15), (30, 11 if strike else 13)], CORAL)
    else:
        y = 2 + frame % 2 * 5
        pic.poly([(17, 12), (10, y), (3, y + 1), (7, y + 5), (17, 14)], "#9cbcb2")
        pic.line([(1, 14), (4, 12), (8, 12)], CORAL if strike else "#b48e5c")
        pic.oval((7, 9, 24, 17), "#bf783f")
        for x in (12, 16):
            pic.line([(x, 10), (x + 1, 16)], "#663b43", 2)
        pic.poly([(17, 13), (7, 15), (3, 19), (10, 20), (19, 15)], "#6b999b")
        pic.oval((19, 8, 27, 16), "#d3924b")
        pic.eye(23, 10, CYAN)
    return pic.image


def actor(kind, frame):
    if kind < 2:
        return styled_actor(kind, frame)
    # The boss keeps its original drawing until the boss overhaul.
    cel = Cel(32, 24)
    strike = frame in (4, 5)
    cel.shape([(0, 17), (4, 11), (10, 6), (16, 4), (23, 6), (29, 12), (31, 19),
               (23, 20), (9, 20)], "#895044", INK)
    cel.shape([(3, 13), (8, 9), (15, 6), (22, 8), (22, 12), (13, 16), (7, 16)], "#c48352", INK)
    cel.line([(7, 11), (12, 8), (17, 8), (21, 10)], GOLD, 2)
    cel.line([(5, 16), (11, 17), (18, 14)], "#b36346", 2)
    cel.oval((21, 11, 29, 17), "#5c3b45", INK)
    cel.dot((25, 12), CYAN)
    cel.line([(28, 14), (31, 11 if strike else 13)], GOLD, 2)
    cel.line([(28, 16), (31, 19 if strike else 17)], CORAL, 2)
    return cel.image


def actors():
    atlas = Cel(192, 72)
    for kind in range(3):
        for frame in range(6):
            atlas.image.paste(actor(kind, frame), (32 * frame, 24 * kind))
    atlas.image.save(OUT / "DesertActors.png")


def hazards():
    atlas = Cel(128, 32)
    for frame in range(4):
        cel = Cel(32, 32)
        if frame < 3:
            cel.shape([(2, 23), (7, 20), (25, 20), (29, 23), (26, 27), (5, 27)], "#704c44", INK)
            cel.line([(6, 23), (24, 23)], "#9a7857", 2)
            hue = GOLD if frame else "#9b7758"
            cel.line([(8, 21), (22, 21)], hue, 2)
            if frame:
                cel.line([(8, 17), (12, 16), (22, 17)], hue)
            if frame == 2:
                cel.shape([(1, 19), (6, 14), (13, 13), (18, 9), (25, 11), (30, 15), (28, 21), (6, 23)],
                          CORAL, "#aa574b")
                cel.line([(4, 18), (11, 17), (18, 12), (25, 14)], GOLD, 2)
        else:
            cel.shape([(1, 19), (6, 15), (11, 14), (18, 15), (23, 14), (29, 19), (26, 23), (4, 23)],
                      "#ab634c", INK)
            cel.line([(3, 19), (9, 17), (20, 17), (27, 19)], GOLD, 2)
            cel.line([(12, 21), (18, 21)], CYAN)
            cel.dot((8, 13), CORAL)
            cel.dot((23, 12), CORAL)
        atlas.image.paste(cel.image, (frame * 32, 0))
    atlas.image.save(OUT / "DesertHazards.png")



def cacti():
    """Cacti standing on their bottom edge: a barrel 0.8 wide and 0.9 tall, then a tall one 0.6 wide and 1.3 tall."""
    atlas = Cel(32, 32)
    dark, mid, light, spine = "#2f6a4c", "#4f9a5e", "#8ac66e", "#f6e6b4"
    barrel = Cel(16, 32)
    barrel.oval((2, 17, 13, 31), mid, INK)
    for x in (5, 8, 11):
        barrel.line([(x, 19), (x, 29)], dark)
    barrel.line([(4, 21), (4, 27)], light)
    for x, y in ((3, 20), (12, 21), (2, 25), (13, 26), (6, 18), (10, 18), (3, 29), (12, 29)):
        barrel.dot((x, y), spine)
    barrel.dot((7, 16), CORAL)
    barrel.dot((8, 16), CORAL)
    barrel.dot((7, 17), GOLD)
    atlas.image.paste(barrel.image, (0, 0))
    tall = Cel(16, 32)
    tall.shape([(4, 31), (4, 13), (6, 11), (9, 11), (11, 13), (11, 31)], mid, INK)
    tall.shape([(11, 22), (13, 22), (14, 20), (14, 16), (13, 15), (12, 16), (12, 20), (11, 20)], mid, INK)
    tall.line([(7, 13), (7, 30)], dark)
    tall.line([(5, 14), (5, 29)], light)
    for x, y in ((3, 15), (12, 14), (3, 20), (12, 25), (3, 26), (15, 17), (6, 10), (9, 10), (3, 30)):
        tall.dot((x, y), spine)
    atlas.image.paste(tall.image, (16, 0))
    atlas.image.save(OUT / "DesertCacti.png")


if __name__ == "__main__":
    backdrop()
    terrain()
    actors()
    hazards()
    cacti()
