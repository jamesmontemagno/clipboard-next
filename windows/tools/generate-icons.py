"""Regenerates every Windows app, Store, tile, and tray icon for Clipboard Next.

The artwork follows the macOS app icon (ClipboardNext/AppIcon.icon) and the site logo
(site/assets/logo.svg): the purple-to-teal gradient, a clipboard with a queued sheet behind it,
and the "next" arrow. Small sizes use the simpler logo glyph so they stay legible at 16 px.

Run from the repo root:  python windows/tools/generate-icons.py
Requires Pillow (pip install pillow).
"""
import os

from PIL import Image, ImageDraw

ASSETS = os.path.join("windows", "src", "ClipboardNext.App", "Assets")

GRADIENT_START = (0x7C, 0x5C, 0xFF)  # top-left
GRADIENT_END = (0x22, 0xC3, 0xA6)  # bottom-right
CORNER_FRACTION = 0.225  # rounded-square corner radius, as a fraction of the size
WHITE = (255, 255, 255, 255)
RUNNING_GREEN = (0x2E, 0xCC, 0x71, 255)
DETECTED_GREEN = (0x10, 0x7C, 0x10)
DETECTED_GREEN_LIGHT = (0x2E, 0xCC, 0x71)

# Below this size the layered artwork turns to mush; use the plain logo glyph instead.
SIMPLE_GLYPH_BELOW = 48


