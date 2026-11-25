"""Paint Old Oakhorn, the Forest stag beetle boss, at the game's native pixel scale.

The beetle is a parametric side view facing right. Each animation frame is a pose (body height and
pitch, head tilt, jaw opening, feet, wing cases, wing flap), drawn straight into its own cell so every
frame stays crisp instead of being rotated at runtime.

Writes:
  Assets/Resources/PixelArt/ForestBoss.png      the frames, 16 per row, 128 x 112 each
  Assets/Resources/PixelArt/ForestBossGlow.png  the jaws alone in white, for telegraph glows
  Assets/Resources/PixelArt/BossFx.png          shared boss effects (dust, sparks, shards, shadows, waves)
  Builds/BossPreviews/forest.png                a contact sheet for review, with the player for scale

The frame order here must match StagBeetleView.Anims in Assets/Scripts/Bosses/StagBeetleView.cs.
"""

import math
from pathlib import Path

from PIL import Image, ImageDraw

from forest_pixels import OUT

CELL_W, CELL_H = 128, 112
GROUND = 100          # the cell row the feet stand on; the sprite pivot
CENTER = 64           # the cell column under the body's center (the boss's Position)
COLUMNS = 16
PREVIEWS = Path(__file__).resolve().parent.parent / "Builds/BossPreviews"

INK = (27, 16, 41, 255)
BARK_DARK = (62, 38, 24, 255)
BARK = (104, 67, 38, 255)
BARK_LIGHT = (142, 95, 53, 255)
BARK_SHINE = (196, 146, 84, 255)
GRAIN = (76, 47, 28, 255)
MOSS = (100, 128, 52, 255)
MOSS_LIGHT = (150, 178, 77, 255)
LICHEN = (190, 198, 140, 255)
THORAX = (80, 49, 30, 255)
THORAX_LIGHT = (130, 84, 48, 255)
HEAD = (72, 44, 28, 255)
JAW = (142, 66, 34, 255)
JAW_LIGHT = (204, 116, 62, 255)
JAW_DARK = (92, 42, 23, 255)
LEG = (98, 61, 38, 255)
LEG_FAR = (58, 38, 28, 255)
JOINT = (168, 116, 66, 255)
EYE = (255, 189, 103, 255)
BELLY = (92, 60, 38, 255)
SEGMENT = (60, 37, 24, 255)
MEMBRANE = (230, 192, 122, 165)
MEMBRANE_EDGE = (168, 116, 66, 225)
BLUR = (240, 220, 172, 75)
WHITE = (255, 255, 255, 255)


# ---------- geometry ----------
def rotate(point, pivot, degrees):
    a = math.radians(degrees)
    x, y = point[0] - pivot[0], point[1] - pivot[1]
    return (pivot[0] + x * math.cos(a) - y * math.sin(a), pivot[1] + x * math.sin(a) + y * math.cos(a))


def ellipse(cx, cy, rx, ry, start=0, end=360, steps=28):
    return [(cx + rx * math.cos(math.radians(start + (end - start) * i / steps)),
             cy + ry * math.sin(math.radians(start + (end - start) * i / steps))) for i in range(steps + 1)]


class Pose:
    """One frame's pose. Lengths are pixels, angles degrees, y up from the ground."""

    def __init__(self, **values):
        self.height = 20          # body origin above the ground
        self.shift = 0            # body origin's x offset
        self.pitch = 0            # nose up is positive, around the rear underside
        self.head = 0             # head tilt, nose up positive
        self.jaw = 0.0            # 0 closed, 1 wide open
        self.feet = None          # six (x, y) feet in ground space, near legs first: front, mid, rear
        self.wings = 0.0          # 0 cases shut, 1 fully lifted
        self.flap = None          # None, or a wing-beat frame 0-3
        self.squash = 1.0         # vertical scale about the ground
        self.antenna = 0          # antenna twitch
        self.splay = 0.0          # death: legs spread flat
        self.shake = 0
        self.glow_body = False    # the glow mask covers the whole body, not just the jaws
        self.__dict__.update(values)


