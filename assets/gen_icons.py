# Recolors the original Anti-Zooka inventory icon: camo barrel + silver or original gold frame.
import colorsys
import numpy as np
from PIL import Image, ImageFilter

SRC = r"C:/Users/Capitan Salat/AppData/Local/Temp/claude/de6a1c707e105dc5/c835c4aa-a02e-4e4d-b665-0097da9918a0/scratchpad/tex/icon_antizooka.png"
im = Image.open(SRC).convert("RGBA")
a = np.asarray(im).astype(np.float64) / 255
rgb, alpha = a[..., :3], a[..., 3]
mx, mn = rgb.max(-1), rgb.min(-1)
val, sat = mx, np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0)
r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
d = np.maximum(mx - mn, 1e-6)
hue = np.where(mx == r, ((g - b) / d) % 6, np.where(mx == g, (b - r) / d + 2, (r - g) / d + 4)) * 60

gold = (hue >= 15) & (hue <= 65) & (sat > 0.35) & (val > 0.25)
barrel = (((hue >= 190) & (hue <= 330) & (sat > 0.12)) | ((hue >= 140) & (hue < 190) & (sat > 0.2))) & (val < 0.5) & (val > 0.07)
closed = Image.fromarray((barrel * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(7)).filter(ImageFilter.MinFilter(7))
barrel = (np.asarray(closed) > 127) & ~gold & (val > 0.1) & (alpha > 0.5)

lum = rgb @ [0.299, 0.587, 0.114]
camo = np.asarray(Image.open("camo_red.png").convert("RGB").resize((160, 160), Image.LANCZOS)).astype(np.float64) / 255
H, W = lum.shape
yy, xx = np.mgrid[0:H, 0:W]
camo_pixels = camo[yy % 160, xx % 160]
shade = np.clip(0.85 + (lum[..., None] - 0.12) * 1.2, 0.8, 1.15)

def build(silver):
    out = rgb.copy()
    out[barrel] = np.clip(camo_pixels * shade, 0, 1)[barrel]
    if silver:
        grey = np.clip((lum - 0.1) * 1.25 + 0.2, 0, 1)
        silver_rgb = np.stack([grey * 0.95, grey * 0.97, grey * 1.04], -1)
        out[gold] = np.clip(silver_rgb, 0, 1)[gold]
    res = np.concatenate([out, alpha[..., None]], -1)
    return Image.fromarray((res * 255 + 0.5).astype(np.uint8))

build(True).save("icon_silver.png", optimize=True)
build(False).save("icon_gold.png", optimize=True)
