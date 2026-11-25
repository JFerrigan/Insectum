"""Paint the Swamp biome at the game's native pixel scale."""

import random

from forest_pixels import Cel, OUT
from sprite_style import Pic, limb

INK = "#14202c"
TEAL = "#54b4a4"
LIME = "#afdf76"
GOLD = "#e5ac64"


def backdrop():
    canvas = Cel(384, 192, "#112834")
    randomizer = random.Random(1330)
    canvas.shape([(0, 0), (384, 0), (384, 96), (290, 116), (215, 106), (140, 120), (70, 101), (0, 105)],
                 "#153745")
    canvas.shape([(30, 98), (106, 52), (168, 41), (251, 52), (324, 81), (348, 120), (92, 121)], "#23505a")
    canvas.shape([(74, 103), (137, 66), (206, 59), (263, 70), (308, 103), (214, 112), (143, 116)], "#2c5b61")
    for x, depth in [(16, 91), (48, 104), (99, 126), (143, 139), (236, 139), (288, 132), (333, 115), (365, 99)]:
        canvas.shape([(x - 9, 0), (x + 13, 0), (x + 5, 26), (x + 10, depth - 26),
                      (x + 29, depth), (x - 17, depth + 6), (x - 3, depth - 27)], "#193743")
        canvas.line([(x + 7, 54), (x + 23, 41), (x + 30, 40)], "#193743", 4)
    canvas.shape([(0, 125), (59, 119), (109, 127), (165, 116), (211, 122), (280, 112), (384, 121),
                  (384, 192), (0, 192)], "#183643")
    for _ in range(160):
        x, y = randomizer.randrange(384), randomizer.randrange(123, 192)
        canvas.line([(x, y), (min(383, x + randomizer.randrange(2, 14)), y)],
                    randomizer.choice(["#1b3f49", "#24535a", "#2b5f62"]))
    for x in range(7, 382, 11):
        h = randomizer.randrange(8, 54)
        y = 191 - h
        stem = randomizer.choice(["#172c37", "#264c50", "#315758"])
        canvas.line([(x, 191), (x + 2, y + 9), (x + 4, y)], stem, 2)
        canvas.line([(x + 2, y + 13), (x - 3, y + 9), (x - 7, y + 3)], stem, 2)
        if x % 3 == 1:
            canvas.oval((x + 2, y - 5, x + 6, y + 5), "#355d5c", stem)
    for _ in range(55):
        x, y = randomizer.randrange(14, 370), randomizer.randrange(22, 175)
        canvas.dot((x, y), randomizer.choice(["#588779", "#6c9b7d", "#3f726d"]))
    canvas.image.save(OUT / "SwampBackdrop.png")


def terrain():
    atlas = Cel(112, 352)
    palettes = [
        ("#695944", "#8a7958"), ("#534740", "#766452"), ("#3c524c", "#627e64"),
        ("#554947", "#705849"), ("#685050", "#9a6f60"), ("#455457", "#63776c"),
        ("#38434d", "#586d69"), ("#3a3645", "#5a515b"), ("#33545c", "#528381"),
        ("#3b4350", "#5a7372"),
    ]
    for row, (low, high) in enumerate(palettes):
        for column in range(4):
            tile = Cel(28, 32, "#26313b")
            tile.shape([(0, 9), (10, 1), (22, 2), (27, 11), (25, 25), (16, 30),
                        (5, 27), (1, 21)], low)
            tile.shape([(0, 8), (10, 2), (22, 3), (27, 11), (19, 9), (10, 9), (2, 14)], high)
            tile.line([(2, 23), (8, 21), (14, 24), (24, 20)], "#293f43", 2)
            tile.line([(6, 13), (14, 12), (22, 14)], high)
            randomizer = random.Random(680 + row * 4 + column)
            for _ in range(30):
                tile.dot((randomizer.randrange(28), randomizer.randrange(32)),
                         randomizer.choice([low, high, "#304348"]))
            atlas.image.paste(tile.image, (column * 28, row * 32))
    for column in range(4):
        plant = Cel(28, 32)
        if column == 0:
            for x in range(28):
                plant.line([(x, 0), (x, 12 + (x * 3) % 8)], "#47745e")
                plant.line([(x, 0), (x, 4 + x % 5)], "#77a676" if x % 3 else "#a0c177")
        else:
            for x, height in [(5, 15), (13, 22), (21, 12 + column * 3)]:
                y = 31 - height
                plant.line([(x - 2, 31), (x, y + 5), (x + 2, y)], "#51806a", 2)
                plant.line([(x, y + 8), (x + 5, y + 4), (x + 7, y + 3)], "#74a77a", 2)
                plant.oval((x, y - 1, x + 3, y + 5), "#b99160", "#624b45")
        atlas.image.paste(plant.image, (column * 28, 320))
    atlas.image.save(OUT / "SwampTerrain.png")


