"""Compose promotional PNGs around the real Windows screenshots.

Run: python windows\\tools\\generate-store-marketing.py
Requires Pillow, as does generate-icons.py. Uses the website palette and Windows fonts.
Original captures are read-only; outputs go into docs/store-assets/marketing/.
"""

from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont


ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "docs" / "store-assets"
OUTPUT = ASSETS / "marketing"
FONTS = Path("C:\\Windows\\Fonts")
SIZE = (1920, 1200)
SCALE = 2
INK = "#0d0f1a"
WHITE = "#eef0ff"
MUTED = "#b0b6d5"
MINT = "#5ce0c7"
VIOLET = "#b9a8ff"


def box(bounds):
    return tuple(round(value * SCALE) for value in bounds)


def text(image, position, value, size, color=WHITE, bold=False, max_width=None):
    font = ImageFont.truetype(str(FONTS / ("segoeuib.ttf" if bold else "segoeui.ttf")), size * SCALE)
    draw = ImageDraw.Draw(image)
    if max_width is not None and draw.textlength(value, font=font) > max_width * SCALE:
        raise ValueError(f"Text exceeds its layout width: {value!r}")
    draw.text(box(position), value, font=font, fill=color, anchor="lt")


def lines(image, position, values, size, leading, color=MUTED, bold=False, max_width=360):
    x, y = position
    for index, value in enumerate(values):
        text(image, (x, y + index * leading), value, size, color, bold, max_width)


def background():
    image = Image.new("RGB", tuple(value * SCALE for value in SIZE), INK)
    for bounds, color, opacity, blur in (
        ((760, -420, 2220, 880), "#7c5cff", 70, 220),
        ((1000, 620, 2200, 1480), "#22c3a6", 48, 180),
    ):
        mask = Image.new("L", image.size)
        ImageDraw.Draw(mask).ellipse(box(bounds), fill=opacity)
        mask = mask.filter(ImageFilter.GaussianBlur(blur * SCALE))
        image.paste(Image.new("RGB", image.size, color), (0, 0), mask)
    return image


def panel(image, content, position, width, radius=12, shadow_blur=24, shadow_offset=20):
    x, y = position
    height = round(width * content.height / content.width)
    if x < 0 or y < 0 or x + width > SIZE[0] or y + height > SIZE[1]:
        raise ValueError("Screenshot or detail would be clipped by the canvas.")
    shadow = Image.new("L", image.size)
    ImageDraw.Draw(shadow).rounded_rectangle(
        box((x, y + shadow_offset, x + width, y + height + shadow_offset)),
        radius=radius * SCALE,
        fill=165,
    )
    shadow = shadow.filter(ImageFilter.GaussianBlur(shadow_blur * SCALE))
    image.paste(Image.new("RGB", image.size, "#000000"), (0, 0), shadow)
    content = content.resize((width * SCALE, height * SCALE), Image.Resampling.LANCZOS)
    mask = Image.new("L", content.size)
    ImageDraw.Draw(mask).rounded_rectangle(
        (0, 0, content.width - 1, content.height - 1), radius=radius * SCALE, fill=255
    )
    image.paste(content, box(position), mask)


def header(image, step):
    with Image.open(ROOT / "src" / "ClipboardNext.App" / "Assets" / "AppIcon-256.png") as source:
        icon = source.convert("RGBA").resize((48 * SCALE, 48 * SCALE), Image.Resampling.LANCZOS)
    image.paste(icon, box((84, 48)), icon)
    text(image, (148, 57), "Clipboard Next", 30, bold=True)
    text(image, (1660, 64), "For Windows 11", 22, MUTED, max_width=190)
    ImageDraw.Draw(image).line(box((84, 116, 1836, 116)), fill="#2a2f4a", width=SCALE)
    for index, (label, x) in enumerate((("Prepare", 1212), ("Import", 1362), ("Present", 1490))):
        text(image, (x, 65), label, 22, MINT if index == step else MUTED, bold=index == step)


def feature(image, y, title, description, accent):
    text(image, (84, y), title, 30, accent, bold=True, max_width=370)
    lines(image, (84, y + 50), description, 26, 39)


def shortcut(image):
    draw = ImageDraw.Draw(image)
    for x, width, label in ((84, 142, "Ctrl"), (276, 98, "V")):
        draw.rounded_rectangle(
            box((x, 650, x + width, 738)),
            radius=12 * SCALE,
            fill="#1a1e33",
            outline="#444b70",
            width=SCALE,
        )
        text(image, (x + 25, 671), label, 40, bold=True)
    text(image, (242, 678), "+", 29, MUTED)


def compose(filename, headline, accent, step):
    with Image.open(ASSETS / filename) as source:
        capture = source.convert("RGB")
    bounds = capture.getbbox()
    if bounds is None:
        raise ValueError(f"The source screenshot is blank: {filename}")
    # PrintWindow's black outer gutters are not part of the app window.
    screenshot = capture.crop(bounds)
    image = background()
    header(image, step)
    text(image, (80, 152), headline[0], 94, bold=True, max_width=1756)
    text(image, (80, 258), headline[1], 94, accent, bold=True, max_width=1756)
    panel(image, screenshot, (576, 360), 1260)

    if step == 0:
        lines(image, (84, 427), ("Commands. Code. Prompts.", "Ready before you take", "the stage."), 28, 42,
              max_width=390)
        feature(image, 646, "Code, not fragments.", ("Multiline snippets.", "Exactly as you wrote them."), accent)
        feature(image, 856, "Your order. Your pace.", ("Title, edit, and reorder", "every step of your demo."), accent)
    elif step == 1:
        lines(image, (84, 427), ("Your presenter notes.", "Now a paste-ready queue."), 28, 42, max_width=390)
        feature(image, 646, "Whole code blocks.", ("Keep multiline code intact.", "No retyping required."), accent)
        feature(image, 856, "Preview. Then choose.", ("Pick the snippets and", "prompts you want to bring."), accent)
    else:
        lines(image, (84, 427), ("Turn on paste detection.", "Paste, and the next snippet", "is copied for you."), 28, 42,
              max_width=390)
        shortcut(image)
        lines(image, (84, 765), ("Automatic advancing", "is optional. You're in control."), 25, 37)
        # Enlarge only the real badge pixels, not a reconstruction of its UI.
        panel(image, capture.crop((1420, 100, 1550, 140)), (84, 900), 300,
              radius=10, shadow_blur=14, shadow_offset=10)
        text(image, (84, 1012), "Your place, at a glance.", 27, accent, bold=True, max_width=370)
        text(image, (84, 1054), "Enlarged badge detail", 20, MUTED)

    text(image, (84, 1146), "Free. Open source. Local only.", 22, MUTED, max_width=390)
    image = image.resize(SIZE, Image.Resampling.LANCZOS)
    path = OUTPUT / filename
    image.save(path, optimize=True)
    with Image.open(path) as exported:
        if exported.size != SIZE or exported.mode != "RGB" or exported.format != "PNG":
            raise ValueError(f"Invalid promotional export: {path}")
    if path.stat().st_size > 50 * 1024 * 1024:
        raise ValueError(f"Promotional export exceeds 50 MB: {path}")
    print(f"{path.name}: {SIZE[0]} x {SIZE[1]}, RGB PNG, {path.stat().st_size:,} bytes")


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    compose("01-demo-queue.png", ("Your demo.", "Already queued."), MINT, 0)
    compose("02-markdown-import.png", ("From notes", "to showtime."), VIOLET, 1)
    compose("03-presenting.png", ("Less switching.", "More presenting."), MINT, 2)


if __name__ == "__main__":
    main()
