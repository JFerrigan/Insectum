"""Paint the Caves biome's original textures at native size."""

import random

from forest_pixels import Cel, OUT

INK = "#14142b"
ICE = "#8fdbe4"
VIOLET = "#c68bf5"
WHITE = "#e1eaff"


def backdrop():
    cel = Cel(384, 192, "#171832")
    cel.shape([(0, 15), (66, 0), (140, 0), (208, 20), (277, 0), (384, 0), (384, 192), (0, 192)], "#202345")
    cel.shape([(26, 80), (103, 20), (183, 32), (251, 16), (365, 86), (350, 170), (100, 184)], "#28365a")
    cel.shape([(95, 88), (151, 47), (208, 52), (258, 43), (303, 100), (286, 143), (154, 151)], "#33456e")
    for x, offset in [(22, 0), (63, 27), (118, 43), (163, 6), (224, 48), (279, 20), (327, 3), (369, 31)]:
        cel.shape([(x - 17, 0), (x + 19, 0), (x + 16, 28 + offset), (x + 2, 73 + offset),
                   (x - 12, 39 + offset)], "#1c2545")
        cel.line([(x - 5, 7), (x + 5, 29 + offset), (x + 2, 64 + offset)], "#28355b", 2)
    cel.shape([(0, 170), (46, 159), (82, 175), (127, 154), (163, 164), (212, 146), (247, 164), (298, 155),
               (344, 172), (384, 158), (384, 192), (0, 192)], "#181d36")
    rng = random.Random(8761)
    for x, base, height, width in [(18, 188, 56, 13), (39, 178, 33, 9), (68, 182, 46, 12),
                                    (130, 171, 39, 11), (191, 185, 34, 8), (265, 177, 28, 9),
                                    (310, 189, 59, 15), (342, 181, 39, 11), (364, 187, 53, 12)]:
        y = base - height
        light, dark = rng.choice([("#3b658c", "#2f4f75"), ("#656093", "#4d4b77"), ("#426d89", "#314968")])
        cel.shape([(x - width, base), (x - width + 3, y + 16), (x, y), (x + width, y + 21),
                   (x + width - 3, base)], dark, "#243554")
        cel.shape([(x - width + 3, y + 16), (x, y), (x + 3, y + 14), (x - 3, base), (x - width, base)], light)
        cel.line([(x, y + 2), (x - 4, y + 13), (x - 2, y + 22)], "#6f8aac")
    for _ in range(155):
        x, y = rng.randrange(384), rng.randrange(20, 188)
        cel.dot((x, y), rng.choice(["#445279", "#53658e", "#59618c", "#222b4a"]))
    cel.image.save(OUT / "CavesBackdrop.png")


def terrain():
    atlas = Cel(112, 352)
    material = [
        ("#5a5265", "#75697d"), ("#514a63", "#695c77"), ("#414a64", "#5c637f"),
        ("#52465f", "#77647f"), ("#5c4863", "#8b6b85"), ("#54476f", "#7765a0"),
        ("#414e70", "#65769d"), ("#333d5c", "#52617f"), ("#404b69", "#667da1"),
        ("#3b4262", "#687093"),
    ]
    for row, (dark, bright) in enumerate(material):
        for variant in range(4):
            tile = Cel(28, 32, "#28283f")
            tile.shape([(0, 8), (11, 0), (24, 3), (27, 12), (24, 26), (18, 31), (6, 27), (0, 21)], dark)
            tile.shape([(0, 8), (11, 0), (24, 3), (27, 12), (17, 8), (8, 9), (1, 15)], bright)
            tile.shape([(0, 20), (9, 16), (15, 21), (24, 16), (24, 26), (18, 31), (6, 27)],
                       "#303249")
            tile.line([(2, 23), (7, 22), (14, 24), (21, 17), (26, 16)], "#292941")
            rng = random.Random(2012 + row * 4 + variant)
            for _ in range(35):
                tile.dot((rng.randrange(28), rng.randrange(32)), rng.choice([dark, bright, "#3b3b56"]))
            if row in (6, 7, 9):
                x, y = (6 + 4 * variant, 12 + 2 * variant)
                tile.line([(x, y), (x + 3, y - 2), (x + 5, y), (x + 9, y - 1)],
                          ("#647eaa", "#7067a3", "#619da7", "#7a6ca6")[variant])
            atlas.image.paste(tile.image, (variant * 28, row * 32))

    for column in range(4):
        stones = Cel(28, 32)
        if column == 0:
            for x in range(28):
                stones.line([(x, 0), (x, 9 + (x * 7) % 9)], "#5a6392")
                stones.line([(x, 0), (x, 3 + x % 4)], "#868dc2")
        else:
            hues = [("#6f70b3", "#a8a2e9"), ("#617fae", ICE), ("#775faa", VIOLET)]
            base, rim = hues[column - 1]
            for x, y, width in [(6, 20, 4), (14, 10 + column, 5), (22, 19 - column, 4)]:
                stones.shape([(x - width, 31), (x - width, y + 5), (x, y), (x + width, y + 5),
                              (x + width - 1, 31)], base, INK)
                stones.line([(x, y + 1), (x - 2, y + 5), (x - 1, 30)], rim)
        atlas.image.paste(stones.image, (column * 28, 320))
    atlas.image.save(OUT / "CavesTerrain.png")


