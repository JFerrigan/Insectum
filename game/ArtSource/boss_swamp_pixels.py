"""Render Mire Lich at 16 px/unit and generate the matching logic geometry.

Each abdomen stage has the same frame order (MireLichPose in MireLichArt.cs).
The queen always faces right in the atlas. All geometry is in local Y-up pixels.
"""

import math
from pathlib import Path

from PIL import Image, ImageDraw

from boss_pixels import (OUT, PREVIEWS, BossPose, draw_hurtboxes, ellipse, hurtbox_coverage,
                         pose_preview, rotate)

CELL = (160, 104)
PIVOT = (80, 72)
COLUMNS = 8
INK = (32, 19, 37)
BONE = (184, 148, 111)
LIGHT = (245, 210, 151)
SHADE = (100, 68, 71)
CHITIN = (132, 101, 90)
EYE = (204, 246, 157)
BLOOD = [(103, 27, 49), (137, 31, 55), (170, 39, 63)]
SKIN = [(169, 49, 67), (203, 59, 77), (229, 76, 95)]
GLINT = [(240, 112, 117), (255, 130, 129), (255, 166, 145)]


class Pose(BossPose):
    def __init__(self, **values):
        super().__init__(height=22, tilt=0, tip=(39, 16), beat=0, legs="air", glow="head",
                         pulse=0, swell=0, rupture=-1, corpse=False)
        super().__init__(**values)


def poses():
    out = [(f"Hover{i}", Pose(beat=i)) for i in range(4)]
    tips = [(51, -5), (40, -20), (23, -28)]
    for i, (x, y) in enumerate(tips):
        out.append((f"DiveAim{i}", Pose(tilt=-10 - 7*i, tip=(x - 8, y + 7), beat=2)))
    for i, tip in enumerate(tips):
        out.append((f"Dive{i}", Pose(tilt=-13 - 7*i, tip=tip, beat=3, legs="dive")))
    out += [
        ("Spray", Pose(tilt=8, tip=(32, 28), pulse=-2, beat=1, glow="sac")),
        ("Bubble", Pose(tilt=12, tip=(37, 31), beat=1, glow="thorax")),
        ("Call", Pose(tilt=18, tip=(35, 34), beat=2, glow="sac")),
        ("Land", Pose(tip=(27, 5), beat=1, legs="ground", glow="wing")),
        ("Feed0", Pose(height=17, tilt=-15, tip=(28, -2), beat=3, legs="ground", glow="sac")),
        ("Feed1", Pose(height=17, tilt=-15, tip=(28, -2), beat=3, legs="ground", pulse=1, glow="sac")),
        ("Feed2", Pose(height=17, tilt=-15, tip=(28, -2), beat=3, legs="ground", pulse=2, glow="sac")),
        ("JabWind", Pose(height=15, tilt=8, tip=(27, 12), beat=3, legs="ground")),
        ("Jab", Pose(height=12, tilt=-9, tip=(42, 9), beat=3, legs="ground")),
        ("Buffet", Pose(height=18, tilt=6, tip=(30, 12), beat=1, legs="ground", glow="wing")),
        ("ProbeWind", Pose(height=21, tilt=-20, tip=(10, 1), beat=2, legs="ground")),
        ("Probe", Pose(height=17, tilt=-32, tip=(2, -12), beat=3, legs="ground")),
        ("Takeoff", Pose(tilt=10, tip=(36, 27), beat=0, legs="ground", glow="wing")),
        ("Stagger", Pose(tilt=-8, tip=(28, 5), beat=3, glow="thorax")),
    ]
    out += [(f"Swell{i}", Pose(tilt=-5, beat=3, swell=i*2, glow="sac")) for i in range(4)]
    out += [(f"Rupture{i}", Pose(beat=3, rupture=i, corpse=True)) for i in range(3)]
    out += [("Corpse", Pose(height=9, tilt=-7, tip=(33, 1), beat=3, corpse=True, legs="ground"))]
    return out


