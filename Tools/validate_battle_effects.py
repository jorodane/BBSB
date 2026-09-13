"""Read-only validation for the ranged weapons and battle VFX pack. Requires Pillow.

python Tools/validate_battle_effects.py [project-or-unzipped-pack-root]
"""
import json
import sys
from pathlib import Path
from PIL import Image

repo = Path(__file__).resolve().parents[1]
root = Path(sys.argv[1]) if len(sys.argv) > 1 else repo
manifest = json.loads((repo / 'Docs/battle-effects-manifest.json').read_text())
failures = []
frames = sockets = 0
for asset in manifest['assets']:
    try:
        with Image.open(root / asset['file']) as image:
            assert image.format == 'PNG' and image.mode == 'RGBA', 'genuine RGBA PNG required'
            width, height = image.size
            assert height >= 512, 'source height must be at least 512 px'
            alpha = image.getchannel('A'); hist = alpha.histogram()
            assert hist[0] > width * height * .10, 'no meaningful transparent background'
            assert sum(hist[240:]) > width * height * .01, 'artwork is missing or too faint'
            for x, y in [(0, 0), (width - 1, 0), (0, height - 1), (width - 1, height - 1)]:
                assert alpha.getpixel((x, y)) == 0, 'opaque canvas corner'
            if asset['kind'] == 'vfx':
                assert width == height, 'VFX must be square'
                continue
            assert abs(width / height - 3) < .01, 'ranged atlas must have three square cells'
            assert [frame['pose'] for frame in asset['frames']] == ['Idle', 'Prepare', 'Release'], 'pose order changed'
            count = {'common': 1, 'rare': 2, 'epic': 2, 'legendary': 3}[asset['rarity']]
            for index, frame in enumerate(asset['frames']):
                frames += 1
                left, right = (round(width * value) for value in frame['slice'])
                cell = right - left
                assert len(frame['sockets']) == count, 'wrong socket count'
                # Tiny subpixel antialias remnants are allowed; solid artwork must not cross a frame boundary.
                for box in [(left, 0, left + 2, height), (right - 2, 0, right, height)]:
                    assert sum(alpha.crop(box).histogram()[200:]) == 0, 'weapon crosses a frame boundary'
                assert 0 < frame['muzzle'][0] < 1 and 0 < frame['muzzle'][1] < 1, 'muzzle outside its frame'
                for socket in frame['sockets']:
                    sockets += 1
                    for dx, dy in [(0, 0), (.60, 0), (-.60, 0), (0, .60), (0, -.60)]:
                        x = left + round((socket['x'] + dx * socket['radiusX']) * cell - .5)
                        y = round((1 - socket['y'] - dy * socket['radiusY']) * height - .5)
                        r, g, b, a = image.getpixel((x, y))
                        assert a > 240 and min(r, g, b) > 115 and max(r, g, b) - min(r, g, b) < 65, 'socket misses its opaque silver disc'
    except (AssertionError, OSError, KeyError, ValueError) as error:
        failures.append(f'{asset["file"]}: {error}')
if failures:
    print('\n'.join(failures)); raise SystemExit(1)
assert len(manifest['assets']) == 22 and frames == 36 and sockets == 72, 'incomplete pack'
print(f'{len(manifest["assets"])} transparent PNGs, {frames} ranged poses and {sockets} socket positions verified')
