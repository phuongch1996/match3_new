"""Derive searchable declarations from REA evidence. No target code executes."""
import json, re
from pathlib import Path
from collections import defaultdict

ROOT=Path(__file__).resolve().parent
bundle=json.loads((ROOT/'metadata-native-evidence.json').read_text(encoding='utf-8-sig'))
record=next(r for r in bundle['records'] if r['operation']=='inspect_il2cpp_metadata' and r['normalized_result'].get('binary'))
n=record['normalized_result']
types=n['types']
def typename(i):
    t=types[i]
    if t['declaring_type']>=0: return typename(t['declaring_type'])+'+'+t['name']
    return (t['namespace']+'.' if t['namespace'] else '')+t['name']
fields=defaultdict(list)
for i,f in enumerate(n['fields']): fields[f['declaring_type']].append(dict(index=i,**f,type_name=n['field_types'][i]['name']))
params=defaultdict(list)
for i,p in enumerate(n['parameters']): params[p['method']].append(dict(index=i,**p,type_name=n['parameter_types'][i]['name']))
methods=defaultdict(list)
for i,m in enumerate(n['methods']):
    methods[m['declaring_type']].append(dict(index=i,**m,**n['method_addresses'][i],return_type=n['return_types'][i]['name'],parameters=params[i]))
out=ROOT/'Metadata'
out.mkdir(exist_ok=True)
api=[]
for i,t in enumerate(types[:7201]):
    api.append(dict(index=i,full_name=typename(i),**t,fields=fields[i],methods=methods[i]))
(out/'game-api.json').write_text(json.dumps(dict(evidence_id=record['evidence_id'],metadata_sha256=n['artifact']['sha256'],native_sha256=n['binary']['artifact']['sha256'],limitations=record['limitations'],types=api),ensure_ascii=False,indent=1),encoding='utf-8')
focus=[t for t in api if any(k in t['full_name'] for k in ['Mechanics.Matches.','Context.Units.Combo.','SpecialItems.SpinnerItem.','SpecialItems.ElectroBallItem.','SpecialItems.DynamiteItem.','SpecialItems.RocketItem.','LevelParser','LevelData','LevelConfig','ItemType','BoardParser'])]
(out/'gameplay-focus.json').write_text(json.dumps(focus,ensure_ascii=False,indent=1),encoding='utf-8')
lines=['DECLARATIONS RECOVERED FROM IL2CPP — NOT ORIGINAL METHOD BODIES',f"Evidence: {record['evidence_id']}",f"Metadata: {n['artifact']['sha256']}",f"Binary: {n['binary']['artifact']['sha256']}",'']
for t in focus:
    lines.append(f"TYPE {t['index']}: {t['full_name']}")
    for f in t['fields']: lines.append(f"  FIELD[{f['index']}] {f['type_name']} {f['name']}")
    for m in t['methods']:
        args=', '.join(f"{p['type_name']} {p['name']}" for p in m['parameters'])
        lines.append(f"  METHOD[{m['index']}] {m['address']} {m['return_type']} {m['name']}({args})")
    lines.append('')
(out/'gameplay-focus.txt').write_text('\n'.join(lines),encoding='utf-8')
strings=[]
pattern=re.compile(r'(level|spinner|rocket|dynamite|electroball|match|\.json|\.bytes|\.dat|flatbuffer)',re.I)
for i,s in enumerate(n['string_literals']):
    text=s.get('value')
    if text and pattern.search(text): strings.append(dict(index=i,value=text))
(out/'gameplay-strings.json').write_text(json.dumps(strings,ensure_ascii=False,indent=1),encoding='utf-8')
print(json.dumps(dict(game_types=len(api),focused_types=len(focus),focused_methods=sum(len(t['methods']) for t in focus),filtered_strings=len(strings),evidence=record['evidence_id']),indent=2))
for t in focus:
    if 'Context.Units.Combo.' in t['full_name'] or 'Mechanics.Matches.' in t['full_name']:
        print(t['index'],t['full_name'],[(m['name'],m['address']) for m in t['methods']])