STAND_FEET = [(24, 0), (3, 0), (-29, 0), (28, 0), (8, 0), (-24, 0)]


class Beetle:
    def __init__(self, pose, mask=False, shell_only=False):
        self.p = pose
        self.mask = mask
        # Draw only the solid body (no legs or wing membranes), for measuring hurtbox coverage.
        self.shell_only = shell_only
        self.image = Image.new("RGBA", (CELL_W, CELL_H), (0, 0, 0, 0))
        self.draw = ImageDraw.Draw(self.image)

    # Body-local (x forward, y up) to cell pixels.
    def body(self, point):
        p = self.p
        x, y = rotate(point, (-30, -6), p.pitch)
        x += p.shift + p.shake
        y += p.height
        return self.ground((x, y))

    def ground(self, point):
        x, y = point
        return (CENTER + x, GROUND - y * self.p.squash)

    def head_point(self, point):
        return self.body(rotate(point, (16, 8), self.p.head))

    def jaw_point(self, point, near):
        opening = self.p.jaw * (24 if near else -16)
        return self.head_point(rotate(point, (27, 6), opening))

    def poly(self, points, fill, outline=INK):
        if self.mask:
            if self.p.glow_body:
                self.draw.polygon([(round(x), round(y)) for x, y in points], WHITE, WHITE)
            return
        self.draw.polygon([(round(x), round(y)) for x, y in points], fill, outline)

    def line(self, points, fill, width=1):
        if self.mask:
            return
        self.draw.line([(round(x), round(y)) for x, y in points], fill, width=width)

    def dot(self, point, fill):
        if not self.mask:
            self.draw.point((round(point[0]), round(point[1])), fill)

    # ---------- parts ----------
    def leg(self, hip_local, foot, color, femur=12, tibia=15):
        hip = self.body(hip_local)
        if self.p.splay:
            foot = (foot[0] * (1 + self.p.splay * 0.45), foot[1])
        target = self.ground(foot)
        dx, dy = target[0] - hip[0], target[1] - hip[1]
        reach = max(1e-3, min(math.hypot(dx, dy), femur + tibia - 0.5))
        base = math.atan2(dy, dx)
        bend = math.acos(max(-1.0, min(1.0, (femur ** 2 + reach ** 2 - tibia ** 2) / (2 * femur * reach))))
        # Knees bend up and away from the body's middle.
        sign = -1 if dx >= 0 else 1
        knee = (hip[0] + femur * math.cos(base + sign * bend), hip[1] + femur * math.sin(base + sign * bend))
        foot_end = (target[0] + (2 if dx >= 0 else -2), target[1])
        self.line([hip, knee, target, foot_end], INK, 3)
        self.line([hip, knee, target], color, 1)
        self.dot(knee, JOINT)
        self.dot(((hip[0] + knee[0]) / 2, (hip[1] + knee[1]) / 2 - 1), JOINT)

    def membrane(self, far):
        p = self.p
        if p.flap is None and p.wings < 0.5:
            return
        root = (-4, 15)
        if p.flap is None:
            sweep = -150 + 40 * p.wings
        else:
            sweep = (-170, -120, -75, -115)[p.flap]
        if far:
            sweep += 14
        length = 44
        tip = (root[0] + length * math.cos(math.radians(sweep)), root[1] - length * math.sin(math.radians(sweep)))
        mid = (root[0] + 0.55 * (tip[0] - root[0]) - 6, root[1] + 0.55 * (tip[1] - root[1]) + 7)
        trail = (root[0] + 0.6 * (tip[0] - root[0]) + 6, root[1] + 0.6 * (tip[1] - root[1]) - 6)
        points = [self.body(root), self.body(mid), self.body(tip), self.body(trail)]
        if p.flap is not None and not self.mask:
            for ghost in (-22, 22):
                gtip = (root[0] + length * math.cos(math.radians(sweep + ghost)),
                        root[1] - length * math.sin(math.radians(sweep + ghost)))
                self.draw.polygon([(round(x), round(y)) for x, y in
                                   (self.body(root), self.body(mid), self.body(gtip))], BLUR)
        self.poly(points, MEMBRANE, MEMBRANE_EDGE)
        self.line([self.body(root), self.body(tip)], MEMBRANE_EDGE)

    def abdomen(self):
        shell = ellipse(-20, 2, 22, 11)
        self.poly([self.body(pt) for pt in shell], BELLY)
        for x in (-35, -28, -21, -14, -7):
            self.line([self.body((x, -6)), self.body((x + 2, 8))], SEGMENT)

    def elytra(self):
        p = self.p
        lift = -p.wings * 58
        # Each half lifts around the shoulder at the front of the shell, its rear rising.
        def case(pt):
            return self.body(rotate(pt, (0, 14), lift))
        dome = ellipse(-20, 2, 23, 15, 0, 180) + ellipse(-20, 2, 23, 5, 180, 360)
        self.poly([case(pt) for pt in dome], BARK)
        for ry, color in ((11, GRAIN), (7, GRAIN)):
            ridge = ellipse(-20, 2, 23 - (15 - ry) * 0.6, ry, 20, 160, 14)
            for i in range(0, len(ridge) - 1, 2):
                self.line([case(ridge[i]), case(ridge[i + 1])], color)
        self.line([case(pt) for pt in ellipse(-20, 2, 23, 15, 30, 150, 14)], BARK_LIGHT)
        self.line([case(pt) for pt in ellipse(-18, 3, 17, 12, 55, 110, 6)], BARK_SHINE)
        self.line([case((-42, 1)), case((1, 3))], BARK_DARK)
        for (x, r) in ((-31, 3.5), (-20, 2.5), (-9, 3)):
            top = 2 + 15 * math.sqrt(max(0.0, 1 - ((x + 20) / 23) ** 2))
            tuft = ellipse(x, top - 1, r + 1, r * 0.8, 0, 180, 10)
            self.poly([case(pt) for pt in tuft], MOSS, None)
            self.dot(case((x - 1, top + r * 0.6)), MOSS_LIGHT)
            self.dot(case((x + 1, top + r * 0.5)), MOSS_LIGHT)
        for x, y in ((-26, 9), (-13, 11), (-35, 5)):
            self.dot(case((x, y)), LICHEN)

    def thorax(self):
        plate = [(-3, -3), (-4, 11), (1, 17), (12, 17), (17, 12), (17, 1), (12, -4), (2, -5)]
        self.poly([self.body(pt) for pt in plate], THORAX)
        self.line([self.body(pt) for pt in ((0, 14), (5, 16), (12, 15))], THORAX_LIGHT)
        self.line([self.body(pt) for pt in ((3, 8), (8, 9))], BARK_DARK)
        self.line([self.body((15, 4)), self.body((15, 12))], BARK_DARK)

    def head_and_jaws(self):
        p = self.p
        # The far jaw sits behind the head, the near jaw in front of it.
        self.jaw(near=False)
        shape = [(13, 0), (13, 13), (18, 17), (26, 16), (31, 11), (31, 3), (24, -2), (17, -2)]
        self.poly([self.head_point(pt) for pt in shape], HEAD)
        self.line([self.head_point((16, 14)), self.head_point((25, 14))], THORAX_LIGHT)
        self.line([self.head_point((28, 12)), self.head_point((29, 5))], BARK_DARK)
        eye = self.head_point((25, 10))
        if not self.mask:
            self.draw.rectangle((round(eye[0]) - 1, round(eye[1]) - 1, round(eye[0]), round(eye[1])), EYE)
        twitch = p.antenna
        self.line([self.head_point((24, 15)), self.head_point((27, 22 + twitch)), self.head_point((34, 23 + twitch))], INK, 2)
        self.line([self.head_point((27, 22 + twitch)), self.head_point((34, 23 + twitch))], JOINT)
        self.line([self.head_point((34, 22 + twitch)), self.head_point((36, 25 + twitch))], JOINT)
        self.jaw(near=True)

    def jaw(self, near):
        outline = [(26, 8), (34, 10), (42, 11), (48, 13), (52, 17), (54, 22), (57, 21), (56, 16), (59, 14),
                   (57, 11), (52, 9), (48, 7), (47, 2), (44, 6), (38, 5), (31, 3), (26, 3)]
        if not near:
            outline = [(x - 2, y + 3) for x, y in outline]
        points = [self.jaw_point(pt, near) for pt in outline]
        if self.mask:
            self.draw.polygon([(round(x), round(y)) for x, y in points], WHITE, WHITE)
            return
        self.poly(points, JAW if near else JAW_DARK)
        if near:
            self.line([self.jaw_point(pt, near) for pt in ((29, 7), (37, 8), (44, 9), (50, 12), (53, 17))], JAW_LIGHT)
            self.dot(self.jaw_point((46, 4), near), JAW_LIGHT)
            self.dot(self.jaw_point((57, 14), near), JAW_LIGHT)

    def render(self):
        p = self.p
        feet = p.feet or STAND_FEET
        far, near = feet[3:], feet[:3]
        hips = [(10, -3), (1, -3), (-12, -3)]
        if not self.shell_only:
            for hip, foot in zip(hips, far):
                self.leg((hip[0] + 2, hip[1] + 2), foot, LEG_FAR)
            self.membrane(far=True)
        if p.wings > 0:
            self.abdomen()
        self.thorax()
        self.elytra()
        if not self.shell_only:
            self.membrane(far=False)
        self.head_and_jaws()
        if not self.shell_only:
            for hip, foot in zip(hips, near):
                self.leg(hip, foot, LEG)
        return self.image


