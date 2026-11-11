"""Decode selected AudioClip data with UnityPy's bundled FMOD parser, never run app code."""
import sys,json,re,hashlib,io,wave
from pathlib import Path
ROOT=Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT/'Tools/PythonLibs'))
import UnityPy
env=UnityPy.load(str(ROOT/'Raw/base/assets/bin/Data/data.unity3d'),str(ROOT/'Raw/data/assets/bin/Data/datapack.unity3d'),str(ROOT/'Raw/data/assets/bin/Data/resources.resource'))
out=ROOT/'Unity/GameplayAudio'
out.mkdir(exist_ok=True)
rows=[]
for obj in env.objects:
    if obj.type.name!='AudioClip': continue
    name=obj.peek_name() or ''
    if not name.startswith(('match_','spinner_','rocket_','dynamite_','electro_','box_','swipe_')): continue
    row=dict(name=name,path_id=obj.path_id,source_file=obj.assets_file.name,source_bundle='Raw/data/assets/bin/Data/datapack.unity3d',exports=[])
    try:
        data=obj.read()
        row.update(original_channels=data.m_Channels,original_frequency=data.m_Frequency,original_length=data.m_Length)
        samples=data.samples
        for filename,content in samples.items():
            safe=re.sub(r'[^a-zA-Z0-9._-]','_',filename)
            dest=out/(str(obj.path_id)+'_'+safe)
            dest.write_bytes(content)
            entry=dict(path=dest.relative_to(ROOT).as_posix(),bytes=len(content),sha256=hashlib.sha256(content).hexdigest())
            if content.startswith(b'RIFF'):
                with wave.open(io.BytesIO(content)) as wav:
                    entry.update(channels=wav.getnchannels(),frequency=wav.getframerate(),frames=wav.getnframes(),seconds=wav.getnframes()/wav.getframerate())
            row['exports'].append(entry)
    except Exception as e: row['error']=str(e)
    rows.append(row)
    print(json.dumps(row),flush=True)
(out/'manifest.json').write_text(json.dumps(rows,indent=1),encoding='utf-8')
print('AUDIO EXPORTED',sum(bool(r['exports']) for r in rows),'ERRORS',sum('error' in r for r in rows))
