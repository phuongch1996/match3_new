"""Prepare the first supported original level; refuse unsupported mechanics explicitly."""
import json,hashlib,shutil
from pathlib import Path
ROOT=Path(__file__).resolve().parent
PROJECT=ROOT.parent.parent
source=ROOT/'Levels/1.json'
envelope=json.loads(source.read_text())
level=envelope['level']; grid=level['Grid']
assert len(grid['Boards'])==1 and all(1<=t<=5 or 21<=t<=23 for t in grid['Items'])
assert all(c['Ice']==0 and not c['HasJelly'] and all(c[k]==0 for k in ['Ivy','Curtain','Chain','Sand','Border','Puzzle']) for c in grid['Cells'])
assert all(g['Goal']==6 for b in grid['Boards'] for g in b['Goals'])
assert all(1<=tile<=5 for entry in level['Predefined'] for tile in entry['Ids'])
# Only the top row of this single-board source is the predefined filling source.
assert all(grid['Cells'][x+(grid['Height']-1)*grid['Width']]['IsPredefined'] for x in range(grid['Width']))
adapter=dict(sourceName=envelope['source_name'],sourceSha256=envelope['source_sha256'],embeddedName=level['Name'],width=grid['Width'],height=grid['Height'],moves=level['Move'],boxGoal=sum(g['Count'] for b in grid['Boards'] for g in b['Goals']),boardCount=1,tiles=grid['Items'],colors=level['Colors'],predefined=[dict(x=p['X'],y=p['Y'],ids=p['Ids']) for p in level['Predefined']])
destination=PROJECT/'Assets/Resources/OriginalLevels'
destination.mkdir(parents=True,exist_ok=True)
(destination/'level_1.json').write_text(json.dumps(adapter,indent=1),encoding='utf-8')
art=json.loads((ROOT/'Unity/GameplayArt/manifest.json').read_text())
box=next(a for a in art if a['name']=='Items-BoxItem-box')
target=PROJECT/'Assets/Resources/RecoveredArt/box.png'
shutil.copyfile(ROOT/box['export'],target)
provenance_file=target.parent/'provenance.json'
provenance=json.loads(provenance_file.read_text(encoding='utf-8-sig'))
provenance=[p for p in provenance if p['resource']!='box']
provenance.append(dict(resource='box',sourceName=box['name'],pathId=box['path_id'],sha256=hashlib.sha256(target.read_bytes()).hexdigest(),sourceBundle=box['source_bundle'],dependencyBundle='Raw/base/assets/bin/Data/data.unity3d'))
provenance_file.write_text(json.dumps(provenance,indent=1),encoding='utf-8')
print('Prepared native original level 1 and box sprite with source provenance.')