# ---------- animations ----------
def gait(phase, stride=5, lift=3, base=STAND_FEET):
    feet = []
    for index, (x, y) in enumerate(base):
        group = (index % 2) ^ (index >= 3)
        angle = 2 * math.pi * (phase + 0.5 * group)
        feet.append((x + stride * math.cos(angle), y + max(0.0, lift * math.sin(angle))))
    return feet


def spread(feet, amount):
    return [(x * amount, y) for x, y in feet]


CROUCH_FEET = spread(STAND_FEET, 1.25)
TUCKED = [(20, 10), (2, 9), (-24, 10), (23, 11), (6, 10), (-20, 11)]
DANGLE = [(22, -6), (4, -8), (-26, -6), (26, -5), (8, -7), (-22, -5)]


def anims():
    out = []
    out.append(("idle", [Pose(height=20 + b, antenna=a) for b, a in ((0, 0), (1, 0), (1, 1), (0, 1))]))
    out.append(("walk", [Pose(height=20 + (1 if f % 3 == 1 else 0), feet=gait(f / 6), shift=0) for f in range(6)]))
    out.append(("turn", [Pose(height=18, feet=spread(STAND_FEET, 0.8), squash=0.96),
                         Pose(height=17, feet=spread(STAND_FEET, 0.7), squash=0.94)]))
    windup = STAND_FEET[:]
    scrape_back = [(14, 1)] + STAND_FEET[1:]
    scrape_fore = [(30, 0)] + STAND_FEET[1:]
    out.append(("charge_windup", [
        Pose(height=21, shift=-3, pitch=5, head=6, feet=windup),
        Pose(height=22, shift=-5, pitch=8, head=10, feet=scrape_back),
        Pose(height=21, shift=-4, pitch=6, head=6, feet=scrape_fore),
        Pose(height=22, shift=-5, pitch=8, head=10, feet=scrape_back),
        Pose(height=12, shift=-2, pitch=-2, head=-4, jaw=0.2, feet=CROUCH_FEET),
        Pose(height=6, shift=0, pitch=-5, head=-6, jaw=0.35, feet=CROUCH_FEET)]))
    out.append(("charge_run", [Pose(height=6 + (f % 2), pitch=-5, head=-6, jaw=0.3,
                                    feet=gait(f / 4, stride=9, lift=4, base=CROUCH_FEET)) for f in range(4)]))
    out.append(("stuck", [Pose(height=6, pitch=-7, head=-8, jaw=0.1, shake=(-1, 1, 0, 1)[f],
                               feet=gait(f / 4, stride=6, lift=3, base=CROUCH_FEET)) for f in range(4)]))
    out.append(("wrench", [Pose(height=8, pitch=-2, shift=-2, head=4, jaw=0.4, feet=CROUCH_FEET),
                           Pose(height=12, pitch=4, shift=-4, head=10, jaw=0.5, feet=STAND_FEET),
                           Pose(height=17, pitch=2, shift=-2, head=4, jaw=0.2, feet=STAND_FEET)]))
    out.append(("snap", [Pose(height=21, head=12, jaw=0.6, shift=-2),
                         Pose(height=22, head=18, jaw=1.0, shift=-3),
                         Pose(height=20, head=-10, jaw=0.0, shift=3),
                         Pose(height=18, head=-14, jaw=0.2, shift=1)]))
    rear_feet = [(28, 0), (8, 0), (-26, 0), (31, 0), (12, 0), (-22, 0)]
    out.append(("rear", [Pose(height=20, head=12, feet=rear_feet),
                         Pose(height=20, pitch=16, head=14, jaw=0.6, feet=rear_feet),
                         Pose(height=20, pitch=34, head=22, jaw=1.0, feet=[(14, 18), (4, 10), (-26, 0), (17, 20), (7, 12), (-22, 0)]),
                         Pose(height=20, pitch=38, head=10, jaw=0.0, feet=[(12, 22), (3, 12), (-26, 0), (15, 24), (6, 14), (-22, 0)]),
                         Pose(height=20, pitch=20, head=0, jaw=0.2, feet=[(22, 8), (6, 3), (-26, 0), (25, 9), (9, 4), (-22, 0)]),
                         Pose(height=19, pitch=4, head=-6, feet=STAND_FEET)]))
    out.append(("wing_open", [Pose(height=21, wings=w, feet=STAND_FEET) for w in (0.25, 0.5, 0.8, 1.0)]))
    out.append(("hover", [Pose(height=20, wings=1.0, flap=f, feet=DANGLE, head=4) for f in range(4)]))
    out.append(("dive", [Pose(height=20, pitch=-22, wings=1.0, flap=f, feet=TUCKED, head=-10, jaw=0.5) for f in (0, 2)]))
    out.append(("land", [Pose(height=9, wings=0.6, feet=spread(STAND_FEET, 1.35), squash=0.9),
                         Pose(height=13, wings=0.3, feet=spread(STAND_FEET, 1.2)),
                         Pose(height=17, wings=0.0, feet=spread(STAND_FEET, 1.08))]))
    out.append(("dash", [Pose(height=14, wings=1.0, flap=f, feet=TUCKED, head=-2, jaw=0.45) for f in (1, 3)]))
    out.append(("roar", [Pose(height=20, pitch=24, head=16, jaw=j, feet=rear_feet) for j in (0.3, 1.0, 0.6, 1.0)]))
    kneel = []
    for f in range(5):
        t = f / 4
        front = (24 - 14 * t, 0)
        kneel.append(Pose(height=20 - 4 * t, pitch=-12 * t, head=-18 * t, jaw=0.3 * t,
                          feet=[front, (3 - 3 * t, 0), (-29, 0), (front[0] + 4, 0), (8 - 3 * t, 0), (-24, 0)]))
    drop = []
    for f in range(4):
        t = (f + 1) / 4
        drop.append(Pose(height=16 - 11 * t, pitch=-12 + 8 * t, head=-18 + 6 * t, jaw=0.3, splay=t,
                         feet=[(10 + 18 * t, 0), (4 * t, 0), (-29 - 10 * t, 0), (14 + 18 * t, 0), (5 + 5 * t, 0), (-24 - 10 * t, 0)]))
    flat = Pose(height=5, pitch=-4, head=-12, jaw=0.35, splay=1.0,
                feet=[(30, 0), (4, 0), (-40, 0), (34, 0), (10, 0), (-35, 0)])
    out.append(("death", kneel + drop + [flat]))
    # Added after death so earlier frame numbers stay put: rise high and quiver, belly-flop, push back up.
    tall = spread(STAND_FEET, 0.8)
    out.append(("slam", [
        Pose(height=25, head=4, jaw=0.2, feet=spread(STAND_FEET, 0.88), glow_body=True),
        Pose(height=28, head=6, jaw=0.3, feet=tall, shake=1, glow_body=True),
        Pose(height=4, head=-6, jaw=0.2, splay=0.6, squash=0.92,
             feet=[(32, 0), (6, 0), (-40, 0), (36, 0), (10, 0), (-35, 0)]),
        Pose(height=10, head=-2, feet=spread(STAND_FEET, 1.2)),
        Pose(height=16, feet=spread(STAND_FEET, 1.05))]))
    return out