def _diagonal_mask(size):
    """A grayscale ramp from 0 (top-left) to 255 (bottom-right)."""
    ramp = Image.frombytes("L", (256, 256), bytes((x + y) // 2 for y in range(256) for x in range(256)))
    return ramp.resize((size, size), Image.BILINEAR)


def _background(size, start=GRADIENT_START, end=GRADIENT_END):
    """A full-bleed rounded square filled with the diagonal gradient."""
    gradient = Image.composite(
        Image.new("RGB", (size, size), end), Image.new("RGB", (size, size), start), _diagonal_mask(size)
    ).convert("RGBA")
    shape = Image.new("L", (size, size), 0)
    ImageDraw.Draw(shape).rounded_rectangle([0, 0, size - 1, size - 1], radius=size * CORNER_FRACTION, fill=255)
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    image.paste(gradient, (0, 0), shape)
    return image


def _stroke(draw, points, width, fill=WHITE):
    """A polyline with round caps and joins."""
    draw.line(points, fill=fill, width=round(width), joint="curve")
    radius = width / 2
    for x, y in points:
        draw.ellipse([x - radius, y - radius, x + radius, y + radius], fill=fill)


def _supersample(size):
    return size * (8 if size <= 64 else 4 if size <= 256 else 2)


def layered_icon(size):
    """The macOS app icon's layers (1024-unit canvas) on the gradient background."""
    s = _supersample(size)
    k = s / 1024
    image = _background(s)
    draw = ImageDraw.Draw(image)

    def rect(x, y, w, h, r, fill):
        draw.rounded_rectangle([x * k, y * k, (x + w) * k, (y + h) * k], radius=r * k, fill=fill)

    def circle(cx, cy, r, fill):
        draw.ellipse([(cx - r) * k, (cy - r) * k, (cx + r) * k, (cy + r) * k], fill=fill)

    rect(234, 278, 446, 500, 60, "#b9acff")  # queued sheet
    rect(290, 238, 462, 546, 64, "#f8f8ff")  # clipboard
    for y, w in ((370, 288), (444, 244), (518, 188)):  # snippet lines
        rect(358, y, w, 32, 16, "#a6addb")
    rect(411, 196, 220, 108, 42, "#6954d8")  # clip
    circle(521, 228, 15, "#e1dcff")
    circle(704, 700, 118, "#4931b5")  # next badge
    _stroke(draw, [(651 * k, 700 * k), (755 * k, 700 * k)], 31 * k)
    _stroke(draw, [(718 * k, 663 * k), (755 * k, 700 * k), (718 * k, 737 * k)], 31 * k)
    return image.resize((size, size), Image.LANCZOS)


def simple_icon(size):
    """The site logo (64-unit canvas): clipboard outline, clip, and arrow on the gradient."""
    s = _supersample(size)
    k = s / 64
    image = _background(s)
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle([17 * k, 15 * k, 47 * k, 52 * k], radius=5 * k, outline=WHITE, width=round(4 * k))
    draw.rounded_rectangle([24 * k, 10 * k, 40 * k, 19 * k], radius=3 * k, fill=WHITE)
    _stroke(draw, [(27 * k, 33 * k), (35 * k, 33 * k)], 4 * k)
    _stroke(draw, [(32 * k, 28 * k), (37 * k, 33 * k), (32 * k, 38 * k)], 4 * k)
    return image.resize((size, size), Image.LANCZOS)


def app_icon(size):
    return simple_icon(size) if size < SIMPLE_GLYPH_BELOW else layered_icon(size)


def running_tray_icon(size):
    """The tray icon while a queue is running: the logo with a green dot."""
    s = _supersample(size)
    image = simple_icon(s)
    draw = ImageDraw.Draw(image)
    radius = s * 0.17
    cx = cy = s - radius - s * 0.03
    ring = radius + s * 0.05
    draw.ellipse([cx - ring, cy - ring, cx + ring, cy + ring], fill=(20, 20, 20, 255))
    draw.ellipse([cx - radius, cy - radius, cx + radius, cy + radius], fill=RUNNING_GREEN)
    return image.resize((size, size), Image.LANCZOS)


def detected_tray_icon(size):
    """The tray icon for a moment after a paste is detected: a white check on green."""
    s = _supersample(size)
    image = _background(s, DETECTED_GREEN_LIGHT, DETECTED_GREEN)
    k = s / 64
    _stroke(ImageDraw.Draw(image), [(18 * k, 33 * k), (28 * k, 43 * k), (46 * k, 22 * k)], 7 * k)
    return image.resize((size, size), Image.LANCZOS)


def centered(width, height, icon_fraction=0.5):
    """The app icon centered on a transparent canvas, for wide tiles and the splash screen."""
    canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    icon = app_icon(round(min(width, height) * icon_fraction))
    canvas.alpha_composite(icon, ((width - icon.width) // 2, (height - icon.height) // 2))
    return canvas


def save_png(image, name):
    image.save(os.path.join(ASSETS, name), optimize=True)


def save_ico(render, name, sizes):
    frames = [render(size) for size in sizes]
    frames[-1].save(os.path.join(ASSETS, name), format="ICO", append_images=frames[:-1],
                    sizes=[(size, size) for size in sizes])


def main():
    os.makedirs(ASSETS, exist_ok=True)
    scales = {100: 1.0, 125: 1.25, 150: 1.5, 200: 2.0, 400: 4.0}

    for base, logo in (("StoreLogo", 50), ("Square44x44Logo", 44), ("Square150x150Logo", 150),
                       ("SmallTile", 71), ("LargeTile", 310)):
        for scale, factor in scales.items():
            save_png(app_icon(round(logo * factor)), f"{base}.scale-{scale}.png")
    save_png(app_icon(50), "StoreLogo.png")

    for scale, factor in scales.items():
        save_png(centered(round(310 * factor), round(150 * factor), 0.6), f"Wide310x150Logo.scale-{scale}.png")
        save_png(centered(round(620 * factor), round(300 * factor), 0.5), f"SplashScreen.scale-{scale}.png")

    # Taskbar, Start, and Alt+Tab icons. The artwork carries its own background, so the plated,
    # unplated, and light-unplated variants are the same image.
    for target in (16, 24, 32, 48, 256):
        icon = app_icon(target)
        for suffix in ("", "_altform-unplated", "_altform-lightunplated"):
            save_png(icon, f"Square44x44Logo.targetsize-{target}{suffix}.png")

    save_png(app_icon(256), "AppIcon-256.png")
    save_ico(app_icon, "AppIcon.ico", [16, 20, 24, 32, 40, 48, 64, 256])
    tray_sizes = [16, 20, 24, 32, 40, 48]
    save_ico(simple_icon, "TrayIcon.ico", tray_sizes)
    save_ico(running_tray_icon, "TrayIconRunning.ico", tray_sizes)
    save_ico(detected_tray_icon, "TrayIconDetected.ico", tray_sizes)

    print(f"Wrote {len(os.listdir(ASSETS))} files to {ASSETS}")


if __name__ == "__main__":
    main()