def styled_actor(kind, phase):
    """Reed Runner and Marsh Skimmer in the shared style, animated like the originals."""
    pic = Pic(32, 24, INK)
    pulse = phase in (4, 5)
    if kind == 0:
        for index, x in enumerate((10, 14, 20, 23)):
            toe = x + (-1 if index % 2 else 1) * (phase % 3)
            limb(pic, [(x, 14), (toe, 18), (toe + 3, 19)], "#a27756")
        pic.oval((4, 8, 21, 17), "#3a806e")
        pic.line([(7, 12), (11, 10), (17, 10)], LIME)
        pic.line([(22, 10), (26, 5), (29, 4)], "#bbad78")
        pic.oval((17, 9, 27, 17), "#558d69")
        pic.eye(23, 11, GOLD)
        if pulse:
            pic.stroke([(24, 16), (30, 14), (28, 12)], LIME)
    else:
        if phase % 2:
            pic.poly([(14, 11), (3, 4), (5, 2), (13, 5), (20, 11)], "#6d9d97")
        else:
            pic.poly([(14, 11), (7, 1), (12, 1), (20, 11)], "#91bdb1")
        pic.stroke([(8, 16), (12, 14), (18, 13)], "#3d786e", width=2)
        if phase % 2:
            pic.poly([(14, 11), (6, 17), (8, 19), (17, 16), (20, 12)], "#63858d")
        else:
            pic.poly([(14, 11), (2, 13), (4, 16), (16, 15), (20, 12)], "#5f9d98")
        pic.oval((17, 7, 27, 15), "#3b9b8c")
        pic.eye(22, 10, "#f3e1a8")
        pic.line([(25, 13), (29, 15)], LIME if pulse else "#8aa577")
    return pic.image


def actor(kind, phase):
    if kind < 2:
        return styled_actor(kind, phase)
    # The boss keeps its original drawing until the boss overhaul.
    cel = Cel(32, 24)
    pulse = phase in (4, 5)
    cel.shape([(3, 19), (3, 14), (7, 8), (13, 4), (23, 4), (29, 10), (31, 18), (27, 20), (7, 20)],
              "#37675f", INK)
    cel.shape([(5, 14), (10, 7), (15, 5), (23, 6), (27, 11), (24, 15), (14, 17)], "#60926f", INK)
    cel.line([(7, 11), (12, 8), (18, 7), (22, 8)], "#a0bf76", 2)
    cel.line([(9, 17), (14, 14), (16, 10)], "#294758", 2)
    for x in (8, 13, 18):
        cel.box((x, 11, x + 2, 12), LIME if pulse else TEAL)
    cel.oval((22, 12, 30, 17), "#30545b", INK)
    cel.dot((27, 13), GOLD)
    if pulse:
        cel.line([(29, 14), (31, 13)], LIME, 2)
    return cel.image


def actors():
    atlas = Cel(32 * 6, 24 * 3)
    for kind in range(3):
        for phase in range(6):
            atlas.image.paste(actor(kind, phase), (phase * 32, kind * 24))
    atlas.image.save(OUT / "SwampActors.png")


def vents():
    atlas = Cel(96, 32)
    for phase in range(3):
        cel = Cel(32, 32)
        cel.shape([(3, 24), (7, 20), (25, 20), (29, 24), (25, 27), (7, 27)], "#4f635a", INK)
        cel.oval((9, 20, 24, 23), "#203b41", INK)
        cel.line([(6, 24), (12, 24), (17, 25), (25, 24)], "#899974")
        if phase >= 1:
            for x, y in [(11, 16), (15, 12), (20, 15)]:
                cel.oval((x, y, x + 2, y + 2), LIME, "#558669")
        if phase == 2:
            cel.shape([(2, 18), (6, 13), (10, 13), (10, 7), (17, 4), (21, 8), (25, 8), (29, 15),
                       (27, 21), (21, 20), (17, 23), (11, 21), (6, 23)], "#5ca875", "#2e7465")
            cel.line([(7, 15), (11, 15), (12, 10), (16, 8), (22, 11), (25, 14)], LIME, 2)
            cel.line([(8, 19), (14, 17), (19, 18), (23, 16)], "#8dce78", 2)
        atlas.image.paste(cel.image, (phase * 32, 0))
    atlas.image.save(OUT / "SwampVents.png")



def flytraps():
    """A Venus flytrap rooted at the bottom edge: open with its lure lit, twitching, and shut."""
    atlas = Cel(96, 32)
    stem, leaf, jaw, inside, tooth = "#3f7a4b", "#5ca875", "#78b85a", "#e0607e", "#f2f0c8"
    for frame in range(3):
        cel = Cel(32, 32)
        cel.line([(16, 31), (16, 24)], stem, 2)
        cel.shape([(16, 30), (9, 27), (6, 29), (12, 31)], leaf, INK)
        cel.shape([(16, 30), (23, 27), (26, 29), (20, 31)], leaf, INK)
        if frame == 2:
            cel.shape([(8, 24), (10, 17), (16, 14), (22, 17), (24, 24), (16, 26)], jaw, INK)
            cel.line([(10, 21), (16, 18), (22, 21)], inside)
            for x in (10, 13, 16, 19, 22):
                cel.dot((x, 20 + (x % 2)), tooth)
        else:
            gap = 0 if frame == 0 else 2
            cel.shape([(16, 25), (7, 22 - gap), (5, 15 + gap), (9, 13 + gap), (15, 20)], jaw, INK)
            cel.shape([(16, 25), (25, 22 - gap), (27, 15 + gap), (23, 13 + gap), (17, 20)], jaw, INK)
            cel.shape([(15, 23), (9, 18), (16, 20), (23, 18), (17, 23)], inside)
            for x, y in ((5, 15 + gap), (7, 13 + gap), (9, 12 + gap), (27, 15 + gap), (25, 13 + gap), (23, 12 + gap)):
                cel.dot((x, y - 1), tooth)
            cel.dot((16, 21), GOLD if frame == 0 else LIME)
        atlas.image.paste(cel.image, (frame * 32, 0))
    atlas.image.save(OUT / "SwampFlytraps.png")


if __name__ == "__main__":
    backdrop()
    terrain()
    actors()
    vents()
    flytraps()
