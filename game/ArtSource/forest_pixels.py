"""Paint the original Forest textures and sprite sheets at their native size."""

from pathlib import Path
import random

from PIL import Image, ImageDraw

from sprite_style import Pic, limb, ramp

OUT =Path(__file__).resolve().parent.parent / "Assets/Resources/PixelArt"
OUT.mkdir(exist_ok=True)
INK = "#1b1029"
CREAM = "#ffe8bb"
GOLD = "#ffbd67"
CYAN = "#75eed8"
PINK = "#fa81a6"


class Cel:
    def __init__(self, width, height, fill=(0, 0, 0, 0)):
        self.image = Image.new("RGBA", (width, height), fill)
        self.draw = ImageDraw.Draw(self.image)

    def shape(self, points, color, outline=None):
        self.draw.polygon(points, color, outline)

    def box(self, bounds, color):
        self.draw.rectangle(bounds, color)

    def oval(self, bounds, color, outline=None):
        self.draw.ellipse(bounds, color, outline)

    def line(self, points, color, width=1):
        self.draw.line(points, color, width=width)

    def dot(self, point, color):
        self.draw.point(point, color)


def legs(cel, positions, color, phase):
    steps = (0, 2, -2, -1, 1, 0)
    for index, (x, y) in enumerate(positions):
        step = steps[phase] * (-1 if index % 2 else 1)
        foot = (x + step + (index - 1), 19 - (phase == 3) * 2)
        path = [(x, y), (x + step, y + 3), foot, (foot[0] + 1, foot[1])]
        cel.line(path, INK, 3)
        cel.line(path, color)
        cel.dot(path[1], GOLD)


def insect(row, pose):
    if row == 2:
        return dragonfly(pose)
    if row == 4:
        return worm(pose)
    cel = Cel(32, 24)
    swing = (0, 1, 0, -1, 0, 1)[pose]
    flash = pose == 5
    if row == 0:  # Jumping spider
        legs(cel, [(8, 13), (11, 13), (18, 13), (22, 13)], "#d27457", pose)
        cel.oval((5, 8, 17, 16), "#674c73", INK)
        cel.line([(7, 12), (9, 10), (13, 10)], "#c4848c")
        cel.oval((13, 5 + swing, 26, 16 + swing), "#dba076", INK)
        cel.oval((15, 6 + swing, 24, 13 + swing), "#f6d5a7")
        cel.oval((17, 8 + swing, 21, 13 + swing), INK)
        cel.oval((22, 8 + swing, 25, 12 + swing), INK)
        cel.dot((18, 9 + swing), CREAM)
        cel.dot((23, 9 + swing), CREAM)
        cel.line([(21, 14 + swing), (23, 14 + swing)], "#8b4656")
        if pose == 4:
            cel.line([(24, 13), (28, 12), (29, 9)], GOLD)
        if flash:
            cel.line([(4, 9), (2, 9), (0, 11)], "#cf9adc")
    elif row == 1:  # Beetle
        if flash:
            cel.oval((6, 6, 25, 19), "#2c777a", INK)
            cel.shape([(8, 13), (12, 7), (19, 7), (24, 12), (18, 12), (14, 16)], "#5bbaa6")
            cel.line([(8, 15), (13, 13), (15, 11), (18, 11)], CYAN)
            cel.line([(10, 18), (20, 18), (23, 16)], "#37536e")
        else:
            legs(cel, [(8, 15), (12, 15), (18, 15), (23, 14)], "#a66a54", pose)
            cel.oval((4, 7 + swing, 22, 17), "#387c79", INK)
            cel.shape([(6, 11 + swing), (11, 8 + swing), (18, 9 + swing), (19, 11), (13, 11), (8, 14)], "#77b9a0")
            cel.line([(7, 15), (12, 13), (18, 13), (20, 11)], "#244457")
            cel.oval((18, 10, 27, 17), "#455468", INK)
            cel.line([(24, 11), (27, 8), (27, 5), (25, 3)], CREAM, 2)
            cel.line([(20, 10), (21, 6), (20, 4)], "#c89971")
            cel.box((25, 14, 26, 15), INK)
            cel.dot((25, 14), CREAM)
            if pose == 4:
                cel.line([(26, 14), (29, 12), (30, 9)], CREAM)
    elif row == 3:  # Fire ant
        legs(cel, [(8, 13), (13, 14), (18, 14), (23, 12)], "#e6755d", pose)
        cel.oval((3, 8 + swing, 13, 16), "#9f4057", INK)
        cel.line([(5, 11 + swing), (8, 9 + swing), (11, 10 + swing)], "#ed8a63")
        cel.oval((11, 9, 21, 16), "#ae405c", INK)
        cel.line([(13, 11), (17, 10), (19, 12)], "#ed7659")
        cel.oval((18, 5 + swing, 27, 14 + swing), "#db625d", INK)
        cel.line([(20, 7 + swing), (23, 6 + swing), (25, 7 + swing)], "#f4a66c")
        cel.line([(23, 6 + swing), (25, 3 + swing), (26, 3 + swing)], "#dc775f")
        cel.line([(21, 6 + swing), (21, 3 + swing), (23, 1 + swing)], "#b75d5e")
        cel.oval((22, 8 + swing, 25, 11 + swing), INK)
        cel.dot((24, 9 + swing), CREAM)
        cel.line([(26, 12), (29, 12 - (pose in (4, 5)) * 3), (28, 14)], GOLD)
        if flash:
            cel.line([(4, 8), (7, 5), (11, 7)], "#f7bd6a")
            cel.dot((12, 5), "#fff0a9")
    return cel.image


