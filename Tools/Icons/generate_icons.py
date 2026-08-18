"""Generates the EchoRun geometric line icon set.

Every icon is defined once as a list of primitives (lines, arcs, circles,
polylines) in a 96x96 grid. The same definition is emitted twice:

  * SVG line-art source -> ArtSource/Icons/<name>.svg (provenance)
  * Runtime PNG         -> Assets/Resources/Art/Icons/<name>.png

PNGs are rasterized at 4x and downscaled for anti-aliasing. A contact
sheet lands in docs/ConceptArt/IconSet-v1.png for the competition deck.

Run with: python3 Tools/Icons/generate_icons.py  (Pillow only, no SVG lib)
"""

import json
import math
import os

from PIL import Image, ImageDraw, ImageFont


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SVG_DIR = os.path.join(ROOT, "ArtSource", "Icons")
PNG_DIR = os.path.join(ROOT, "Assets", "Resources", "Art", "Icons")
DOCS_DIR = os.path.join(ROOT, "docs", "ConceptArt")
SHEET_PATH = os.path.join(DOCS_DIR, "IconSet-v1.png")
STATS_PATH = os.path.join(SVG_DIR, "icons-v1-stats.json")

for path in (SVG_DIR, PNG_DIR, DOCS_DIR):
    os.makedirs(path, exist_ok=True)

SIZE = 96
STROKE = 7
SUPERSAMPLE = 4

CYAN = "#39D7FF"
CORAL = "#FF675A"
GOLD = "#F0AD3D"
ICE = "#739EE0"
VIOLET = "#968AE6"
PURPLE = "#A87EE0"


def line(x1, y1, x2, y2, width=STROKE):
    return ("line", (x1, y1, x2, y2), width)


def circle(cx, cy, r, width=STROKE, fill=False):
    return ("circle", (cx, cy, r), width, fill)


def dot(cx, cy, r):
    return ("circle", (cx, cy, r), 0, True)


def arc(cx, cy, r, a0, a1, width=STROKE):
    return ("arc", (cx, cy, r, a0, a1), width)


def poly(points, width=STROKE, closed=True, fill=False):
    return ("poly", list(points), width, closed, fill)


def rect(x0, y0, x1, y1, fill=True):
    return ("rect", (x0, y0, x1, y1), 0, fill)


def ray_burst(cx, cy, r0, r1, count=8):
    parts = []
    for k in range(count):
        a = math.radians(k * 360.0 / count)
        parts.append(line(cx + math.cos(a) * r0, cy + math.sin(a) * r0,
                          cx + math.cos(a) * r1, cy + math.sin(a) * r1))
    return parts


