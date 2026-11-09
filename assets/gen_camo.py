import numpy as np
from PIL import Image, ImageFilter
rng = np.random.default_rng(7)
N = 1024
def noise(scale, seed):
    r = np.random.default_rng(seed)
    g = r.random((N // scale + 2, N // scale + 2)).astype(np.float32)
    im = Image.fromarray((g * 255).astype(np.uint8)).resize((N, N), Image.BICUBIC)
    return np.asarray(im, dtype=np.float32) / 255
base  = np.array([92, 18, 20])    # dark red
c1    = np.array([150, 28, 30])   # bright red
c2    = np.array([58, 10, 14])    # deep maroon
c3    = np.array([28, 24, 24])    # near-black
n1 = noise(96, 1) * .6 + noise(48, 2) * .4
n2 = noise(80, 3) * .6 + noise(32, 4) * .4
n3 = noise(64, 5) * .6 + noise(24, 6) * .4
img = np.tile(base, (N, N, 1)).astype(np.float32)
for mask, col in ((n1 > .56, c1), (n2 > .60, c2), (n3 > .66, c3)):
    m = Image.fromarray((mask * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2))
    m = (np.asarray(m, dtype=np.float32) / 255)[..., None]
    img = img * (1 - m) + col * m
grain = (rng.random((N, N, 1)) - .5) * 14
img = np.clip(img + grain, 0, 255).astype(np.uint8)
Image.fromarray(img).save("camo_red.png")
