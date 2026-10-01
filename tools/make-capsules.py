#!/usr/bin/env python3
"""Placeholder Steam capsule art at Steam's sizes, into docs/steam/.

Built from game screenshots (docs/demo/) and the title — good enough for a
"Coming Soon" page while real key art is made. Needs Pillow.

    python3 tools/make-capsules.py
"""
import os

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.join(os.path.dirname(__file__), "..")
DEMO = os.path.join(ROOT, "docs", "demo")
OUT = os.path.join(ROOT, "docs", "steam")
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSerif-Bold.ttf"
FONT_SMALL = "/usr/share/fonts/truetype/dejavu/DejaVuSerif.ttf"
GOLD = (255, 214, 120)
TAGLINE = "Its people — and its gods — run on your own computer."


def background(size, shot="07-animals.png"):
    """A screenshot, cropped to fill, scaled with hard pixels, darkened toward the edges."""
    src = Image.open(os.path.join(DEMO, shot)).convert("RGB")
    src = src.crop((0, 50, src.width, src.height - 115))  # drop caption bar, HUD and status line
    w, h = size
    scale = max(w / src.width, h / src.height)
    img = src.resize((int(src.width * scale) + 1, int(src.height * scale) + 1), Image.NEAREST)
    left, top = (img.width - w) // 2, (img.height - h) // 2
    img = img.crop((left, top, left + w, top + h))

    # vignette + overall darken so the title reads
    shade = Image.new("L", size, 0)
    d = ImageDraw.Draw(shade)
    for i in range(40):
        a = int(255 * (i / 40) ** 1.6)
        d.rectangle([i * w // 80, i * h // 80, w - i * w // 80, h - i * h // 80], fill=a)
    shade = shade.filter(ImageFilter.GaussianBlur(max(w, h) // 25))
    dark = Image.new("RGB", size, (8, 7, 10))
    # keep the world visible: at least ~55% everywhere, full in the middle
    img = Image.composite(img, dark, shade.point(lambda v: 140 + int(v * 115 / 255)))
    # a soft dark band behind the title so it reads over busy scenery
    band = Image.new("L", size, 0)
    ImageDraw.Draw(band).ellipse([w * 0.12, h * 0.28, w * 0.88, h * 0.72], fill=120)
    band = band.filter(ImageFilter.GaussianBlur(max(w, h) // 12))
    img = Image.composite(dark, img, band)
    return img.convert("RGBA")


def title(img, text_px, y_frac=0.5, tagline=True):
    w, h = img.size
    font = ImageFont.truetype(FONT, text_px)
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    text = "AIN SOPH"
    tw = d.textlength(text, font=font)
    x, y = (w - tw) / 2, h * y_frac - text_px * 0.6
    # glow
    glow = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ImageDraw.Draw(glow).text((x, y), text, font=font, fill=(255, 190, 80, 200))
    glow = glow.filter(ImageFilter.GaussianBlur(text_px / 7))
    img.alpha_composite(glow)
    d.text((x, y), text, font=font, fill=GOLD + (255,))
    if tagline and text_px >= 60:
        small = ImageFont.truetype(FONT_SMALL, max(14, text_px // 5))
        tw2 = d.textlength(TAGLINE, font=small)
        d.text(((w - tw2) / 2, y + text_px * 1.25), TAGLINE, font=small, fill=(235, 228, 205, 255))
    img.alpha_composite(layer)
    return img


def save(img, name):
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, name)
    img.save(path)
    print("wrote", os.path.relpath(path, ROOT), img.size)


if __name__ == "__main__":
    save(title(background((920, 430)), 96), "header_capsule.png")
    save(title(background((462, 174)), 58, tagline=False), "small_capsule.png")
    save(title(background((1232, 706), "03-npcs.png"), 130), "main_capsule.png")
    save(title(background((748, 896), "03-npcs.png"), 104, y_frac=0.42), "vertical_capsule.png")
    save(title(background((600, 900)), 86, y_frac=0.4), "library_capsule.png")
    save(background((3840, 1240), "07-animals.png"), "library_hero.png")  # no text allowed

    logo = Image.new("RGBA", (1280, 720), (0, 0, 0, 0))
    save(title(logo, 190, tagline=False), "library_logo.png")
