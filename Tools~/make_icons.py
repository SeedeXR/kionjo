"""Draws the Kionjo icon set from the same shapes as Tools~/kionjo.svg.

The mark: a film frame that is also a headset visor. A rounded frame with a nose bridge notch
in its lower edge, and two lenses inside it. Drawn on a 1024 px canvas (a 16 unit grid) with Pillow, then
downsampled with LANCZOS, so no SVG renderer is needed.

    python Tools~/make_icons.py
"""
import os
import sys

from PIL import Image, ImageChops, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "Editor", "Icons")

LIGHT = (0x55, 0x55, 0x55, 255)  # light editor skin
DARK = (0xC4, 0xC4, 0xC4, 255)   # dark editor skin
ACCENT = (0xD9, 0x43, 0x3B, 255) # record red, asset icon only


def draw(size_units, colour, accent=None):
    px = 1024  # supersampled canvas, at least 4x the largest output, keeps edges smooth
    img = Image.new("RGBA", (px, px), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    u = px / 16.0  # one grid unit

    # Frame: rounded rectangle from (1.5, 3.5) to (14.5, 12.5), radius 2.5, stroke 1.5, with a
    # nose bridge: the stroke bends up around an arch centred on the lower edge. Built from masks
    # (outer silhouette minus nose) minus (inner silhouette minus a bigger nose) so the stroke
    # keeps its width all the way round.
    stroke = 1.5
    def mask(rect, radius, nose_rx, nose_ry):
        m = Image.new("L", (px, px), 0)
        md = ImageDraw.Draw(m)
        md.rounded_rectangle([v * u for v in rect], radius=radius * u, fill=255)
        md.ellipse([(8 - nose_rx) * u, (12.5 - nose_ry) * u, (8 + nose_rx) * u, (12.5 + nose_ry) * u], fill=0)
        return m
    outer = mask([1.5, 3.5, 14.5, 12.5], 2.5, 1.5, 1.7)
    inner = mask([1.5 + stroke, 3.5 + stroke, 14.5 - stroke, 12.5 - stroke], 2.5 - stroke, 1.5 + stroke, 1.7 + stroke)
    frame = ImageChops.subtract(outer, inner)
    img.paste(Image.new("RGBA", (px, px), colour), (0, 0), frame)

    # Lenses
    r = 1.7 * u
    for cx in (5.4, 10.6):
        d.ellipse([cx * u - r, 7.6 * u - r, cx * u + r, 7.6 * u + r], fill=colour)

    if accent is not None:  # small record dot for the storyboard asset icon
        rr = 1.4 * u
        d.ellipse([13.6 * u - rr, 2.6 * u - rr, 13.6 * u + rr, 2.6 * u + rr], fill=accent)
    return img


def save(img, name, size):
    path = os.path.join(OUT, name)
    img.resize((size, size), Image.LANCZOS).save(path)
    print(path, size)


def main():
    os.makedirs(OUT, exist_ok=True)
    light, dark = draw(16, LIGHT), draw(16, DARK)
    save(light, "kionjo.png", 16)
    save(light, "kionjo@2x.png", 32)
    save(dark, "d_kionjo.png", 16)
    save(dark, "d_kionjo@2x.png", 32)
    asset = draw(16, (0x8A, 0x8A, 0x8A, 255), ACCENT)
    save(asset, "kionjo-asset.png", 64)
    save(asset, "kionjo-asset@2x.png", 128)
    save(draw(16, (0x2B, 0x2B, 0x2B, 255), ACCENT), "kionjo-logo.png", 256)
    return 0


if __name__ == "__main__":
    sys.exit(main())
