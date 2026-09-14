"""Read-only validation of the separately installed gesture icon PNG pack."""
from pathlib import Path
import sys
from PIL import Image

root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parents[1]
base = root / 'Assets/BBSB/Resources/BBSB/GestureIcons'
count = 0
for size in ['Small', 'Large']:
    for kind in ['tap', 'hold', 'flick', 'dive', 'shake']:
        path = base / size / (kind + '.png')
        with Image.open(path) as image:
            assert image.format == 'PNG' and image.mode == 'RGBA', f'{path}: RGBA PNG required'
            assert image.width == image.height and image.width >= 128, f'{path}: square source required'
            alpha = image.getchannel('A'); histogram = alpha.histogram()
            assert histogram[0] > image.width * image.height * .10, f'{path}: meaningful transparent exterior required'
            assert sum(histogram[240:]) > image.width * image.height * .35, f'{path}: opaque badge missing'
            for x,y in [(0,0),(image.width-1,0),(0,image.height-1),(image.width-1,image.height-1)]:
                assert alpha.getpixel((x,y)) == 0, f'{path}: opaque canvas corner'
            count += 1
print(f'{count} square gesture icons have genuine transparent alpha and visible artwork')
