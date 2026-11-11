"""Read Unity containers as data, inventory objects and preserve TextAssets."""
import sys,json,hashlib,re,gc
from pathlib import Path
from collections import Counter
ROOT=Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT/'Tools/PythonLibs'))
import UnityPy

out=ROOT/'Unity'
out.mkdir(exist_ok=True)
rows=[]; bundles=[]; failures=[]
candidates=[]
for p in (ROOT/'Raw').rglob('*'):
    if not p.is_file(): continue
    with p.open('rb') as f: magic=f.read(8)
    if magic.startswith(b'UnityFS'): candidates.append(p)

for index,path in enumerate(candidates):
    relative=path.relative_to(ROOT/'Raw').as_posix()
    try:
        env=UnityPy.load(str(path))
        counts=Counter()
        bundle_rows=[]
        container_names={}
        for name,ptr in env.container.items():
            try: container_names.setdefault((ptr.assets_file.name,ptr.path_id),[]).append(name)
            except Exception: pass
        for obj in env.objects:
            counts[obj.type.name]+=1
            try: name=obj.peek_name() or ''
            except Exception: name=''
            row=dict(bundle=relative,file=obj.assets_file.name,path_id=obj.path_id,type=obj.type.name,name=name,size=obj.byte_size,containers=container_names.get((obj.assets_file.name,obj.path_id),[]))
            if obj.type.name=='TextAsset':
                try:
                    data=obj.read()
                    script=data.m_Script
                    raw=script.encode('utf-8','surrogateescape') if isinstance(script,str) else bytes(script)
                    safe=re.sub(r'[^\w.\- ]','_',name)[:95] or 'unnamed'
                    folder=out/'TextAssets'/str(index)
                    folder.mkdir(parents=True,exist_ok=True)
                    dest=folder/(str(obj.path_id)+'_'+safe)
                    dest.write_bytes(raw)
                    row.update(export=dest.relative_to(ROOT).as_posix(),content_size=len(raw),sha256=hashlib.sha256(raw).hexdigest(),prefix_hex=raw[:24].hex())
                except Exception as e: row['export_error']=str(e)
            bundle_rows.append(row)
        rows.extend(bundle_rows)
        bundles.append(dict(path=relative,bytes=path.stat().st_size,objects=len(bundle_rows),counts=dict(counts),containers=len(env.container)))
        print(json.dumps(bundles[-1]),flush=True)
        del env
        gc.collect()
    except Exception as e:
        failures.append(dict(path=relative,error=str(e)))
        print('ERROR',relative,str(e),flush=True)
    # Checkpoint after every container; interrupted runs still leave usable inventory.
    (out/'asset-inventory.json').write_text(json.dumps(dict(unitypy=UnityPy.__version__,bundles=bundles,objects=rows,failures=failures),ensure_ascii=False,indent=1),encoding='utf-8')
summary=dict(bundles=len(bundles),objects=len(rows),types=dict(Counter(r['type'] for r in rows)),failures=failures)
(out/'summary.json').write_text(json.dumps(summary,indent=2),encoding='utf-8')
print(json.dumps(summary,indent=2))