# ---------- shared effects ----------
def effects():
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
    draw.polygon([(ox + 10, 11), (ox + 20, 9), (ox + 23, 15), (ox + 17, 22), (ox + 9, 19)], BARK, INK)
    draw.line((ox + 12, 13, ox + 19, 12), BARK_LIGHT)
    draw.point((ox + 14, 17), MOSS)
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


# ---------- sheets ----------
def build():
    table = anims()
    frames = [pose for _, poses in table for pose in poses]
    rows = math.ceil(len(frames) / COLUMNS)
    atlas = Image.new("RGBA", (CELL_W * COLUMNS, CELL_H * rows), (0, 0, 0, 0))
    glow = Image.new("RGBA", atlas.size, (0, 0, 0, 0))
    for index, pose in enumerate(frames):
        x, y = index % COLUMNS * CELL_W, index // COLUMNS * CELL_H
        atlas.paste(Beetle(pose).render(), (x, y))
        glow.paste(Beetle(pose, mask=True).render(), (x, y))
    atlas.save(OUT / "ForestBoss.png")
    glow.save(OUT / "ForestBossGlow.png")

    start = 0
    lines = []
    for name, poses in table:
        lines.append(f"{name} {start} {len(poses)}")
        start += len(poses)
    print("Frames:", ", ".join(lines))
    return table, atlas


