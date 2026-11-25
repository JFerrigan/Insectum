"""Paint Alien biomatter, machines and sprites at the game's native size."""

import random

from forest_pixels import Cel, OUT
from sprite_style import Pic, limb

INK = "#191633"
CYAN = "#86f5e2"
LIME = "#d5f480"
PINK = "#f286ce"


def backdrop():
    cel = Cel(384, 192, "#151932")
    cel.shape([(0, 0), (384, 0), (384, 192), (0, 192)], "#221d40")
    cel.shape([(0, 75), (69, 46), (129, 65), (197, 24), (270, 49), (322, 32), (384, 71),
               (384, 152), (339, 169), (266, 152), (210, 165), (137, 138), (70, 171), (0, 150)], "#322852")
    cel.shape([(71, 111), (135, 81), (172, 84), (220, 48), (252, 79), (294, 90), (327, 132),
               (281, 152), (228, 142), (195, 153), (129, 143)], "#3b3c6a")
    for x, y, w in [(28, 50, 20), (91, 74, 11), (151, 110, 10), (257, 85, 13), (321, 80, 16), (361, 43, 18)]:
        cel.shape([(x - w, 192), (x - w, y+18), (x, y), (x+w, y+15), (x+w, 192)], "#263552", "#202941")
        cel.line([(x, y+4), (x, y+36), (x-6, y+45), (x-6, 191)], "#41637b", 2)
        cel.line([(x+4, y+12), (x+4, y+29)], "#609c95", 2)
        cel.dot((x-6, y+51), "#8cc5a6")
    for x, y, h in [(16, 140, 44), (52, 124, 60), (110, 145, 41), (177, 153, 32), (217, 130, 58),
                    (273, 146, 42), (306, 128, 51), (370, 132, 52)]:
        cel.line([(x, y+h), (x+2, y+18), (x+12, y)], "#4f4366", 4)
        cel.line([(x+6, y+15), (x-3, y+8), (x-8, y+1)], "#524971", 2)
        cel.oval((x+7, y-9, x+19, y+5), "#566890", "#382d57")
        cel.line([(x+10, y-3), (x+15, y-5), (x+17, y-1)], "#8e9cb5")
        cel.oval((x-11, y-4, x-4, y+3), "#805b82")
        cel.dot((x-8, y-1), "#c0849e")
    rng = random.Random(3527)
    for _ in range(142):
        x, y = rng.randrange(384), rng.randrange(20, 189)
        cel.dot((x, y), rng.choice(["#716290", "#588380", "#465d80", "#4a4263"]))
    cel.image.save(OUT / "AlienBackdrop.png")


def terrain():
    atlas = Cel(112, 352)
    colors = [
        ("#635966", "#8a7587"), ("#52425b", "#74607b"), ("#434e62", "#627782"),
        ("#573b61", "#79597b"), ("#67375f", "#935f89"), ("#633875", "#9963a5"),
        ("#413d68", "#6d6693"), ("#4c3b60", "#76618b"), ("#385861", "#607e91"),
        ("#443a66", "#796aa3"),
    ]
    for row, (mid, high) in enumerate(colors):
        for col in range(4):
            tile = Cel(28, 32, "#2e2848")
            tile.shape([(0, 10), (9, 3), (16, 0), (24, 4), (27, 11), (24, 25),
                        (18, 31), (6, 27), (1, 22)], mid)
            tile.shape([(0, 10), (9, 3), (16, 0), (24, 4), (27, 11), (21, 8), (15, 8),
                        (10, 13), (1, 16)], high)
            rng = random.Random(1498 + row * 13 + col)
            for _ in range(30):
                tile.dot((rng.randrange(28), rng.randrange(32)), rng.choice([mid, high, "#3e3458"]))
            zone = (7+col*2, 12+col)
            if row == 5:
                tile.line([(zone[0], 8), zone, (14, 17), (13, 26)], "#d176b3", 2)
                tile.line([(14, 17), (22, 13), (24, 9)], "#b864a7")
                tile.dot((13, 26), PINK)
            elif row in (8, 9):
                tile.line([(6, 12), (6, 20), (13, 24), (21, 24), (25, 20)], "#384063", 2)
                tile.box((11, 11+col, 16, 12+col), "#5faca5")
                tile.box((19, 15+col, 22, 15+col), "#b394b7")
            else:
                tile.line([(5, 23), (12, 20), (17, 23), (23, 20)], "#363151")
            atlas.image.paste(tile.image, (col * 28, row * 32))

    for col in range(4):
        growth = Cel(28, 32)
        if col == 0:
            for x in range(28):
                growth.line([(x, 0), (x, 8 + x*5 % 10)], "#645793")
                growth.line([(x, 0), (x, 4 + x % 4)], "#9a84b6" if x % 3 else "#6ea4a9")
        else:
            dark, lit = [("#845493", PINK), ("#4c8796", CYAN), ("#6e916a", LIME)][col-1]
            for x, y in [(5, 21), (14, 11), (22, 20)]:
                growth.line([(x, 31), (x+1, y+8), (x, y+5)], dark, 2)
                growth.oval((x-3, y, x+4, y+6), dark, INK)
                growth.line([(x-1, y+1), (x+1, y+1), (x+2, y+3)], lit)
        atlas.image.paste(growth.image, (col*28, 320))
    atlas.image.save(OUT / "AlienTerrain.png")