def enemy(row, frame):
    if row == 0:
        return crawler(frame)
    if row == 1:
        return spore_moth(frame)
    # The charge boss keeps its original drawing until the boss overhaul.
    cel = Cel(32, 24)
    big = row == 2
    span = (1, 29) if big else (5, 25)
    lift = 3 if big else 0
    legs(cel, [(span[0] + 3, 14), (12, 14), (19, 13), (23, 13)], "#bb6b72", frame)
    cel.oval((span[0], 7 - lift, span[1], 17), "#744659", INK)
    for x in range(span[0] + 3, span[1] - 2, 5):
        cel.shape([(x - 2, 10), (x, 5 - lift), (x + 4, 4 - lift), (x + 5, 11), (x + 2, 14)],
                  "#ae695f", INK)
        cel.line([(x, 7 - lift), (x + 3, 6 - lift), (x + 4, 8 - lift)], "#db986a")
        if big:
            cel.line([(x + 1, 5 - lift), (x + 2, 2), (x + 3, 5 - lift)], GOLD)
    cel.oval((20, 9, 29, 17), "#3b2949", INK)
    cel.line([(23, 11), (26, 11)], GOLD if frame != 4 else CREAM)
    cel.line([(27, 14), (30, 13), (29, 16)], "#e2a56f")
    return cel.image


# Newer actors in the shared style (sprite_style.py). Each follows its original's animation: frame 0 idle,
# 1 and 2 moving, 3 airborne, 4 attack, 5 heavy, ability or (for enemies) the attack warning.

def styled_legs(pic, positions, color, phase):
    """The walk cycle from legs(), drawn in the shared style."""
    steps = (0, 2, -2, -1, 1, 0)
    for index, (x, y) in enumerate(positions):
        step = steps[phase] * (-1 if index % 2 else 1)
        foot = (x + step + (index - 1), 19 - (phase == 3) * 2)
        limb(pic, [(x, y), (x + step, y + 3), foot, (foot[0] + 1, foot[1])], color)