def actor(kind, frame):
    cel = Cel(32, 24)
    alert = frame in (4, 5)
    if kind == 0:
        for x, shift in [(8, -1), (14, 1), (20, -1)]:
            toe = x + shift * (frame % 3)
            cel.line([(x, 13), (toe - 2, 17), (toe + 4, 19)], "#54628e", 3)
            cel.line([(x, 13), (toe - 2, 17), (toe + 4, 19)], "#a8a0d5")
        cel.shape([(4, 15), (6, 10), (12, 6), (19, 7), (20, 15), (15, 17), (8, 17)], "#6576aa", INK)
        cel.line([(7, 12), (12, 8), (17, 9)], ICE)
        cel.oval((17, 9, 26, 16), "#746caa", INK)
        cel.dot((23, 11), WHITE)
        if alert:
            cel.line([(24, 14), (29, 15), (30, 12)], ICE, 2)
    elif kind == 1:
        top, bottom = ((2, 18) if frame % 2 else (6, 15))
        cel.shape([(18, 12), (12, top), (6, top + 1), (8, top + 6), (15, 12)], "#7d83c7", INK)
        cel.shape([(17, 12), (8, bottom), (4, bottom + 2), (11, bottom + 3), (18, 14)], "#77669e", INK)
        cel.oval((10, 9, 25, 16), "#7255aa", INK)
        cel.oval((19, 10, 26, 15), VIOLET, INK)
        cel.dot((23, 11), WHITE)
        cel.line([(10, 15), (5, 17)], ICE if alert else "#6caaae")
    else:
        cel.shape([(1, 18), (4, 12), (8, 9), (20, 8), (24, 10), (30, 13), (31, 19), (21, 20),
                   (6, 20)], "#514f82", INK)
        for x, y in [(8, 8), (15, 2), (21, 5)]:
            cel.shape([(x - 3, 13), (x - 3, y + 3), (x, y), (x + 4, y + 4), (x + 3, 13)],
                      "#7968ad", INK)
            cel.line([(x, y + 1), (x - 1, y + 4), (x, 11)], VIOLET if alert else "#afa3e2")
        cel.line([(4, 17), (9, 15), (15, 17), (20, 14)], "#8a8ab9", 2)
        cel.oval((23, 12, 30, 18), "#3e466e", INK)
        cel.dot((27, 13), ICE)
    return cel.image


def actors():
    atlas = Cel(192, 72)
    for kind in range(3):
        for frame in range(6):
            atlas.image.paste(actor(kind, frame), (32 * frame, 24 * kind))
    atlas.image.save(OUT / "CavesActors.png")


def hazards():
    atlas = Cel(128, 32)
    for frame in range(4):
        cel = Cel(32, 32)
        if frame < 2:
            crystal = "#836cba" if frame else "#6475a6"
            cel.shape([(8, 1), (24, 1), (24, 8), (19, 16), (16, 25), (12, 16), (9, 10)],
                      crystal, INK)
            cel.line([(10, 4), (15, 3), (17, 8), (16, 20)], VIOLET if frame else "#a2bce0")
            if frame:
                for x, y in [(11, 22), (20, 24), (15, 28), (7, 18), (22, 20)]:
                    cel.dot((x, y), WHITE)
        elif frame == 2:
            cel.shape([(4, 23), (7, 16), (12, 18), (16, 8), (22, 14), (23, 20), (28, 21), (29, 25),
                       (4, 26)], "#7e6cb5", INK)
            cel.line([(10, 21), (12, 22), (17, 15), (19, 18), (23, 22)], VIOLET, 2)
            cel.line([(5, 26), (29, 26)], ICE)
        else:
            cel.shape([(2, 16), (11, 14), (19, 10), (23, 15), (29, 17), (21, 19), (5, 19)], "#7160b6", INK)
            cel.line([(4, 17), (13, 16), (19, 12), (22, 16), (27, 17)], ICE, 2)
            cel.dot((19, 12), WHITE)
        atlas.image.paste(cel.image, (frame * 32, 0))
    atlas.image.save(OUT / "CavesHazards.png")



def spikes():
    """Stalagmite spike tiles, half a unit wide and standing on their bottom edge: four middle variants, then the
    left and right end caps, laid out like the Forest bramble sheet."""
    atlas = Cel(48, 16)
    rock, shade = "#4a4380", "#332e5f"
    tips = (
        ((1, 9), (4, 8), (6, 10)),
        ((2, 8), (5, 9), (7, 11)),
        ((0, 10), (3, 8), (6, 9)),
        ((1, 8), (4, 10), (6, 8)),
        ((3, 12), (6, 9)),
    )
    for index in range(6):
        cel = Cel(8, 16)
        spikes_here = tips[min(index, 4)]
        if index == 5:
            spikes_here = tuple((7 - x, top) for x, top in tips[4])
        cel.box((0, 14, 7, 15), shade)
        for x, top in spikes_here:
            cel.shape([(x - 1, 15), (x, top), (x + 1, 15)], rock, INK)
            cel.line([(x, top + 1), (x, 14)], ICE)
            cel.dot((x, top), WHITE)
        cel.line([(0, 15), (7, 15)], INK)
        atlas.image.paste(cel.image, (index * 8, 0))
    atlas.image.save(OUT / "CavesSpikes.png")


if __name__ == "__main__":
    backdrop()
    terrain()
    actors()
    hazards()
    spikes()
