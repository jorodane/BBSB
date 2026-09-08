"""Rebuild the bundled Korean UI subset. Requires fonttools; input is the upstream static OTF.

python Tools/subset_font.py /path/to/NotoSansCJKkr-Regular.otf
The upstream font is not downloaded automatically. See the bundled font notice for its source/license.
"""
import sys
from pathlib import Path
from fontTools import subset
from fontTools.ttLib import TTFont

root = Path(__file__).resolve().parents[1]
source = Path(sys.argv[1])
font = TTFont(source)
text = ''.join(p.read_text(encoding='utf-8') for p in (root / 'Assets/BBSB').rglob('*.cs'))
characters = set(range(0x20, 0x7f)) | set(map(ord, text)) | {0x00d7, 0x00b7, 0x2192}
options = subset.Options()
options.name_IDs = ['*']
options.name_legacy = True
options.name_languages = ['*']
worker = subset.Subsetter(options=options)
worker.populate(unicodes=characters)
worker.subset(font)
# Give the modified subset its own family name; retain upstream copyright/license names.
for record in font['name'].names:
    replacements = {1: 'BBSB UI', 2: 'Regular', 3: 'BBSB UI Regular 1.0',
                    4: 'BBSB UI Regular', 6: 'BBSBUI-Regular', 16: 'BBSB UI', 17: 'Regular'}
    if record.nameID in replacements:
        record.string = replacements[record.nameID].encode(record.getEncoding())
if 'CFF ' in font:
    cff = font['CFF '].cff
    cff.fontNames = ['BBSBUI-Regular']
    cff.topDictIndex[0].FamilyName = 'BBSB UI'
    cff.topDictIndex[0].FullName = 'BBSB UI Regular'
output = root / 'Assets/BBSB/Resources/BBSB/Fonts/BBSBUI.otf'
output.parent.mkdir(parents=True, exist_ok=True)
font.save(output)
print(f'Saved {output.name}: {output.stat().st_size:,} bytes, {len(font.getBestCmap())} characters')