class Queen:
    def __init__(self, stage, pose):
        self.stage, self.p = stage, pose
        self.image = Image.new("RGBA", CELL)
        self.draw = ImageDraw.Draw(self.image)
        self.mask = Image.new("RGBA", CELL)
        self.parts = []
        self.thorax = (0, pose.height)
        self.head = self.local((19, 1))
        self.sac = self.local(([-23, -28, -34][stage], [1, 2, 4][stage]))
        self.sac_size = ([19, 25, 31][stage] + pose.pulse + pose.swell,
                         [15, 19, 24][stage] + pose.pulse + pose.swell)
        self.solid = Image.new("RGBA", CELL)

    def local(self, point):
        x, y = rotate(point, (0, 0), self.p.tilt)
        return x, y + self.p.height

    @staticmethod
    def pixel(point):
        return round(PIVOT[0] + point[0]), round(PIVOT[1] - point[1])

    def poly(self, points, fill, edge=None):
        self.draw.polygon([self.pixel(p) for p in points], fill, edge)

    def line(self, points, fill, width=1):
        self.draw.line([self.pixel(p) for p in points], fill, width)

    def spot(self, center, rx, ry, fill, edge=None):
        self.poly(ellipse(*center, rx, ry), fill, edge)

    def group(self, name, painter):
        previous = self.image
        self.image = Image.new("RGBA", CELL)
        self.draw = ImageDraw.Draw(self.image)
        painter()
        if name == self.p.glow or name == "head" and self.p.glow == "head":
            tint = Image.new("RGBA", CELL, (255, 255, 255, 0))
            tint.putalpha(self.image.getchannel("A"))
            self.mask.alpha_composite(tint)
        if name in ("sac", "thorax", "head"):
            self.solid.alpha_composite(self.image)
        previous.alpha_composite(self.image)
        self.image = previous
        self.draw = ImageDraw.Draw(self.image)

    def wing(self):
        x, y = self.thorax
        for far in (True, False):
            spread = [1.0, 0.64, 0.20, 0.49][self.p.beat]
            length = (61 if far else 69) * (0.51 if self.p.corpse else 1)
            lift = (14 if far else 45) * spread * (0.20 if self.p.corpse else 1)
            size = 10 if far else 15
            tip = (x - length, y + lift + (5 if far else 1))
            knuckle = (x - length * 0.64, y + lift * 0.58)
            rim = (156, 187, 169, 192 if far else 231)
            fill = (96, 136, 135, 107 if far else 151)
            path = [(x+4, y+3), (knuckle[0]+4, knuckle[1]+size), (tip[0]+7, tip[1]+6),
                    tip, (tip[0]+6, tip[1]-7), (knuckle[0], knuckle[1]-size), (x-8, y)]
            self.poly(path, fill, rim)
            self.line([(x, y+3), knuckle, tip], rim)
            for bend in (0.28, 0.56, 0.79):
                sx, sy = x - length*bend, y + lift*bend
                self.line([(sx+6, sy-7), (sx-5, sy), (sx-12, sy+7)], (196, 210, 166, 123))
            self.line([tip, (tip[0]+7, tip[1]+6), (knuckle[0]+4, knuckle[1]+size)], (228, 230, 170, 167))

    def limbs(self, near):
        for i in range(3):
            root = self.local((-8 + i*7, -6))
            x, y = root
            if self.p.legs == "ground":
                foot = (-37 + 33*i + (5 if near else -6), 1 if near else 4)
                joint = (foot[0] + (10 if i == 2 else -8), 17 + 2*i)
            elif self.p.legs == "dive":
                joint, foot = (x-15+i*4, y+3), (x-37+i*10, y-14+i*3)
            else:
                flick = (self.p.beat % 2) * (2 if near else -2)
                joint = (x-15+i*12, y-18+2*i)
                foot = (joint[0]-13+i*6+flick, joint[1]-16+i*2)
            if not near:
                joint = (joint[0]+6, joint[1]+4)
                foot = (foot[0]+3, foot[1]+5)
            toe = (foot[0]+5, foot[1]+(2 if self.p.legs == "air" else 0))
            self.line([root, joint, foot, toe], INK, 4 if near else 3)
            self.line([root, joint, foot, toe], BONE if near else SHADE, 2 if near else 1)
            self.spot(joint, 2 if near else 1, 2 if near else 1, LIGHT if near else CHITIN)

    def abdomen(self):
        x, y = self.sac
        rx, ry = self.sac_size
        if self.p.corpse:
            x, y, rx, ry = x+10, y-3, 19, 7
        color, skin, glint = BLOOD[self.stage], SKIN[self.stage], GLINT[self.stage]
        self.spot((x, y), rx, ry, color, INK)
        self.spot((x-2, y+3), rx-4, ry-5, skin)
        self.poly([(x-rx+5,y-4), (x-rx/2,y-ry+5), (x+rx/2,y-ry+4), (x+rx-2,y-2),
                   (x+8,y-6), (x-5,y-8), (x-15,y-5)], color)
        if self.p.corpse:
            self.line([(x-rx,y), (x-7,y+3), (x-2,y-3), (x+10,y+2)], INK, 2)
            return
        self.line(ellipse(x, y, rx-3, ry-2, 30, 155, 10), glint, 2)
        for i in range(3):
            self.line([(x+rx-3,y+4-i*4), (x+7,y+4-i*5), (x-1,y+1-i*4),
                       (x-13,y+4-i*6), (x-rx+4,y-2-i*2)], color, 1 if i else 2)
        self.line([(x-2,y-3), (x-7,y+2), (x-10,y+10), (x-15,y+ry-3)], color)
        self.line([(x-12,y+ry-6),(x-7,y+ry-4),(x+1,y+ry-5)], glint, 3)
        self.spot((x-16,y+ry-8), 2, 1, LIGHT)
        self.parts += [(x - (rx-ry)*0.62, y, ry, True), (x+(rx-ry)*0.7, y, ry, True)]

    def plates(self):
        self.spot(self.thorax, 15, 13, SHADE, INK)
        self.poly([self.local(p) for p in [(-13,0),(-8,10),(3,12),(12,8),(10,1),(3,-3),(-9,-4)]],
                  BONE, INK)
        self.line([self.local(p) for p in [(-9,7),(-2,10),(6,9),(10,6)]], LIGHT, 2)
        for offset in (-8, -3, 2):
            self.line([self.local((offset, -1)), self.local((offset+3, -7))], CHITIN, 2)
        self.spot(self.local((-5,-4)), 2, 2, EYE, INK)
        self.parts.append((*self.thorax, 13, False))

    def snout(self):
        hx, hy = self.head
        tx, ty = self.p.tip
        dx, dy = tx-hx, ty-hy
        length = math.hypot(dx,dy)
        nx, ny = -dy/length, dx/length
        self.poly([(hx+4*nx,hy+4*ny), (hx+dx*0.54+2*nx,hy+dy*0.54+2*ny),
                   (tx,ty), (hx+dx*0.62-2*nx,hy+dy*0.62-2*ny), (hx-4*nx,hy-4*ny)], BONE, INK)
        self.line([(hx,hy), (hx+dx*0.65,hy+dy*0.65), (tx,ty)], LIGHT)
        self.spot(self.head, 10, 10, CHITIN, INK)
        self.poly([(hx-8,hy+3),(hx-3,hy+9),(hx+4,hy+9),(hx+8,hy+4),(hx+4,hy+1),(hx-4,hy)], BONE, INK)
        self.spot((hx+3,hy+1), 5, 5, SHADE, INK)
        self.spot((hx+4,hy+2), 3, 4, EYE, INK)
        self.line([(hx+3,hy+4),(hx+3,hy+2)], (255,255,216))
        self.parts.append((hx, hy, 10, False))
        for i in range(8, math.ceil(length), 5):
            n = min(i/length, 0.96)
            self.parts.append((hx+dx*n, hy+dy*n, 2.5-1.3*n, False))

    def feelers(self):
        hx, hy = self.head
        for direction in (-1, 1):
            path = [(hx,hy+8), (hx-3+direction*5,hy+15), (hx+2+direction*14,hy+19),
                    (hx+direction*21,hy+16)]
            self.line(path, INK, 3)
            self.line(path, BONE)
            self.line([path[2], (path[2][0]-direction*3,path[2][1]+3)], LIGHT)

    def burst(self):
        frame = self.p.rupture
        for i in range(14):
            a = math.tau*i/14
            r = 18 + 7*frame + i%3*4
            x, y = self.sac
            end = (x+math.cos(a)*r, y+math.sin(a)*r)
            base = (x+math.cos(a-0.11)*r*0.36, y+math.sin(a-0.11)*r*0.36)
            self.line([base, end], SKIN[2], 4-frame)
            self.spot(end, 2+(i%3==0), 2, GLINT[2])

    def render(self):
        self.group("wing", self.wing)
        self.limbs(False)
        self.group("sac", self.abdomen)
        self.group("thorax", self.plates)
        self.limbs(True)
        self.group("head", self.snout)
        self.feelers()
        if self.p.rupture >= 0:
            self.burst()
        return self.image, self.mask


