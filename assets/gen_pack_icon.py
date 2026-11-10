# Thunderstore pack icon: camo that flows red -> green -> blue behind three overlapping launcher icons (red, green, blue, left to right).
import numpy as np
from PIL import Image, ImageFilter

SIZE = 512
OUT = "pack_icon.png"


def tiled(name):
    camo = Image.open(name).convert("RGB")
    canvas = Image.new("RGB", (SIZE, SIZE))
    for y in range(0, SIZE, camo.height):
        for x in range(0, SIZE, camo.width):
            canvas.paste(camo, (x, y))
    return np.asarray(canvas).astype(np.float64)


red, green, blue = tiled("camo_red.png"), tiled("camo_green.png"), tiled("camo_blue.png")
t = np.linspace(0, 1, SIZE)[None, :, None] * np.ones((SIZE, 1, 1))
# diagonal flow so the colour change is not a vertical stripe
t = np.clip(t * 0.8 + np.linspace(0, 0.2, SIZE)[:, None, None], 0, 1)
w_red = np.clip(1 - t * 2, 0, 1)
w_blue = np.clip(t * 2 - 1, 0, 1)
w_green = 1 - w_red - w_blue
background = Image.fromarray(np.clip(red * w_red + green * w_green + blue * w_blue, 0, 255).astype(np.uint8)).convert("RGBA")

icons = [Image.open(n).convert("RGBA") for n in ("icon_silver.png", "icon_jump_silver.png", "icon_blue_silver.png")]
ICON = 320
OFFSETS = [(0, 10), (96, 96), (192, 182)]
for icon, (x, y) in zip(icons, OFFSETS):
    icon = icon.resize((ICON, ICON), Image.LANCZOS)
    shadow = Image.new("RGBA", icon.size, (0, 0, 0, 0))
    shadow.putalpha(icon.getchannel("A").point(lambda v: int(v * 0.55)))
    shadow = shadow.filter(ImageFilter.GaussianBlur(8))
    background.alpha_composite(shadow, (x, y + 8))
    background.alpha_composite(icon, (x, y))

background.convert("RGB").save(OUT, optimize=True)
background.convert("RGB").resize((256, 256), Image.LANCZOS).save("pack_icon_256.png", optimize=True)
