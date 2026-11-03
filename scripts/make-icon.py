"""Renders src/DevMonitor.App/Assets/Icon.ico: a code bracket pair around a pulse line.

Sizes from 48 px show both brackets and the pulse; smaller sizes keep only a heavier pulse so it stays legible in the tray and taskbar.
Run with: python scripts/make-icon.py [preview.png]
"""
import os
import sys

from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TARGET = os.path.join(ROOT, "src", "DevMonitor.App", "Assets", "Icon.ico")
SIZES = [16, 20, 24, 32, 40, 48, 64, 128, 256]
TOP = (22, 140, 156)
BOTTOM = (11, 86, 99)
GLYPH = (255, 255, 255, 255)

BRACKETS = [[(0.29, 0.33), (0.165, 0.5), (0.29, 0.67)], [(0.71, 0.33), (0.835, 0.5), (0.71, 0.67)]]
PULSE = [(0.345, 0.52), (0.42, 0.52), (0.47, 0.29), (0.545, 0.73), (0.595, 0.52), (0.655, 0.52)]
SMALL_PULSE = [(0.16, 0.53), (0.34, 0.53), (0.44, 0.25), (0.58, 0.79), (0.68, 0.53), (0.84, 0.53)]


def background(size):
    gradient = Image.new("RGBA", (size, size))
    pixels = gradient.load()
    for y in range(size):
        t = y / (size - 1)
        color = tuple(round(TOP[i] + (BOTTOM[i] - TOP[i]) * t) for i in range(3)) + (255,)
        for x in range(size):
            pixels[x, y] = color
    mask = Image.new("L", (size, size), 0)
    inset = round(size * 0.03)
    ImageDraw.Draw(mask).rounded_rectangle([inset, inset, size - inset, size - inset], radius=round(size * 0.22), fill=255)
    tile = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    tile.paste(gradient, (0, 0), mask)
    return tile


def polyline(draw, points, size, width):
    scaled = [(x * size, y * size) for x, y in points]
    draw.line(scaled, fill=GLYPH, width=width, joint="curve")
    radius = width / 2
    for x, y in (scaled[0], scaled[-1]):
        draw.ellipse([x - radius, y - radius, x + radius, y + radius], fill=GLYPH)


def render(size):
    scale = 8
    canvas = size * scale
    image = background(canvas)
    draw = ImageDraw.Draw(image)
    if size >= 48:
        width = round(canvas * 0.06)
        for bracket in BRACKETS:
            polyline(draw, bracket, canvas, width)
        polyline(draw, PULSE, canvas, width)
    else:
        polyline(draw, SMALL_PULSE, canvas, round(canvas * (0.13 if size < 32 else 0.105)))
    return image.resize((size, size), Image.LANCZOS)


def main():
    images = [render(size) for size in SIZES]
    images[-1].save(TARGET, format="ICO", sizes=[(size, size) for size in SIZES], append_images=images[:-1])
    print(f"wrote {TARGET}")
    if len(sys.argv) > 1:
        sheet = Image.new("RGBA", (sum(SIZES) + 10 * len(SIZES) + 10, 276), (40, 44, 52, 255))
        x = 10
        for image in images:
            sheet.paste(image, (x, 10), image)
            x += image.width + 10
        sheet.save(sys.argv[1])
        print(f"preview {sys.argv[1]}")


if __name__ == "__main__":
    main()
