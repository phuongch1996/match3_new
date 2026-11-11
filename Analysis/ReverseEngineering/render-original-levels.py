"""Preview original grid using native-proven x + width*y indexing; not a playable conversion."""
import json,sys
from pathlib import Path
from collections import Counter
ROOT=Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT/'Tools/PythonLibs'))
from PIL import Image,ImageDraw,ImageFont
enums=json.loads((ROOT/'Metadata/enums.json').read_text())
tile_enum=next(e for e in enums if e['type'].endswith('.TiledId'))
tiles={v['value']:v['name'] for v in tile_enum['values']}
goals={v['value']:v['name'] for v in next(e for e in enums if e['type'].endswith('.GoalType'))['values']}
art=json.loads((ROOT/'Unity/GameplayArt/manifest.json').read_text())
art_paths={r['name']:ROOT/r['export'] for r in art if 'export' in r}
sprites={}
for color in ['blue','green','red','yellow','pink']:
    sprites[color.capitalize()]=Image.open(art_paths['Items-MatchItem-'+color]).convert('RGBA')
box=Image.open(art_paths['Items-BoxItem-box']).convert('RGBA')
for i in range(1,4): sprites['Box'+str(i)]=box
for tile,name in [('Spinner','SpecialItems-spinner'),('VerticalRocket','Items-Rocket-vertical_rocket'),('HorizontalRocket','Items-Rocket-horizontal_rocket'),('Dynamite','Items-Dynamite-dynamite'),('ElectroBall','Items-ElectroBall-electroball')]: sprites[tile]=Image.open(art_paths[name]).convert('RGBA')
index=json.loads((ROOT/'Levels/index.json').read_text())
out=ROOT/'Levels/Previews'
out.mkdir(exist_ok=True)
try: font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',18); small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',12)
except OSError: font=small=ImageFont.load_default()
summary=[]; total=Counter(); unknown=Counter()
for row in index['levels']:
    data=json.loads((ROOT/row['export']).read_text())['level']; grid=data['Grid']; w,h=grid['Width'],grid['Height']
    counts=Counter(tiles.get(t,'UNKNOWN:'+str(t)) for t in grid['Items']); total.update(counts)
    unknown.update({k:v for k,v in counts.items() if k.startswith('UNKNOWN:')})
    goal_list=[dict(board=b['Id'],goal=goals.get(g['Goal'],'UNKNOWN:'+str(g['Goal'])),count=g['Count']) for b in grid['Boards'] for g in b['Goals']]
    summary.append(dict(name=row['name'],width=w,height=h,moves=data['Move'],tiles=dict(counts),goals=goal_list))
    if row['name'] not in ['1','2','3','4','5','10','13','100','1000','2300']: continue
    s=57; image=Image.new('RGB',(max(620,w*s+60),h*s+175),(19,29,47)); draw=ImageDraw.Draw(image)
    draw.text((22,15),f"Original level {row['name']} / {data['Name']}",font=font,fill='white')
    draw.text((22,44),f"{w} x {h} | {data['Move']} moves | "+', '.join(g['goal']+': '+str(g['count']) for g in goal_list),font=small,fill='#f5cc6a')
    for y in range(h):
        for x in range(w):
            i=x+w*y; name=tiles.get(grid['Items'][i],'?'+str(grid['Items'][i])); px=25+x*s; py=82+(h-1-y)*s
            draw.rounded_rectangle((px,py,px+s-3,py+s-3),radius=7,fill='#263c58')
            if name in sprites:
                im=sprites[name].copy(); im.thumbnail((s-7,s-7)); image.paste(im,(px+(s-3-im.width)//2,py+(s-3-im.height)//2),im)
                if name in ('Box2','Box3'): draw.text((px+3,py+2),name[-1],font=small,fill='white')
            elif name not in ('None','Empty'):
                draw.text((px+4,py+17),name[:8],font=small,fill='white')
    draw.text((22,h*s+100),'Raw original tiles; empty/set/random tiles are not simulated.',font=small,fill='#b6c8da')
    draw.text((22,h*s+121),'Index = x + width*y: LevelParser.GetCell @ 0x20cf144.',font=small,fill='#b6c8da')
    draw.text((22,h*s+142),'Y shown bottom-up; board origins and refill Y support this view.',font=small,fill='#b6c8da')
    image.save(out/(row['name']+'.png'))
result=dict(indexing=dict(formula='x + width*y',native_instructions=['0x20cf144 madd w19, w0, w19, w21','0x20cf0c4 madd w0, w3, w2, w1'],y_display='bottom-up, consistent with bottom-left board origins and above-board predefined refill coordinates'),tile_enum=tile_enum['type'],tile_enum_count=len(tiles),tile_counts=dict(total),unknown_tile_values=dict(unknown),levels=summary)
(ROOT/'Levels/tile-goal-summary.json').write_text(json.dumps(result,indent=1),encoding='utf-8')
print('Mapped',len(summary),'level grids; tile enum entries',len(tiles),'unknown tile IDs',dict(unknown))
