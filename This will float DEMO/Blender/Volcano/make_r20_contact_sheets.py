from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parent
BASE = ROOT / "MaterialControlMasks_R20"
DIAG = BASE / "Diagnostics"
RENDERS = BASE / "Renders"
TEXTURES = BASE / "Textures"


def font(size):
    for path in (
        "/System/Library/Fonts/Supplemental/Arial Bold.ttf",
        "/System/Library/Fonts/Helvetica.ttc",
    ):
        try:
            return ImageFont.truetype(path, size=size)
        except OSError:
            pass
    return ImageFont.load_default()


def sheet(items, output, columns, thumb_size):
    label_h = 48
    rows = (len(items) + columns - 1) // columns
    canvas = Image.new("RGB", (columns * thumb_size[0], rows * (thumb_size[1] + label_h)), (20, 22, 24))
    draw = ImageDraw.Draw(canvas)
    label_font = font(24)
    for index, (label, path) in enumerate(items):
        image = Image.open(path).convert("RGB")
        image.thumbnail(thumb_size, Image.Resampling.LANCZOS)
        x = (index % columns) * thumb_size[0]
        y = (index // columns) * (thumb_size[1] + label_h)
        px = x + (thumb_size[0] - image.width) // 2
        py = y + label_h + (thumb_size[1] - image.height) // 2
        canvas.paste(image, (px, py))
        draw.text((x + 14, y + 10), label, font=label_font, fill=(238, 238, 238))
    canvas.save(output)


sheet(
    (
        ("1. Current R19 Albedo", DIAG / "01_Current_R19_Albedo.png"),
        ("2. Revised R20 Albedo", TEXTURES / "Volcano_Hero_R20_Albedo.png"),
        ("3. Control RGBA (RGB preview)", DIAG / "03_ControlRGBA_RGBPreview.png"),
        ("4. R — Rock + Ash", DIAG / "R20_Channel_R_RockAsh.png"),
        ("5. G — Vegetation Ground", DIAG / "R20_Channel_G_Vegetation.png"),
        ("6. B — Bare Soil", DIAG / "R20_Channel_B_Soil.png"),
        ("7. A — Summit + Crater", DIAG / "R20_Channel_A_Summit.png"),
    ),
    DIAG / "R20_TextureDiagnostics_ContactSheet.png",
    4,
    (520, 520),
)

sheet(
    (
        ("8. Front", RENDERS / "R20_01_front.png"),
        ("9. Left", RENDERS / "R20_02_left.png"),
        ("10. Right", RENDERS / "R20_03_right.png"),
        ("11. 45-degree Top", RENDERS / "R20_04_top45.png"),
        ("12. Direct Top", RENDERS / "R20_05_direct_top.png"),
    ),
    DIAG / "R20_RenderValidation_ContactSheet.png",
    3,
    (600, 420),
)

print("R20_CONTACT_SHEETS_COMPLETE")
