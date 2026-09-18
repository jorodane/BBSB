"""Read-only validation of the delivered attribute atlases; requires Pillow.

python Tools/validate_attribute_weapon_art.py /path/to/unzipped-pack
Checks the committed manifest, PNG alpha, complete isolated frames, measured
preview bounds, hollow regions, and exact source hashes. Never edits images.
"""
import hashlib
import json
import sys
from pathlib import Path
from PIL import Image

repo = Path(__file__).resolve().parents[1]
pack = Path(sys.argv[1]) if len(sys.argv) > 1 else repo
manifest = json.loads((repo / 'Docs/weapon-attribute-art-manifest.json').read_text())
frames = holes = 0
assert len(manifest['textures']) == 30
for entry in manifest['textures']:
    path = pack / entry['file']
    assert hashlib.sha256(path.read_bytes()).hexdigest() == entry['sha256'], path
    with Image.open(path) as im:
        assert im.mode == 'RGBA', path
        assert list(im.size) == entry['size'], path
        alpha = im.getchannel('A')
        hist = alpha.histogram()
        assert hist[0] > im.width * im.height * .1 and hist[255] > 100, path
        seen = set()
        for frame in entry['frames']:
            key = (frame['attribute'], frame['pose'])
            assert key not in seen, (path, key)
            seen.add(key)
            x, y, w, h = frame['rect']  # Source pixels, origin bottom left.
            assert 0 <= x < x+w <= im.width and 0 <= y < y+h <= im.height
            tile = alpha.crop((x, im.height-y-h, x+w, im.height-y))
            # Use the same alpha>=8 geometry threshold as the older preview tool.
            # Genuine transparency and hollow samples still require exactly zero.
            box = tile.point(lambda a: 255 if a > manifest['boundsAlphaThreshold'] else 0).getbbox()
            assert box and 0 < box[0] < box[2] < w and 0 < box[1] < box[3] < h, (path, key, 'clipping')
            bx, by, bw, bh = frame['bounds']
            assert bx <= box[0]/w + 1e-8 and by <= (h-box[3])/h + 1e-8
            assert bx+bw >= box[2]/w - 1e-8 and by+bh >= (h-box[1])/h - 1e-8
            # Match the Unity readback test's >240 opaque-body tolerance.
            assert sum(tile.histogram()[241:]) > 100, (path, key, 'empty weapon')
            frames += 1
        for x, y in entry['transparentHoleSamples']:
            assert alpha.getpixel((x, y)) == 0, (path, 'filled hollow region')
            holes += 1
print(f'PASS: {len(manifest["textures"])} RGBA PNGs, {frames} complete frames, {holes} transparent hollow samples; source hashes unchanged.')