ICONS = {
    # ── actions ──
    "jump": [
        line(18, 70, 78, 70),
        line(48, 58, 48, 26),
        poly([(36, 40), (48, 26), (60, 40)], closed=False),
    ],
    "slide": [
        line(18, 26, 78, 26),
        line(26, 62, 60, 62),
        poly([(52, 50), (68, 62), (52, 74)], closed=False),
    ],
    "left": [
        line(70, 48, 28, 48),
        poly([(42, 34), (26, 48), (42, 62)], closed=False),
    ],
    "right": [
        line(26, 48, 68, 48),
        poly([(54, 34), (70, 48), (54, 62)], closed=False),
    ],
    "hold": [
        dot(48, 48, 9),
        circle(48, 48, 24),
    ],
    # ── echo system ──
    "echo": [
        circle(58, 34, 7, width=5),
        arc(58, 66, 13, 180, 360, width=5),
        circle(40, 38, 9, fill=True),
        arc(40, 70, 16, 180, 360),
    ],
    "contract": [
        circle(37, 48, 13),
        circle(59, 48, 13),
        line(28, 68, 68, 28),
    ],
    "generation": [
        poly([(32, 30), (48, 42), (64, 30)], closed=False),
        poly([(32, 45), (48, 57), (64, 45)], closed=False),
        poly([(32, 60), (48, 72), (64, 60)], closed=False),
    ],
    "pace": [
        arc(48, 58, 26, 180, 360),
        line(48, 58, 64, 38),
        dot(48, 58, 4),
        line(26, 74, 70, 74, width=5),
    ],
    "clarity": [
        circle(44, 44, 16),
        line(56, 56, 71, 71),
        dot(44, 44, 4),
    ],
    "stability": [
        poly([(20, 48), (32, 48), (38, 32), (48, 64), (56, 36), (62, 48),
              (76, 48)], closed=False),
    ],
    "lead": [
        line(32, 22, 32, 74),
        poly([(32, 26), (64, 33), (32, 46)], closed=True),
        line(24, 74, 40, 74, width=5),
    ],
    "shard": [
        poly([(48, 22), (68, 48), (48, 74), (28, 48)], closed=True),
        poly([(48, 37), (58, 48), (48, 59), (38, 48)], width=4, closed=True),
    ],
    "victory": [
        poly([(28, 62), (28, 38), (40, 50), (48, 32), (56, 50), (68, 38),
              (68, 62)], closed=True),
        line(28, 70, 68, 70, width=5),
    ],
    "defeat": [
        poly([(48, 24), (68, 32), (68, 52), (48, 74), (28, 52), (28, 32)],
             closed=True),
        poly([(48, 32), (42, 45), (52, 55), (44, 68)], width=5, closed=False),
    ],
    # ── duel phases ──
    "detection": [
        arc(48, 48, 26, 200, 340),
        arc(48, 48, 26, 20, 160),
        dot(48, 48, 7),
        line(18, 48, 30, 48, width=5),
        line(66, 48, 78, 48, width=5),
    ],
    "reveal": [
        poly([(48, 20), (72, 72), (24, 72)], closed=True),
        dot(48, 58, 5),
        line(48, 12, 48, 20, width=5),
    ],
    "resistance": [
        poly([(48, 22), (70, 32), (70, 52), (48, 76), (26, 52), (26, 32)],
             closed=True),
        poly([(38, 48), (46, 58), (62, 36)], width=5, closed=False),
    ],
    "counterattack": ray_burst(48, 48, 13, 28, 8) + [dot(48, 48, 5)],
    "rewrite": [
        arc(48, 48, 22, 55, 355),
        poly([(61, 64), (52, 68), (57, 55)], width=5, closed=False),
        dot(48, 48, 4),
    ],
    "finale": [
        line(30, 22, 30, 74),
        poly([(30, 24), (66, 24), (66, 46), (30, 46)], closed=True),
        rect(36, 29, 42, 35),
        rect(48, 35, 54, 41),
        rect(54, 29, 60, 35),
        line(24, 74, 38, 74, width=5),
    ],
}

ICON_COLORS = {
    "jump": CYAN, "slide": CYAN, "left": CYAN, "right": CYAN, "hold": CYAN,
    "echo": CYAN, "contract": CORAL, "generation": GOLD, "pace": GOLD,
    "clarity": CYAN, "stability": CYAN, "lead": GOLD, "shard": GOLD,
    "victory": GOLD, "defeat": CORAL,
    "detection": ICE, "reveal": VIOLET, "resistance": PURPLE,
    "counterattack": CORAL, "rewrite": CYAN, "finale": GOLD,
}


# ── SVG emission ──

def svg_element(primitive, color):
    kind = primitive[0]
    if kind == "line":
        x1, y1, x2, y2 = primitive[1]
        return ('<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" '
                'stroke-width="%d"/>' % (x1, y1, x2, y2, primitive[2]))
    if kind == "circle":
        cx, cy, r = primitive[1]
        fill = primitive[3]
        if fill:
            return ('<circle cx="%.1f" cy="%.1f" r="%.1f" fill="%s" '
                    'stroke="none"/>' % (cx, cy, r, color))
        return ('<circle cx="%.1f" cy="%.1f" r="%.1f" stroke-width="%d"/>'
                % (cx, cy, r, primitive[2]))
    if kind == "arc":
        cx, cy, r, a0, a1 = primitive[1]
        p0 = (cx + r * math.cos(math.radians(a0)),
              cy + r * math.sin(math.radians(a0)))
        p1 = (cx + r * math.cos(math.radians(a1)),
              cy + r * math.sin(math.radians(a1)))
        large = 1 if (a1 - a0) % 360 > 180 else 0
        return ('<path d="M %.1f %.1f A %.1f %.1f 0 %d 1 %.1f %.1f" '
                'stroke-width="%d"/>'
                % (p0[0], p0[1], r, r, large, p1[0], p1[1], primitive[2]))
    if kind == "poly":
        points, width, closed, fill = (primitive[1], primitive[2],
                                       primitive[3], primitive[4])
        pts = " ".join("%.1f,%.1f" % p for p in points)
        tag = "polygon" if closed else "polyline"
        fill_attr = 'fill="%s" stroke="none"' % color if fill \
            else 'fill="none" stroke-width="%d"' % width
        return '<%s points="%s" %s/>' % (tag, pts, fill_attr)
    if kind == "rect":
        x0, y0, x1, y1 = primitive[1]
        return ('<rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" '
                'fill="%s" stroke="none"/>'
                % (x0, y0, x1 - x0, y1 - y0, color))
    raise ValueError("unknown primitive " + kind)


