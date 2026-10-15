"""Rebuild committed Windows icon assets from the selected source. Requires Pillow (development only)."""
from collections import deque
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[1]
image = Image.open(root / "branding/lable-studio-source.png").convert("RGBA")
width, height = image.size
pixels = image.load()
# Remove only edge-connected white background, not the white label inside the navy tile.
queue = deque([(x, 0) for x in range(width)] + [(x, height - 1) for x in range(width)]
              + [(0, y) for y in range(height)] + [(width - 1, y) for y in range(height)])
seen = set()
while queue:
    x, y = queue.popleft()
    if (x, y) in seen or not (0 <= x < width and 0 <= y < height):
        continue
    seen.add((x, y))
    r, g, b, a = pixels[x, y]
    if min(r, g, b) < 100:
        continue
    # Unmatte antialiased outer edges against their original white background.
    alpha = max(0, min(1, (255 - min(r, g, b)) / (255 - 8)))
    if min(r, g, b) >= 250:
        pixels[x, y] = (0, 0, 0, 0)
    else:
        rgb = tuple(round(max(0, min(255, (c - 255 * (1 - alpha)) / alpha))) for c in (r, g, b))
        pixels[x, y] = (*rgb, round(255 * alpha))
    queue.extend(((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)))

assets = root / "src/ExcelSticker.Wpf/Assets"
assets.mkdir(parents=True, exist_ok=True)
logo = image.resize((256, 256), Image.Resampling.LANCZOS)
logo.save(assets / "lable-studio.png", optimize=True)
image.save(assets / "lable-studio.ico", format="ICO", sizes=[(s, s) for s in (16, 20, 24, 32, 40, 48, 64, 128, 256)])
print("Created 256px transparent logo and 9-frame Windows icon (16–256px).")
