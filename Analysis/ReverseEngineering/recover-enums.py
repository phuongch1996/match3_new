"""Read enum default constants from the original metadata byte ranges."""
import json,struct
from pathlib import Path
ROOT=Path(__file__).resolve().parent
api=json.loads((ROOT/'Metadata/game-api.json').read_text())
bundle=json.loads((ROOT/'metadata-evidence.json').read_text(encoding='utf-8-sig'))
n=next(r['normalized_result'] for r in bundle['records'] if r['operation']=='inspect_il2cpp_metadata')
sections={s['name']:s for s in n['sections']}
metadata=(ROOT/'Raw/base/assets/bin/Data/Managed/Metadata/global-metadata.dat').read_bytes()
section=sections['fieldDefaultValues']; data=sections['fieldAndParameterDefaultValueData']
defaults={}
for p in range(section['offset'],section['offset']+section['size'],12):
    index,type_index,offset=struct.unpack_from('<iii',metadata,p)
    if offset>=0: defaults[index]=(type_index,offset)
fmts={'int':'<i','uint':'<I','byte':'<B','sbyte':'<b','short':'<h','ushort':'<H','long':'<q','ulong':'<Q'}
result=[]
def compressed_uint(pos):
    first=metadata[pos]
    if first<0x80: return first
    if first<0xC0: return ((first&0x3f)<<8)|metadata[pos+1]
    if first<0xE0: return ((first&0x1f)<<24)|(metadata[pos+1]<<16)|(metadata[pos+2]<<8)|metadata[pos+3]
    if first==0xF0: return struct.unpack_from('<I',metadata,pos+1)[0]
    if first==0xFE: return 0xFFFFFFFE
    if first==0xFF: return 0xFFFFFFFF
    raise ValueError(f'Invalid compressed uint prefix {first:#x}')
for t in api['types']:
    underlying=next((f['type_name'] for f in t['fields'] if f['name']=='value__'),None)
    if underlying not in fmts: continue
    values=[]
    for f in t['fields']:
        default=defaults.get(f['index'])
        if f['name']=='value__' or default is None: continue
        type_index,offset=default
        pos=data['offset']+offset
        if underlying in ('int','uint') and n['metadata_version']>=29:
            unsigned=compressed_uint(pos)
            value=(unsigned>>1)^-(unsigned&1) if underlying=='int' else unsigned
        else: value=struct.unpack_from(fmts[underlying],metadata,pos)[0]
        values.append(dict(name=f['name'],value=value,field_index=f['index'],metadata_file_offset=data['offset']+offset))
    if values: result.append(dict(type=t['full_name'],type_index=t['index'],underlying=underlying,values=values))
(ROOT/'Metadata/enums.json').write_text(json.dumps(result,indent=1),encoding='utf-8')
for t in result:
    if t['type'].split('.')[-1] in ['ItemType','MatchType','GoalType','CellFillType','PatternType']:
        print(t['type'],[(v['name'],v['value']) for v in t['values']])
print('Recovered',len(result),'enums')