def styled_actor(kind, frame):
    """Lattice Tick and Arc Mote in the shared style, animated like the originals."""
    pic = Pic(32, 24, INK)
    charged = frame in (4, 5)
    if kind == 0:
        for x, direction in [(6, -1), (10, 1), (17, -1), (23, 1)]:
            toe = x + (frame % 3) * direction
            limb(pic, [(x, 12), (x + direction, 16), (toe - 1, 19), (toe + 2, 19)], "#8371b3")
        pic.poly([(3, 12), (5, 7), (12, 5), (19, 8), (21, 14), (16, 17), (7, 16)], "#8d508e")
        for x in (8, 12, 16):
            pic.dot((x, 12), CYAN)
        pic.stroke([(25, 12), (29, 10 if charged else 13), (30, 14)], PINK)
        pic.oval((18, 8, 27, 16), "#64578f")
        pic.eye(22, 10, LIME)
    else:
        shift = frame % 2 * 3
        pic.poly([(15, 10), (8, 3 + shift), (2, 5 + shift), (6, 12), (14, 14)], "#6c83ae")
        pic.poly([(15, 12), (10, 14), (4, 20 - shift), (12, 18 - shift), (17, 15)], "#725ba4")
        pic.poly([(11, 13), (15, 7), (22, 6), (27, 12), (23, 17), (15, 16)], "#478c9b")
        pic.oval((18, 9, 25, 14), "#9fcdb1", glint=False)
        pic.eye(21, 10, LIME)
        pic.dot((29, 12), CYAN if charged else "#7775aa")
        if charged:
            pic.dot((30, 9), CYAN)
    return pic.image


def actor(kind, frame):
    if kind < 2:
        return styled_actor(kind, frame)
    # The boss keeps its original drawing until the boss overhaul.
    cel = Cel(32, 24)
    charged = frame in (4, 5)
    cel.shape([(1, 19), (3, 11), (7, 8), (7, 3), (11, 1), (13, 6), (18, 6), (20, 1),
               (24, 3), (24, 8), (30, 12), (31, 19), (24, 21), (7, 21)], "#454575", INK)
    cel.shape([(8, 10), (11, 8), (21, 8), (25, 13), (21, 17), (11, 17), (6, 14)], "#7871ac", INK)
    cel.line([(8, 10), (11, 8), (21, 8), (24, 12)], PINK, 2)
    cel.oval((12, 10, 21, 15), "#367985", INK)
    cel.box((15, 11, 19, 14), CYAN if charged else LIME)
    cel.line([(3, 16), (6, 19), (10, 19)], "#61909d")
    cel.line([(24, 18), (28, 17), (30, 15)], "#61909d")
    return cel.image


def actors():
    atlas = Cel(192, 72)
    for kind in range(3):
        for frame in range(6):
            atlas.image.paste(actor(kind, frame), (32*frame, 24*kind))
    atlas.image.save(OUT / "AlienActors.png")


def hazards():
    atlas = Cel(128, 32)
    for frame in range(4):
        cel = Cel(32, 32)
        if frame < 3:
            cel.shape([(6, 24), (10, 20), (23, 20), (27, 24), (23, 28), (10, 28)], "#545382", INK)
            cel.box((11, 24, 21, 25), "#548f9c")
            if frame >= 1:
                for x, y in [(13, 19), (18, 15), (15, 12), (20, 8)]:
                    cel.dot((x, y), CYAN)
            if frame == 2:
                path = [(16, 25), (13, 21), (18, 17), (14, 13), (20, 10), (17, 6), (19, 1)]
                cel.line(path, "#648cd0", 6)
                cel.line(path, "#92dbe1", 3)
                cel.line(path, "#e7ffe9")
                cel.line([(13, 21), (8, 17)], PINK, 2)
        else:
            cel.line([(0, 16), (31, 16)], "#7165b8", 6)
            cel.line([(0, 16), (31, 16)], "#94cbec", 4)
            cel.line([(0, 16), (31, 16)], CYAN, 2)
            cel.line([(0, 16), (31, 16)], "#e2fff0")
        atlas.image.paste(cel.image, (32*frame, 0))
    atlas.image.save(OUT / "AlienHazards.png")



def lasers():
    """A laser emitter centered in a 16-pixel cel, then a beam a unit long and four pixels thick in the top rows of
    the next cel, drawn from its left edge so it can be stretched and turned."""
    atlas = Cel(32, 16)
    atlas.oval((3, 3, 12, 12), "#3b2f6b", INK)
    atlas.oval((5, 5, 10, 10), PINK, "#a04f97")
    atlas.dot((7, 7), "#fff3fb")
    for x, y in ((2, 7), (13, 8), (7, 2), (8, 13)):
        atlas.dot((x, y), CYAN)
    atlas.line([(16, 0), (31, 0)], "#a04f97")
    atlas.line([(16, 1), (31, 1)], PINK)
    atlas.line([(16, 2), (31, 2)], "#fff3fb")
    atlas.line([(16, 3), (31, 3)], "#a04f97")
    atlas.image.save(OUT / "AlienLasers.png")


if __name__ == "__main__":
    backdrop()
    terrain()
    actors()
    hazards()
    lasers()
