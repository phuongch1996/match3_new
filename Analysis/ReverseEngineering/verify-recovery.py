"""Verify exported evidence/data boundaries without claiming behavioral equivalence."""
import json,hashlib,sys,wave
from pathlib import Path
ROOT=Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT/'Tools/PythonLibs'))
from PIL import Image
inventory=json.loads((ROOT/'Unity/asset-inventory.json').read_text())
levels=json.loads((ROOT/'Levels/index.json').read_text())
art=json.loads((ROOT/'Unity/GameplayArt/manifest.json').read_text())
enums=json.loads((ROOT/'Metadata/enums.json').read_text())
assertions=0
def check(ok,message):
    global assertions
    assertions+=1
    if not ok: raise AssertionError(message)
texts=[a for a in inventory['objects'] if a['type']=='TextAsset']
for a in texts:
    check('export' in a,a['name']+' missing export')
    data=(ROOT/a['export']).read_bytes()
    check(hashlib.sha256(data).hexdigest()==a['sha256'],a['name']+' content mismatch')
numeric={int(r['name']) for r in levels['levels'] if r['name'].isdigit()}
check(numeric==set(range(1,2301)),'expected contiguous levels 1..2300')
cells=0
for row in levels['levels']:
    envelope=json.loads((ROOT/row['export']).read_text())
    level=envelope['level']; grid=level['Grid']; w,h=grid['Width'],grid['Height']
    check(len(grid['Items'])==len(grid['Cells'])==w*h,row['name']+' dimensions inconsistent')
    cells+=w*h
    for board in grid['Boards']:
        p=board['BottomLeft']
        check(p is not None and isinstance(p['X'],int) and isinstance(p['Y'],int),row['name']+' invalid board origin')
        check(0<=p['X'] and p['X']+board['Width']<=w and 0<=p['Y'] and p['Y']+board['Height']<=h,row['name']+' board outside grid')
    check(envelope['source_sha256']==row['sha256'],row['name']+' source hash linkage mismatch')
check(not levels['summary']['errors'],'level decoder errors')
check(not levels['summary']['schema_issues'],'unresolved schema fields')
for a in art:
    check('export' in a,a['name']+' missing image')
    p=ROOT/a['export']; im=Image.open(p)
    check(list(im.size)==a['size'] and im.mode=='RGBA',a['name']+' image format mismatch')
    check(hashlib.sha256(p.read_bytes()).hexdigest()==a['sha256'],a['name']+' image hash mismatch')
byname={e['type'].split('.')[-1]:e for e in enums}
match={v['name']:v['value'] for v in byname['MatchType']['values']}
check(match=={'None':0,'Blue':1,'Green':2,'Red':3,'Yellow':4,'Pink':5},'match enum compressed constants invalid')
goal={v['name']:v['value'] for v in byname['GoalType']['values']}
check(goal['Box']==6 and goal['Ice']==7 and goal['AttackTower']==1000,'goal enum constants invalid')
tile_summary=json.loads((ROOT/'Levels/tile-goal-summary.json').read_text())
check(tile_summary['tile_enum_count']==184 and not tile_summary['unknown_tile_values'],'every original tile ID must have a recovered enum name')
converters=json.loads((ROOT/'Levels/tile-converters.json').read_text())
mapped={r['tile_id']:r for r in converters['mappings']}
check(len(mapped)==184,'converter must cover every declared tile ID')
for tile,layer in [(21,1),(22,2),(23,3)]:
    check(mapped[tile]['item_name']=='Box' and mapped[tile]['layer_count']==layer and mapped[tile]['goal_name']=='Box','box tile/type/layer/goal mapping mismatch')
for tile in range(1,6): check(mapped[tile]['match_color']==tile and mapped[tile]['goal_type']==tile,'match tile color and goal mismatch')
adapter_path=ROOT.parent.parent/'Assets/Resources/OriginalLevels/level_1.json'
if adapter_path.exists():
    adapter=json.loads(adapter_path.read_text())
    native=json.loads((ROOT/'Levels/1.json').read_text())
    level=native['level']; grid=level['Grid']
    check(adapter['sourceSha256']==native['source_sha256'],'runtime adapter source hash mismatch')
    check(adapter['tiles']==grid['Items'] and adapter['width']==grid['Width'] and adapter['height']==grid['Height'],'runtime adapter changed native geometry/tiles')
    check(adapter['moves']==level['Move'] and adapter['boxGoal']==sum(g['Count'] for b in grid['Boards'] for g in b['Goals'] if g['Goal']==6),'runtime adapter changed native moves/goals')
    check(adapter['predefined']==[dict(x=p['X'],y=p['Y'],ids=p['Ids']) for p in level['Predefined']],'runtime adapter changed predefined queue ordering/content')
    check(adapter['colors']==level['Colors'],'runtime adapter changed available source colors')
audio=json.loads((ROOT/'Unity/GameplayAudio/manifest.json').read_text())
for a in audio:
    check(bool(a['exports']) and 'error' not in a,a['name']+' audio export missing')
    for sample in a['exports']:
        p=ROOT/sample['path']
        check(hashlib.sha256(p.read_bytes()).hexdigest()==sample['sha256'],a['name']+' WAV hash mismatch')
        with wave.open(str(p)) as wav:
            check(wav.getnframes()>0 and wav.getframerate()==a['original_frequency'] and wav.getnchannels()==a['original_channels'],a['name']+' audio format mismatch')
            check(abs(wav.getnframes()/wav.getframerate()-a['original_length'])<.002,a['name']+' decoded duration mismatch')
report=dict(status='PASS',assertions=assertions,verified_text_assets=len(texts),decoded_levels=len(levels['levels']),numeric_levels=len(numeric),validated_cells=cells,verified_sprite_exports=len(art),verified_audio_exports=len(audio),known_tile_enum_entries=tile_summary['tile_enum_count'],original_level_adapter_present=adapter_path.exists(),limitations=['Data structure validation is not full behavioral equivalence.','Native function extents in Capstone exports are bounded by next known method, not a proven complete function body.','Original-level runtime adapter is partial: complete original RNG, diagonal falling and full mechanics are not recovered.','Most custom MonoBehaviour type trees are stripped; generic fields require further reconstruction.'])
(ROOT/'verification.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report,indent=2))