def effect_atlas():
    atlas = Image.new("RGBA", (128, 32))
    d = ImageDraw.Draw(atlas)
    d.ellipse((10,10,22,23), SKIN[2], INK, 2)
    d.line((13,14,16,12), GLINT[2], 2)
    d.ellipse((34,13,61,21), BLOOD[2], SKIN[2])
    d.line((38,15,54,15), GLINT[2])
    d.ellipse((68,4,91,27), (69,124,93,109), (173,225,124,255), 2)
    d.line((72,12,75,8,79,7), (224,251,175),2)
    d.ellipse((98,14,119,23), (110,141,70), INK, 2)
    d.line((100,18,106,11,116,10,123,15), (190,196,100), 2)
    d.line((103,18,99,25,112,26), (143,114,84), 2)
    d.point((117,17), (242,244,174))
    atlas.save(OUT / "SwampBossFx.png")


def main():
    table = poses()
    count = len(table)
    w, h = CELL
    atlas = Image.new("RGBA", (w*COLUMNS, h*math.ceil(count*3/COLUMNS)))
    glow = Image.new("RGBA", atlas.size)
    overlay = Image.new("RGBA", (4*w*2, math.ceil(count/4)*h*2), (38,45,47,255))
    overlay_draw = ImageDraw.Draw(overlay)
    geometry = []
    coverages = []
    for stage in range(3):
        for i, (name, pose) in enumerate(table):
            queen = Queen(stage, pose)
            frame, mask = queen.render()
            at = stage*count+i
            atlas.paste(frame, (at%COLUMNS*w, at//COLUMNS*h))
            glow.paste(mask, (at%COLUMNS*w, at//COLUMNS*h))
            if pose.corpse or pose.swell:
                continue
            # Match the values written to C# before measuring the mask.
            parts = [(round(x/16, 4), round(y/16, 4), round(r/16, 4), weak) for x,y,r,weak in queen.parts]
            coverage = hurtbox_coverage(queen.solid, parts, PIVOT)
            coverages.append((coverage,stage+1,name))
            if stage == 2:
                origin = (i%4*w*2, i//4*h*2)
                overlay.alpha_composite(frame.resize((w*2,h*2), Image.NEAREST), origin)
                draw_hurtboxes(overlay_draw, parts, origin, PIVOT, 2, (255,202,76,255))
                overlay_draw.text((origin[0]+5,origin[1]+5), f"{name} / {coverage:.1%}", (251,234,196))
            entries = ", ".join(f"new({x:g}f, {y:g}f, {r:g}f, {2 if weak else 1})" for x,y,r,weak in parts)
            geometry.append(f"            Parts[{at}] = new Part[] {{ {entries} }};")
    lowest = min(coverages)
    assert lowest[0] >= 0.95, lowest
    print(f"Minimum solid-art coverage: {lowest[0]:.2%} (stage {lowest[1]} {lowest[2]})")
    atlas.save(OUT / "SwampBoss.png")
    glow.save(OUT / "SwampBossGlow.png")
    effect_atlas()
    cards = [("Stage 1 / hover", 0), ("Stage 2 / spray", count+10), ("Stage 3 / hover", 2*count),
             ("Dive / locked aim", 2*count+5), ("Landing / legs down", 2*count+13),
             ("Feeding / proboscis down", 2*count+15), ("Ground jab / lunge", 2*count+18),
             ("Probe / downstroke", 2*count+21), ("Wing buffet / wings forward", 2*count+19),
             ("Death / swelling", 2*count+27), ("Death / rupture", 2*count+29),
             ("Death / settled", 2*count+31)]
    pose_preview(atlas, title="MIRE LICH  /  Queen and Jumping Spider at the same scale",
                 filename="swamp.png", cell=CELL, columns=COLUMNS, ground=PIVOT[1], cards=cards)
    overlay.save(PREVIEWS / "swamp_hurtboxes.png")
    path = Path(__file__).resolve().parents[1] / "Assets/Scripts/Bosses/MireLichArt.cs"
    names = ", ".join(name for name, _ in table)
    path.write_text(f"""// Generated by ArtSource/boss_swamp_pixels.py.
using UnityEngine;

namespace Insectum.Game
{{
    internal enum MireLichPose
    {{
        {names}
    }}

    internal static class MireLichArt
    {{
        internal const int FramesPerStage = {count};
        internal const int Columns = {COLUMNS}, Width = {w}, Height = {h}, GroundPixels = {h-PIVOT[1]};
        internal static readonly Part[][] Parts = new Part[FramesPerStage * 3][];
        internal static readonly Vector2[] DiveTips = {{ {', '.join(f'new({x/16:g}f, {y/16:g}f)' for x,y in (p.tip for _,p in table[7:10]))} }};

        internal readonly struct Part
        {{
            internal readonly float X, Y, Radius;
            internal readonly int Multiplier;

            internal Part(float x, float y, float radius, int multiplier)
            {{
                X = x;
                Y = y;
                Radius = radius;
                Multiplier = multiplier;
            }}
        }}

        static MireLichArt()
        {{
{chr(10).join(geometry)}
        }}
    }}
}}
""")


if __name__ == "__main__":
    main()