def write_svg(name, primitives, color):
    body = "\n    ".join(svg_element(p, color) for p in primitives)
    svg = ('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 96 96">\n'
           '  <g stroke="%s" stroke-linecap="round" stroke-linejoin="round" '
           'fill="none">\n    %s\n  </g>\n</svg>\n' % (color, body))
    with open(os.path.join(SVG_DIR, name + ".svg"), "w",
              encoding="utf-8") as handle:
        handle.write(svg)


# ── PNG rasterization ──

def render_png(name, primitives, color):
    big = SIZE * SUPERSAMPLE
    image = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    scale = SUPERSAMPLE

    def s(value):
        return value * scale

    def round_cap(x, y, width):
        r = s(width) / 2.0
        draw.ellipse([s(x) - r, s(y) - r, s(x) + r, s(y) + r], fill=color)

    for primitive in primitives:
        kind = primitive[0]
        if kind == "line":
            x1, y1, x2, y2 = primitive[1]
            width = primitive[2]
            draw.line([s(x1), s(y1), s(x2), s(y2)], fill=color,
                      width=s(width))
            round_cap(x1, y1, width)
            round_cap(x2, y2, width)
        elif kind == "circle":
            cx, cy, r = primitive[1]
            box = [s(cx - r), s(cy - r), s(cx + r), s(cy + r)]
            if primitive[3]:
                draw.ellipse(box, fill=color)
            else:
                draw.ellipse(box, outline=color, width=s(primitive[2]))
        elif kind == "arc":
            cx, cy, r, a0, a1 = primitive[1]
            width = primitive[2]
            draw.arc([s(cx - r), s(cy - r), s(cx + r), s(cy + r)],
                     a0, a1, fill=color, width=s(width))
            round_cap(cx + r * math.cos(math.radians(a0)),
                      cy + r * math.sin(math.radians(a0)), width)
            round_cap(cx + r * math.cos(math.radians(a1)),
                      cy + r * math.sin(math.radians(a1)), width)
        elif kind == "poly":
            points, width, closed, fill = (primitive[1], primitive[2],
                                           primitive[3], primitive[4])
            scaled = [(s(x), s(y)) for x, y in points]
            if fill:
                draw.polygon(scaled, fill=color)
            else:
                seq = scaled + [scaled[0]] if closed else scaled
                draw.line(seq, fill=color, width=s(width), joint="curve")
                for x, y in points:
                    round_cap(x, y, width)
        elif kind == "rect":
            x0, y0, x1, y1 = primitive[1]
            draw.rectangle([s(x0), s(y0), s(x1), s(y1)], fill=color)

    image = image.resize((SIZE, SIZE), Image.LANCZOS)
    image.save(os.path.join(PNG_DIR, name + ".png"))


# ── contact sheet ──

def render_sheet(names):
    cols = 7
    rows = (len(names) + cols - 1) // cols
    cell = 128
    label_band = 22
    width = cols * cell
    height = rows * (cell + label_band) + 20
    sheet = Image.new("RGBA", (width, height), (10, 21, 35, 255))
    font = ImageFont.load_default()
    draw = ImageDraw.Draw(sheet)
    for index, name in enumerate(names):
        col = index % cols
        row = index // cols
        x = col * cell + (cell - SIZE) // 2
        y = row * (cell + label_band) + 10
        icon = Image.open(os.path.join(PNG_DIR, name + ".png"))
        sheet.alpha_composite(icon, (x, y))
        text_width = draw.textlength(name, font=font)
        draw.text((col * cell + (cell - text_width) / 2, y + SIZE + 4),
                  name, fill="#A5BBC8", font=font)
    sheet.save(SHEET_PATH)


names = sorted(ICONS.keys())
for icon_name in names:
    primitives = ICONS[icon_name]
    color = ICON_COLORS[icon_name]
    write_svg(icon_name, primitives, color)
    render_png(icon_name, primitives, color)
render_sheet(names)

stats = {
    "set": "EchoRun Icon Set v1",
    "icons": names,
    "count": len(names),
    "size_px": SIZE,
    "stroke_px": STROKE,
    "supersample": SUPERSAMPLE,
    "svg_dir": SVG_DIR,
    "png_dir": PNG_DIR,
    "sheet": SHEET_PATH,
}
with open(STATS_PATH, "w", encoding="utf-8") as handle:
    json.dump(stats, handle, ensure_ascii=False, indent=2)

print("ECHO_ICON_SET_BUILD_OK")
print(json.dumps(stats, ensure_ascii=False))
