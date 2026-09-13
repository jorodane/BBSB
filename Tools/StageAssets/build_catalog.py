"""Build the small C# catalogue; audio and image binaries stay in the separate asset ZIP."""
import json
from pathlib import Path
HERE = Path(__file__).resolve().parent

def hook_bars(bars):
    return [max(4, int(bars * .22) // 4 * 4), int(bars * .52) // 4 * 4, bars - (6 if bars == 24 else 8)]

def sections(bars):
    hooks = hook_bars(bars)
    points = sorted(set([0, 2, bars - 2, bars] + hooks + [h + 4 for h in hooks]))
    return [dict(name=('HOOK ' + str(hooks.index(a)+1)) if a in hooks else 'INTRO' if a==0 else 'OUTRO' if a==bars-2 else 'GROOVE', start_bar=a, bars=b-a, is_hook=a in hooks, allows_response=a!=0 and a!=bars-2) for a,b in zip(points,points[1:])]

def main():
    data=json.loads((HERE/'stage_concepts.json').read_text())
    q=lambda s:json.dumps(s,ensure_ascii=False)
    rows=[]; maps=[]
    for m in data['maps']:
        maps.append('            new GenreMapDefinition(%s, %s, %s),' % (q(m['id']),q(m['genre']),q(m['name'])))
        for s in m['stages']:
            beats=2 if s['meter']=='6/8' else int(s['meter'][0])
            rows.append('            Define(%s, %s, %s, %d, %d, %d, %s, %s),' % (q(s['id']),q(m['id']),q(s['name']),s['bpm'],beats,s['bars'],q(s['meter']),q(s['field'])))
    template=(HERE/'StageCatalog.template').read_text()
    (HERE.parent.parent/'Assets/BBSB/Core/StageCatalog.cs').write_text(template.replace('@@MAPS@@','\n'.join(maps)).replace('@@STAGES@@','\n'.join(rows)))
if __name__=='__main__': main()
