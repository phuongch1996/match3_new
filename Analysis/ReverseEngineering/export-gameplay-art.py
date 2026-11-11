import sys,json,re,hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT/'Tools/PythonLibs'))
import UnityPy
from PIL import Image,ImageDraw,ImageFont
out=ROOT/'Unity/GameplayArt'
out.mkdir(parents=True,exist_ok=True)
env=UnityPy.load(str(ROOT/'Raw/base/assets/bin/Data/data.unity3d'),str(ROOT/'Raw/data/assets/bin/Data/datapack.unity3d'),str(ROOT/'Raw/data/assets/bin/Data/resources.resource'))
prefixes=('Items-MatchItem-','Items-Rocket-','Items-Spinner-','Items-Dynamite-','Items-ElectroBall-','Items-BoxItem-')
rows=[]
for obj in env.objects:
    if obj.type.name!='Sprite': continue
    name=obj.peek_name() or ''
    if not (name.startswith(prefixes) or name in ('SpecialItems-spinner','spinner_250px','spinner','spinner_shadow_250px')): continue
    row=dict(name=name,path_id=obj.path_id,source_file=obj.assets_file.name,source_bundle='Raw/data/assets/bin/Data/datapack.unity3d')
    try:
        image=obj.read().image
        dest=out/(str(obj.path_id)+'_'+re.sub(r'[^a-zA-Z0-9._-]','_',name)+'.png')
        image.save(dest)
        row.update(export=dest.relative_to(ROOT).as_posix(),size=list(image.size),sha256=hashlib.sha256(dest.read_bytes()).hexdigest())
    except Exception as e: row['error']=str(e)
    rows.append(row)
    print(json.dumps(row),flush=True)
(out/'manifest.json').write_text(json.dumps(rows,indent=2),encoding='utf-8')
good=[r for r in rows if 'export' in r]
columns=6; tilew=200; tileh=166
sheet=Image.new('RGB',(columns*tilew,max(1,(len(good)+columns-1)//columns)*tileh),(23,32,50))
draw=ImageDraw.Draw(sheet)
for i,r in enumerate(good):
    x=(i%columns)*tilew; y=(i//columns)*tileh
    im=Image.open(ROOT/r['export']).convert('RGBA'); im.thumbnail((150,122))
    sheet.paste(im,(x+(tilew-im.width)//2,y+8+(122-im.height)//2),im)
    label=r['name'].replace('Items-','')
    for j in range(0,len(label),28): draw.text((x+7,y+132+(j//28)*12),label[j:j+28],fill='white')
sheet.save(out/'contact-sheet.jpg',quality=90)
print('EXPORTED',len(good),'ERRORS',len(rows)-len(good))
