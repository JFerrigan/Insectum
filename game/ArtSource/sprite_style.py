"""The shared shading style for the newer sprites: original shapes and ink outlines, shaded by one rule.

Every body part is a filled shape with a 1px ink outline. Inside it, the bottom edge (2px on taller parts)
and lower right edge take a darker, purple-shifted shade, the top-left edge takes a lighter, warm-shifted
highlight, and bigger parts get one glint pixel. Details use tones from the same part's ramp, and every eye
is an ink socket with a glint. Pic takes the ink colour, so each biome keeps its own outline.
"""

import colorsys

from PIL import Image, ImageDraw

INK = "#1b1029"
CREAM = "#ffe8bb"
GOLD = "#ffbd67"


def rgb(c):
    if isinstance(c, str):
        c = c.lstrip("#")
        return tuple(int(c[i:i + 2], 16) for i in (0, 2, 4))
    return tuple(c[:3])


def _toward(h, target, amount):
    diff = (target - h + 0.5) % 1 - 0.5
    return (h + max(-amount, min(amount, diff))) % 1


def _hsv(h, s, v):
    return tuple(round(x * 255) for x in colorsys.hsv_to_rgb(h, max(0, min(1, s)), max(0, min(1, v))))


def ramp(base):
    """Five tones around a base: shade toward purple and darker, light toward warm and paler."""
    h, s, v = colorsys.rgb_to_hsv(*(x / 255 for x in rgb(base)))
    light = _hsv(_toward(h, 0.13, 0.03), s * 0.78, v * 1.1 + 0.1)
    return {
        "deep": _hsv(_toward(h, 0.76, 0.06), s * 1.12 + 0.08, v * 0.5),
        "shade": _hsv(_toward(h, 0.76, 0.035), s * 1.08 + 0.05, v * 0.72),
        "base": rgb(base),
        "light": light,
        "glint": tuple(round(a * 0.4 + b * 0.6) for a, b in zip(light, rgb(CREAM))),
    }


class Pic:
    def __init__(self, width, height, ink=INK):
        self.w, self.h, self.ink = width, height, rgb(ink)
        self.image = Image.new("RGBA", (width, height), (0, 0, 0, 0))
        self.px = self.image.load()
        self.draw = ImageDraw.Draw(self.image)

    def put(self, point, color):
        x, y = point
        if 0 <= x < self.w and 0 <= y < self.h:
            self.px[x, y] = rgb(color) + (255,)

    def _cells(self, paint):
        mask = Image.new("1", (self.w, self.h), 0)
        paint(ImageDraw.Draw(mask))
        return {(i % self.w, i // self.w) for i, v in enumerate(mask.getdata()) if v}

    def part(self, cells, color, outline=True, glint=True, light=True):
        """Fill a shape with the shared shading and an ink outline. Returns its ramp for details."""
        tones = ramp(color)
        inside = cells if not outline else {
            (x, y) for x, y in cells
            if {(x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)} <= cells}
        for point in cells - inside:
            self.put(point, self.ink)
        if not inside:
            return tones
        xs = [x for x, _ in inside]
        ys = [y for _, y in inside]
        x0, y0 = min(xs), min(ys)
        w, h = max(xs) - x0 + 1, max(ys) - y0 + 1
        thick = h >= 7
        lit = []
        for x, y in inside:
            bottom = (x, y + 1) not in inside or (thick and (x, y + 2) not in inside)
            right = (x + 1, y) not in inside and y >= y0 + h * 0.35
            top = (x, y - 1) not in inside and x <= x0 + w * 0.62
            left = (x - 1, y) not in inside and y <= y0 + h * 0.5
            if bottom or right:
                tone = tones["shade"]
            elif light and (top or left) and w > 2 and h > 2:
                tone = tones["light"]
                lit.append((x, y))
            else:
                tone = tones["base"]
            self.put((x, y), tone)
        if glint and lit and w >= 6 and h >= 5:
            gx, gy = x0 + w * 0.3, y0 + h * 0.2
            spot = min(lit, key=lambda p: (p[0] - gx) ** 2 + (p[1] - gy) ** 2)
            self.put(spot, tones["glint"])
            if w >= 12 and (spot[0] + 1, spot[1]) in lit:
                self.put((spot[0] + 1, spot[1]), tones["glint"])
        return tones

    def oval(self, bounds, color, **style):
        return self.part(self._cells(lambda d: d.ellipse(bounds, 1, 1)), color, **style)

    def poly(self, points, color, **style):
        return self.part(self._cells(lambda d: d.polygon(points, 1, 1)), color, **style)

    def rect(self, bounds, color, **style):
        return self.part(self._cells(lambda d: d.rectangle(bounds, 1, 1)), color, **style)

    def line(self, points, color, width=1):
        self.draw.line(points, rgb(color) + (255,), width=width)

    def arc(self, bounds, start, end, color):
        self.draw.arc(bounds, start, end, rgb(color) + (255,))

    def fill_oval(self, bounds, color):
        self.draw.ellipse(bounds, rgb(color) + (255,))

    def stroke(self, points, color, width=1, knee=None):
        """An inked limb: ink line two pixels wider, colour line on top, optional knee highlight."""
        self.line(points, self.ink, width + 2)
        self.line(points, color, width)
        if knee:
            self.put(points[1], knee)

    def dot(self, point, color):
        self.put(point, color)

    def eye(self, x, y, glint=CREAM, big=False):
        """Ink socket with a glint at its top left: 2x2, or a 4x5 oval when big."""
        if big:
            self.draw.ellipse((x, y, x + 3, y + 4), self.ink + (255,))
            self.put((x + 1, y + 1), glint)
            self.put((x + 2, y + 3), ramp(glint)["shade"])
        else:
            self.draw.rectangle((x, y, x + 1, y + 1), self.ink + (255,))
            self.put((x, y), glint)

    def spark(self, x, y, color=GOLD):
        for dx, dy in ((-1, 0), (1, 0), (0, -1), (0, 1)):
            self.put((x + dx, y + dy), color)
        self.put((x, y), CREAM)


def limb(pic, path, color):
    """A leg in the shared style: inked, with a highlighted knee."""
    pic.stroke(path, color, knee=ramp(color)["light"])
