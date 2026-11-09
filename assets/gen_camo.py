# Generates a seamlessly tileable camo_red.png directly at its final size (512x512); the barrel shader maps it triplanar.
import numpy as np
from PIL import Image

W, H = 512, 256

def field(seed, cell):
    r = np.random.default_rng(seed)
    gh, gw = H // cell, W // cell
    g = r.random((gh, gw))
    ys = np.arange(H) / cell
    xs = np.arange(W) / cell
    y0 = ys.astype(int); x0 = xs.astype(int)
    y1 = (y0 + 1) % gh; x1 = (x0 + 1) % gw
    fy = ys - y0; fx = xs - x0
    sy = fy * fy * (3 - 2 * fy); sx = fx * fx * (3 - 2 * fx)
    a = g[np.ix_(y0, x0)]; b = g[np.ix_(y0, x1)]
    c = g[np.ix_(y1, x0)]; d = g[np.ix_(y1, x1)]
    top = a + (b - a) * sx[None, :]
    bot = c + (d - c) * sx[None, :]
    return top + (bot - top) * sy[:, None]

def layer(seed, cell, threshold):
    f = 0.65 * field(seed, cell) + 0.35 * field(seed + 100, cell // 2)
    return np.clip((f - threshold) / 0.012 + 0.5, 0, 1)[..., None]  # ~1px antialiased edge

img = np.empty((H, W, 3), dtype=np.float64)
img[:] = (96, 20, 22)
for seed, cell, thr, col in ((1, 64, 0.52, (158, 32, 34)), (2, 64, 0.58, (60, 12, 15)), (3, 32, 0.64, (30, 26, 26))):
    m = layer(seed, cell, thr)
    img = img * (1 - m) + np.array(col) * m
Image.fromarray(np.clip(img + 0.5, 0, 255).astype(np.uint8)).save("camo_red.png", optimize=True)