def dragonfly(frame):
    pic = Pic(32, 24)
    up = frame in (0, 2, 3)
    if up:
        wing = pic.poly([(17, 10), (10, 5), (3, 1), (7, 0), (15, 3), (20, 9)], "#9e92c4")
        pic.line([(7, 2), (13, 5), (16, 8)], wing["light"])
    else:
        pic.poly([(18, 10), (13, 8), (10, 2), (13, 1), (19, 7)], "#a2c6d7")
    pic.stroke([(16, 13), (9, 15), (2, 17)], "#428d96", width=2)
    for x in (5, 8, 11):
        pic.dot((x, 16 - (x - 2) // 4), ramp("#428d96")["deep"])
    if up:
        pic.poly([(17, 11), (6, 11), (2, 8), (9, 7), (19, 9)], "#6581b5")
    else:
        wing = pic.poly([(17, 11), (9, 17), (3, 18), (6, 14), (15, 10)], "#9b83bc")
        pic.line([(7, 16), (12, 14), (15, 12)], wing["light"])
    pic.stroke([(17, 14), (18, 17), (20, 19)], "#e6a278")
    pic.stroke([(20, 14), (22, 17), (24, 18)], "#e6a278")
    pic.oval((14, 8, 22, 15), "#428d96", glint=False)
    pic.oval((19, 6, 28, 15), "#62bdc4")
    pic.eye(23, 8, big=True)
    pic.line([(22, 6), (24, 3), (26, 2)], GOLD)
    if frame == 4:
        pic.line([(28, 14), (30, 12), (31, 9)], GOLD)
    if frame == 5:
        pic.line([(3, 12), (0, 12)], CYAN)
        pic.line([(6, 20), (2, 20)], "#4fbcae")
    return pic.image


def worm(frame):
    pic = Pic(32, 24)
    swing = (0, 1, 0, -1, 0, 1)[frame]
    lift = 3 if frame in (3, 5) else 0
    drag = swing + (1 if frame == 5 else 0)
    segments = [(4, 14, 11, 19), (7, 12 - swing, 15, 19), (11, 9 + swing, 20, 18),
                (15, 7 - lift, 24, 17), (19 + drag, 6 - lift, 29 + drag, 17 - lift)]
    for bounds, color in zip(segments, ("#a14f84", "#c56991", "#dd80a0", "#eb91a7", "#efa9ad")):
        pic.oval(bounds, color)
    pic.eye(25 + drag, 10 - lift)
    pic.line([(26 + drag, 14 - lift), (28 + drag, 13 - lift)], "#a45179")
    if frame == 5:
        pic.dot((29, 18), GOLD)
        pic.dot((27, 20), "#b58667")
        pic.dot((31, 16), "#eaa368")
    return pic.image


def crawler(frame):
    pic = Pic(32, 24)
    styled_legs(pic, [(8, 14), (12, 14), (19, 13), (23, 13)], "#bb6b72", frame)
    pic.oval((5, 7, 25, 17), "#744659")
    for x in (8, 13, 18):
        pic.poly([(x - 2, 10), (x, 5), (x + 4, 4), (x + 5, 11), (x + 2, 14)], "#ae695f", glint=False)
    pic.oval((20, 9, 29, 17), "#3b2949")
    glow = CREAM if frame == 4 else GOLD
    pic.eye(23, 11, glow)
    pic.eye(26, 11, glow)
    pic.stroke([(27, 15), (30, 14)], "#e2a56f")
    return pic.image


def spore_moth(frame):  # front-facing, lavender wings and a red mask
    pic = Pic(32, 24)
    height = (3, 8, 13, 6, 8, 4)[frame]
    for side in (-1, 1):
        x = 16 + 13 * side
        wing = pic.poly([(16, 13), (16 + 6 * side, height), (x, height), (x + side * 2, height + 5),
                         (16 + 7 * side, 15)], "#a688bb")
        pic.line([(16 + 9 * side, height + 2), (16 + 6 * side, height + 4), (16 + 3 * side, 12)], wing["light"])
        pic.dot((16 + 10 * side, height + 4), "#583d7b")
        pic.dot((16 + 11 * side, height + 4), "#583d7b")
    pic.oval((12, 8, 20, 20), "#734c85")
    pic.oval((11, 10, 21, 16), "#c45b78", glint=False)
    pic.eye(13, 12, GOLD)
    pic.eye(17, 12, GOLD)
    return pic.image


def actors():
    image = Image.new("RGBA", (6 * 32, 8 * 24))
    for row in range(8):
        for pose in range(6):
            frame = insect(row, pose) if row < 5 else enemy(row - 5, pose)
            image.paste(frame, (pose * 32, row * 24))
    image.save(OUT / "Actors.png")


def rock_tiles():
    tones = [
        ("#60424c", "#7d5359"), ("#443148", "#5b3c50"), ("#393548", "#51455a"),
        ("#65394c", "#88505d"), ("#674153", "#7a5264"), ("#543061", "#794574"),
        ("#464054", "#63526b"), ("#3d384f", "#524d67"), ("#303d59", "#516278"),
        ("#33253d", "#594051"),
    ]
    atlas = Image.new("RGBA", (4 * 28, 11 * 32))
    for row, (mid, bright) in enumerate(tones):
        for variation in range(4):
            rng = random.Random(700 + row * 17 + variation)
            cel = Cel(28, 32, "#24192f")
            cel.shape([(0, 9), (7, 6), (13, 0), (25, 3), (27, 10), (26, 24), (17, 29), (10, 30), (1, 22)], mid)
            cel.shape([(0, 9), (7, 6), (13, 0), (25, 3), (27, 10), (22, 8), (15, 6), (12, 9), (7, 10), (1, 13)], bright)
            cel.line([(3, 24), (10, 26), (14, 22), (19, 21), (22, 15), (27, 13)], "#302235", 2)
            cel.line([(15, 8), (17, 12), (16, 16), (20, 18)], "#38263d")
            for _ in range(35):
                x, y = rng.randrange(28), rng.randrange(32)
                cel.dot((x, y), rng.choice((mid, bright, "#2f243b")))
            for _ in range(6):
                x, y = rng.randrange(4, 24), rng.randrange(10, 26)
                cel.dot((x, y), rng.choice(("#a07762", "#785063", "#b58766")))
            atlas.paste(cel.image, (variation * 28, row * 32))
    grass = Cel(28, 32)
    heights = (11, 13, 15, 17, 14, 10, 12, 14, 13, 9, 15, 19, 22, 18, 15, 14, 13, 17, 21, 17, 13, 15, 18, 17, 14, 15, 12, 14)
    for x, height in enumerate(heights):
        grass.line([(x, 0), (x, height + 4)], "#776141")
        grass.line([(x, 0), (x, height)], "#6e804b" if x % 4 else "#7d964f")
        grass.line([(x, 0), (x, 5 + (x % 4) * 2)], "#b3b65d" if x % 6 else "#d3ca72")
    atlas.paste(grass.image, (0, 320))
    for column in range(1, 4):
        cel = Cel(28, 32)
        if column == 1:
            cel.line([(12, 31), (13, 23), (14, 17), (16, 11)], "#57876a", 2)
            for y in (15, 21, 26):
                cel.shape([(14, y + 2), (7, y - 1), (5, y - 4), (10, y - 2)], "#79995e")
                cel.shape([(14, y), (18, y - 3), (22, y - 5), (19, y - 1)], "#587c5c")
        else:
            color, shine = ("#97598f", "#d686b0") if column == 2 else ("#458f91", "#77bfb0")
            for x, y in [(9, 22), (17, 16), (22, 25)]:
                cel.line([(x, 31), (x, y + 3)], "#4c445f", 2)
                cel.shape([(x - 4, y + 4), (x - 2, y), (x + 2, y), (x + 4, y + 4)], color)
                cel.line([(x - 2, y + 1), (x + 1, y)], shine)
        atlas.paste(cel.image, (column * 28, 320))
    atlas.save(OUT / "Terrain.png")


# Pickups, chests and the shop pedestal in the shared style. Props cells 0-5 follow LevelFeatureKind; cells 16
# and 17 are the opened chests.

def crystal(pic, color, w):
    """The original pickup gem outline, shaded, with a facet down its middle. The HUD crops x 12-20, y 9-24."""
    tones = pic.poly([(16, 9), (16 + w, 15), (17 + w, 19), (16, 24), (15 - w, 19), (16 - w, 15)], color)
    pic.line([(16, 11), (16, 22)], tones["shade"])


def chest_body(pic, color, lock):
    tones = pic.rect((4, 14, 27, 22), color, glint=False)
    pic.line([(6, 20), (25, 20)], tones["shade"])
    pic.rect((3, 13, 28, 15), "#d69669", glint=False)
    pic.rect((13, 12, 18, 18), "#c58965", glint=False)
    pic.rect((14, 13, 17, 16), lock, outline=False, glint=False)


def open_lid(pic, color, glow):
    tones = pic.rect((4, 2, 27, 9), color, glint=False)
    pic.line([(6, 4), (25, 4)], tones["light"])
    pic.rect((4, 8, 27, 13), "#2b1d38", light=False, glint=False)
    pic.poly([(6, 13), (9, 9), (22, 9), (25, 13)], glow)


def reward_horns(pic):
    for side in (-1, 1):
        pic.poly([(16 + side * 9, 10), (16 + side * 14, 3), (16 + side * 13, 10)], "#9fe0c8", glint=False)


def pedestal(pic):
    """A carved stone plinth for a shop item to hover over. Its foot sits on row 25, where the stall stood."""
    pic.poly([(6, 25), (8, 21), (23, 21), (25, 25)], "#574663", glint=False)
    tones = pic.rect((11, 13, 20, 22), "#6a587a", glint=False)
    for x in (12, 19):
        pic.line([(x, 15), (x, 20)], tones["shade"])
    pic.poly([(15, 14), (17, 16), (17, 18), (15, 20), (14, 18), (14, 16)], GOLD, glint=False)
    pic.dot((15, 15), CREAM)
    tones = pic.poly([(5, 13), (6, 10), (25, 10), (26, 13)], "#84709a", glint=False)
    pic.line([(7, 10), (23, 10)], tones["glint"])
    for x, y in ((7, 21), (8, 20), (22, 21), (23, 22)):
        pic.dot((x, y), "#82b58a")


def styled_prop(index):
    pic = Pic(32, 32)
    if index == 0:  # Nektar
        crystal(pic, GOLD, 3)
    elif index == 1:  # Heart sap
        crystal(pic, PINK, 4)
        pic.line([(14, 17), (18, 17)], CREAM)
        pic.line([(16, 15), (16, 19)], CREAM)
    elif index == 2:  # Energy sap
        crystal(pic, CYAN, 4)
        pic.line([(17, 13), (15, 17), (18, 17), (16, 21)], CREAM)
    elif index == 3:  # Shop pedestal
        pedestal(pic)
    elif index == 4:  # Chest
        pic.poly([(4, 13), (7, 8), (24, 8), (27, 13)], "#703e5a")
        chest_body(pic, "#703e5a", CYAN)
    elif index == 5:  # Boss reward
        reward_horns(pic)
        pic.poly([(4, 13), (7, 8), (24, 8), (27, 13)], GOLD)
        chest_body(pic, "#c58a3f", PINK)
    elif index == 16:  # Chest, opened
        open_lid(pic, "#5a3448", GOLD)
        chest_body(pic, "#703e5a", CREAM)
        pic.spark(2, 6, CREAM)
        pic.spark(29, 4, CREAM)
    else:  # Boss reward, opened
        reward_horns(pic)
        open_lid(pic, "#a8702f", "#ffe08a")
        chest_body(pic, "#c58a3f", CREAM)
        pic.spark(2, 12, CREAM)
        pic.spark(29, 12, CREAM)
    return pic.image


def props():
    atlas = Image.new("RGBA", (8 * 32, 3 * 32))
    for index in (0, 1, 2, 3, 4, 5, 16, 17):
        atlas.paste(styled_prop(index), ((index % 8) * 32, (index // 8) * 32))
    for index in range(6, 16):
        cel = Cel(32, 32)
        if index in (6, 7):  # Portal, lit or locked
            tint = GOLD if index == 6 else "#a46b80"
            cel.shape([(6, 28), (6, 12), (9, 5), (15, 2), (21, 4), (25, 9), (25, 28)], "#563a53", INK)
            cel.line([(8, 26), (8, 12), (11, 6), (15, 4), (20, 6), (23, 12), (23, 26)], tint, 2)
            cel.shape([(11, 25), (11, 12), (13, 8), (17, 7), (21, 12), (21, 25)], "#284357")
            cel.line([(13, 24), (20, 20), (14, 17), (19, 14)], "#75b8ac" if index == 6 else "#75485a", 2)
            cel.box((5, 27, 26, 29), "#9a6762")
            cel.dot((15, 3), CREAM)
        elif index in (8, 9, 10):  # Thorn trap phases
            cel.shape([(4, 22), (9, 19), (22, 19), (27, 22), (25, 25), (6, 25)], "#694b64", INK)
            cel.line([(6, 22), (12, 21), (20, 21), (25, 22)], "#a77471")
            for x in (10, 16, 22):
                tip = 19 if index == 8 else 15 if index == 9 else 8
                cel.shape([(x - 3, 22), (x, tip), (x + 2, 20), (x + 2, 22)],
                          "#be777a" if index == 8 else GOLD if index == 9 else "#f8677f", INK)
                if index != 8:
                    cel.dot((x, tip + 2), CREAM)
        elif index == 11:  # Dart
            cel.shape([(11, 15), (18, 13), (22, 15), (18, 18), (10, 18)], "#82b58a", INK)
            cel.line([(13, 16), (19, 15)], "#dfebac")
        elif index == 12:  # Warning
            cel.shape([(16, 6), (22, 19), (10, 19)], GOLD, INK)
            cel.line([(16, 11), (16, 14)], INK)
            cel.dot((16, 16), INK)
        elif index == 13:  # Blast
            cel.oval((2, 2, 29, 29), (255, 158, 90, 80), (255, 208, 108, 225))
            cel.oval((6, 6, 25, 25), (235, 98, 113, 55), (255, 139, 96, 177))
            for point in ((4, 11), (24, 5), (15, 17), (20, 25), (10, 3), (21, 11)):
                cel.dot(point, CREAM)
        elif index == 14:  # Melee slash
            cel.shape([(16, 7), (24, 9), (26, 13), (25, 19), (20, 24), (15, 24), (22, 18), (23, 13)], GOLD)
            cel.line([(17, 7), (24, 10), (26, 13), (25, 17)], CREAM)
        else:  # Side path marker
            cel.line([(11, 17), (16, 12), (21, 17)], "#8cccab", 2)
            cel.dot((16, 19), "#dbe7ad")
        atlas.paste(cel.image, ((index % 8) * 32, (index // 8) * 32))
    atlas.save(OUT / "Props.png")


def hazards():
    """Bramble tiles, half a unit wide and standing on their bottom edge: four middle variants, then the left
    and right end caps. Hanging brambles reuse them flipped. The tangle is as tall as the half-unit hit box,
    and thorns lean out above it."""
    atlas = Image.new("RGBA", (6 * 8, 16))
    dark, mid, vine, thorn = "#341f33", "#55354d", "#9a6a6c", "#ead0a8"
    tops = (
        (9, 8, 8, 9, 9, 8, 8, 9),
        (9, 9, 8, 8, 9, 9, 8, 8),
        (8, 8, 9, 9, 8, 8, 9, 9),
        (8, 9, 9, 8, 8, 9, 9, 8),
        (14, 13, 12, 11, 10, 9, 9, 9),
    )
    # Each variant: where a thorn sprouts and which way it leans.
    thorns = (((1, -1), (5, 1)), ((2, 1), (6, -1)), ((0, 1), (4, -1)), ((3, -1), (7, -1)), ((5, -1),))
    for index in range(6):
        cel = Cel(8, 16)
        cap = min(index, 4)
        heights = tops[cap]
        flip = index == 5
        if flip:
            heights = tuple(reversed(heights))
        for x, top in enumerate(heights):
            cel.line([(x, top), (x, 15)], dark)
            cel.dot((x, top), INK)
            if (x + index) % 3 == 0 and top + 3 < 15:
                cel.dot((x, top + 3), mid)
        # Two vines weave through the tangle, crossing near the middle of the tile.
        for x, top in enumerate(heights):
            rise = (0, 1, 2, 2, 2, 2, 1, 0)[x]
            cel.dot((x, min(15, top + 1 + rise)), vine)
            cel.dot((x, min(15, top + 5 - rise)), vine)
        # Thorns lean out from the top of the tangle: a pale base and a cream point.
        for x, lean in thorns[cap]:
            if flip:
                x, lean = 7 - x, -lean
            top = heights[x]
            tip = x + lean
            cel.dot((x, top), thorn)
            cel.dot((x, top - 1), thorn)
            if 0 <= tip < 8:
                cel.dot((tip, top - 2), CREAM)
        if index not in (3, 4, 5):
            bx = (index * 3 + 2) % 7
            cel.dot((bx, heights[bx] + 4), PINK)
        atlas.paste(cel.image, (index * 8, 0))
    atlas.save(OUT / "ForestHazards.png")


def cavern():
    width, height = 384, 192
    cel = Cel(width, height, "#171225")
    rng = random.Random(824)
    rings = ("#171225", "#1b1933", "#202043", "#263253", "#29455f", "#30586b", "#356572")
    for idx, color in enumerate(rings[1:], 1):
        # Offset irregular layers around the distant blue hollow.
        sx, sy = 197 - idx * 24, 116 - idx * 13
        cx, cy = 189 + idx * 3, 113
        outline = [(cx - sx, cy), (cx - sx * 3 // 4, cy - sy * 2 // 3), (cx - sx // 2, cy - sy),
                   (cx + sx // 4, cy - sy * 7 // 8), (cx + sx * 3 // 4, cy - sy * 2 // 3),
                   (cx + sx, cy + sy // 7), (cx + sx * 2 // 3, cy + sy * 3 // 4),
                   (cx - sx // 9, cy + sy), (cx - sx * 4 // 5, cy + sy * 2 // 3)]
        cel.shape(outline, color)
    for _ in range(1300):
        x, y = rng.randrange(width), rng.randrange(height)
        color = cel.image.getpixel((x, y))
        adjust = rng.choice((-3, 3, 5))
        cel.dot((x, y), tuple(max(0, min(255, channel + adjust)) for channel in color[:3]) + (255,))
    # Broken structures and trunks at separate depths.
    for x, side, base in [(69, 1, 154), (132, 1, 165), (207, -1, 179), (254, -1, 164), (318, -1, 157)]:
        w = rng.randrange(8, 17)
        y = rng.randrange(13, 57)
        cel.shape([(x-w, 0), (x+w, 0), (x + w//2, y), (x, y+27), (x+w, base),
                   (x-w*2, 191), (x-w, base), (x-w, y+11)], "#211c36")
        cel.line([(x, y+38), (x - side * 12, y + 22), (x - side * 24, y + 17)], "#211c36", 3)
        cel.line([(x + side * 3, y+24), (x + side * 17, y + 12), (x + side * 23, y + 13)], "#211c36", 2)
    for x, y, w in [(119, 139, 16), (137, 143, 10), (180, 151, 23), (218, 143, 11), (236, 154, 19), (263, 146, 14)]:
        cel.shape([(x, y+5), (x, y-5), (x+4, y-5), (x+5, y-8), (x+w, y-8), (x+w+4, y+20), (x-3, y+20)], "#28354a")
        for j in range(3):
            cel.box((x+3+j*4, y+6, x+4+j*4, y+7), "#47858b")
    cel.shape([(0, 0), (132, 0), (106, 11), (70, 17), (49, 40), (28, 46),
               (40, 72), (23, 97), (37, 127), (26, 156), (0, 191)], "#170e23")
    cel.shape([(249, 0), (383, 0), (383, 191), (357, 167), (349, 142), (360, 118),
               (343, 100), (350, 66), (326, 49), (333, 36), (303, 25)], "#171024")
    cel.shape([(0, 180), (25, 169), (47, 163), (93, 171), (121, 158), (163, 178),
               (208, 169), (245, 182), (296, 158), (318, 170), (345, 156), (383, 171), (383, 191), (0, 191)], "#171126")
    for x, end in [(31, 87), (76, 50), (104, 36), (299, 52), (326, 67), (353, 96)]:
        y = rng.randint(6, 19)
        points = [(x, y)]
        while y < end:
            x += rng.choice((-2, -1, 0, 1))
            y += rng.randint(5, 9)
            points.append((x, y))
        cel.line(points, "#34304b", 2)
        for x, y in points[1:]:
            cel.shape([(x, y), (x-4, y-2), (x-6, y-5), (x-3, y-4)], "#424459")
    for x, y in [(45, 134), (54, 139), (83, 153), (114, 155), (286, 143), (306, 125), (339, 137), (357, 157)]:
        main, rim = ("#5e4c7e", "#826394") if x < 150 else ("#376571", "#599791")
        cel.line([(x, y+19), (x+1, y+6), (x+3, y+1)], "#443d60", 2)
        cel.shape([(x-4, y+3), (x-1, y), (x+4, y-1), (x+8, y+3), (x+6, y+5), (x-3, y+5)], main)
        cel.line([(x-3, y+3), (x+5, y+2)], rim)
    for index in range(45):
        x, y = rng.randrange(37, 347), rng.randrange(37, 155)
        cel.dot((x, y), ("#376b77", "#5e5378", "#4e5d7e", "#7a707f")[index % 4])
    cel.image.save(OUT / "Cavern.png")


def font():
    alphabet = {
        "A":"0e11111f111111","B":"1e11111e11111e","C":"0e11101010110e","D":"1e11111111111e",
        "E":"1f10101e10101f","F":"1f10101e101010","G":"0e11101711110f","H":"1111111f111111",
        "I":"0e04040404040e","J":"0702020202120c","K":"11121418141211","L":"1010101010101f",
        "M":"111b1515111111","N":"11191513111111","O":"0e11111111110e","P":"1e11111e101010",
        "Q":"0e11111115120d","R":"1e11111e141211","S":"0f10100e01011e","T":"1f040404040404",
        "U":"1111111111110e","V":"11111111110a04","W":"11111115151b11","X":"11110a040a1111",
        "Y":"11110a04040404","Z":"1f01020408101f","0":"0e11131519110e","1":"040c040404040e",
        "2":"0e11010204081f","3":"1e01010601011e","4":"02060a121f0202","5":"1f10101e01011e",
        "6":"0e10101e11110e","7":"1f010204080808","8":"0e11110e11110e","9":"0e11110f01010e",
        ":":"00040400040400",".":"00000000000004",",":"00000000000408","/":"01010204081010",
        "!":"04040404040004","?":"0e110204040004","+":"0004041f040400","-":"0000001f000000",
        "(":"02040808080402",")":"08040202020408","%":"11110204081111","'":"04040800000000",
        " ":"00000000000000","[":"0e08080808080e","]":"0e02020202020e","_":"0000000000001f",
    }
    cel = Cel(96, 32)
    for character, pattern in alphabet.items():
        code = ord(character) - 32
        ox, oy = (code % 16) * 6, (code // 16) * 8
        for y, row in enumerate(bytes.fromhex(pattern)):
            for x in range(5):
                if row & (1 << (4 - x)):
                    cel.dot((ox + x, oy + y), "#ffffff")
    cel.image.save(OUT / "Glyphs.png")


if __name__ == "__main__":
    actors()
    rock_tiles()
    props()
    hazards()
    cavern()
    font()
