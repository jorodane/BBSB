"""Validate the delivered weapon pack without changing any pixels. Requires Pillow.

python Tools/validate_weapon_art.py [project-or-unzipped-pack-root]
"""
import json
import sys
from pathlib import Path
from PIL import Image

repo = Path(__file__).resolve().parents[1]
root = Path(sys.argv[1]) if len(sys.argv) > 1 else repo
manifest = json.loads((repo / 'Docs/weapon-art-manifest.json').read_text())
failures = []
for asset in manifest['assets']:
    path = root / asset['file']
    try:
        with Image.open(path) as image:
            assert image.format == 'PNG' and image.mode == 'RGBA', 'real RGBA PNG required'
            width, height = image.size
            assert width == height and width >= 512, 'complete square image of at least 512 px required'
            alpha = image.getchannel('A')
            histogram = alpha.histogram()
            assert histogram[0] > width * height * .03, 'no meaningful transparent background'
            assert sum(histogram[240:]) > width * height * .015, 'weapon is missing or too faint'
            for point in [(0, 0), (width - 1, 0), (0, height - 1), (width - 1, height - 1)]:
                assert alpha.getpixel(point) == 0, 'opaque canvas corner'
            for socket in asset['sockets']:
                for dx, dy in [(0, 0), (.65, 0), (-.65, 0), (0, .65), (0, -.65)]:
                    x = round((socket['x'] + dx * socket['radiusX']) * width - .5)
                    y = round((1 - socket['y'] - dy * socket['radiusY']) * height - .5)
                    r, g, b, a = image.getpixel((x, y))
                    assert a > 240 and min(r, g, b) > 125 and max(r, g, b) - min(r, g, b) < 60, 'socket coordinates miss the pale circular opening'
    except (AssertionError, OSError, KeyError, ValueError) as error:
        failures.append(f'{asset["file"]}: {error}')

if failures:
    print('\n'.join(failures))
    raise SystemExit(1)
print(f'{len(manifest["assets"])} transparent PNGs and {sum(len(a["sockets"]) for a in manifest["assets"])} socket positions verified')