def contact_sheet(table, atlas):
    PREVIEWS.mkdir(parents=True, exist_ok=True)
    actors = Image.open(OUT / "Actors.png").convert("RGBA")
    player = actors.crop((0, 0, 32, 24))
    scale = 2
    label_h = 12
    total = sum(len(poses) for _, poses in table)
    cols = 8
    rows = math.ceil(total / cols)
    key = [0, 22, 27, 36, 41, 47, 50, 57]
    top_h = CELL_H * 3 + 24
    sheet = Image.new("RGBA", (cols * CELL_W * scale, top_h + rows * (CELL_H * scale + label_h)), (28, 22, 40, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((8, 4), "Old Oakhorn - key poses at 3x, the player (Jumping Spider) beside each for scale", fill=(255, 232, 187, 255))
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
    sheet.save(PREVIEWS / "forest.png")


# ---------- hurtboxes ----------
# StagBeetle's four body poses, each fitted to one representative frame.
HURT_POSES = (("Stand", 0), ("Crouch", 18), ("Rear", 36), ("Air", 43), ("Dive", 47), ("Dash", 52), ("Raised", 69),
              ("Flat", 70))
# Circles along the body in each part's own frame: (part, point, radius in pixels, armored).
PARTS = (
    ("body", (-31, 4), 11.5, False),
    ("body", (-12, 5), 12.5, False),
    ("body", (7, 6), 11, False),
    ("head", (22, 7), 10, False),
    ("jaw", (36, 7), 6, True),
    ("jaw", (51, 13), 7, True),
    # A lifted wing case, in poses that fly.
    ("case", (-20, 7), 10, False),
    ("case", (-36, 5), 7, False),
)
# StagBeetle pads each hurtbox this far past the art, and keeps contact this much inside it.
PADDING = 3
CONTACT = 0.85


def fitted_parts(pose):
    """The body circles for a pose, in world units from the boss's Position: (x, y, radius, armored)."""
    beetle = Beetle(pose)
    parts = []
    for part, point, radius, armored in PARTS:
        if part == "body":
            x, y = beetle.body(point)
        elif part == "head":
            x, y = beetle.head_point(point)
        elif part == "case":
            if pose.wings < 0.5:
                continue
            x, y = beetle.body(rotate(point, (0, 14), -pose.wings * 58))
        else:
            x, y = beetle.jaw_point(point, True)
        parts.append(((x - CENTER) / 16, (GROUND - y) / 16, radius / 16, armored))
    return parts


def hurtbox_sheet(table, atlas):
    """Prints StagBeetle's fitted parts and draws them over their frames, with how much of the solid
    body the padded hurtboxes cover (it should be all of it)."""
    poses = [pose for _, group in table for pose in group]
    scale = 4
    rows = (len(HURT_POSES) + 1) // 2
    sheet = Image.new("RGBA", (CELL_W * scale * 2, CELL_H * scale * rows + 20), (28, 22, 40, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((6, 4), "Hurtboxes: green padded (hits land), grey armored jaws, red contact (touching hurts)",
              fill=(255, 232, 187, 255))
    for slot, (name, index) in enumerate(HURT_POSES):
        parts = fitted_parts(poses[index])
        print(f"{name}:")
        for x, y, r, armored in parts:
            print(f"    AddPart({x:.2f}f, {y:.2f}f, {r:.2f}f{', armored: true' if armored else ''});")
        frame = atlas.crop((index % COLUMNS * CELL_W, index // COLUMNS * CELL_H,
                            index % COLUMNS * CELL_W + CELL_W, index // COLUMNS * CELL_H + CELL_H))
        ox, oy = slot % 2 * CELL_W * scale, 20 + slot // 2 * CELL_H * scale
        sheet.alpha_composite(frame.resize((CELL_W * scale, CELL_H * scale), Image.NEAREST), (ox, oy))
        shell = Beetle(poses[index], shell_only=True).render()
        solid = covered = 0
        for py in range(CELL_H):
            for px in range(CELL_W):
                if shell.getpixel((px, py))[3] < 128:
                    continue
                solid += 1
                wx, wy = (px + 0.5 - CENTER) / 16, (GROUND - py - 0.5) / 16
                if any((wx - x) ** 2 + (wy - y) ** 2 <= (r + PADDING / 16) ** 2 for x, y, r, _ in parts):
                    covered += 1
        print(f"    // padded hurtboxes cover {covered / solid:.0%} of the solid body")
        for x, y, r, armored in parts:
            cx, cy = ox + (CENTER + x * 16) * scale, oy + (GROUND - y * 16) * scale
            hurt = (r * 16 + PADDING) * scale
            touch = r * 16 * CONTACT * scale
            draw.ellipse((cx - hurt, cy - hurt, cx + hurt, cy + hurt), outline=(150, 150, 160, 255) if armored else (110, 230, 140, 255), width=2)
            draw.ellipse((cx - touch, cy - touch, cx + touch, cy + touch), outline=(240, 80, 80, 255), width=1)
        draw.text((ox + 6, oy + 4), f"{name} ({covered / solid:.0%} covered)", fill=(255, 232, 187, 255))
    sheet.save(PREVIEWS / "forest_hurtboxes.png")


if __name__ == "__main__":
    effects()
    table, atlas = build()
    contact_sheet(table, atlas)
    hurtbox_sheet(table, atlas)
