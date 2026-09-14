"""Regenerate preview framing from weapon alpha; never modifies artwork. Requires Pillow.

python Tools/measure_weapon_preview_bounds.py [--check]
"""
from pathlib import Path
import re
import sys
from PIL import Image

root = Path(__file__).resolve().parents[1]
ui = root / 'Assets/BBSB/Runtime/UI'
layout = (ui / 'RangedWeaponArtLayout.cs').read_text()
overrides = {
    key: [float(v.strip()) for v in values.split(',')]
    for key, values in re.findall(r'\{ "([^"\n]+)", new\[\] \{ ([0-9., ]+) \} \}', layout)
}
entries = []
for path in sorted((root / 'Assets/BBSB/Resources/BBSB/WeaponArt').glob('*/*.png')):
    key = f'{path.parent.name}/{path.stem}'
    image = Image.open(path)
    ranged = path.parent.name in ('bow', 'crossbow', 'wand')
    edges = overrides.get(key, [0, 1 / 3, 2 / 3, 1]) if ranged else [0, 1]
    for pose, (left, right) in enumerate(zip(edges, edges[1:])):
        frame = image.crop((round(image.width * left), 0, round(image.width * right), image.height))
        # Ignore nearly invisible alpha residue (< 3.2%); retain a two-pixel antialias margin.
        bounds = frame.getchannel('A').point(lambda a: 255 if a >= 8 else 0).getbbox()
        if bounds is None:
            raise ValueError(f'Empty weapon: {key}/{pose}')
        x0, y0, x1, y1 = bounds
        x0, y0 = max(0, x0 - 2), max(0, y0 - 2)
        x1, y1 = min(frame.width, x1 + 2), min(frame.height, y1 + 2)
        values = (x0 / frame.width, (frame.height - y1) / frame.height,
                  (x1 - x0) / frame.width, (y1 - y0) / frame.height)
        entries.append('            { "' + key + '/' + str(pose) + '", new PreviewRect(' +
                       ', '.join(f'{v:.9f}' for v in values) + ') },')

target = ui / 'WeaponPreviewBounds.cs'
source = target.read_text()
start, end = '// BEGIN MEASURED BOUNDS', '// END MEASURED BOUNDS'
before, rest = source.split(start)
_, after = rest.split(end)
result = before + start + '\n' + '\n'.join(entries) + '\n            ' + end + after
if '--check' in sys.argv:
    if result != source:
        raise SystemExit('Weapon preview bounds are stale; run this script without --check.')
else:
    target.write_text(result)
print(f'{len(entries)} weapon preview frames verified')
