"""Recover FlatBuffer schema from metadata plus native Add* field-slot constants.
Missing optional scalars use observed builder defaults; bytes remain the authority.
"""
import sys,json,re,struct,hashlib
from pathlib import Path
from collections import Counter
ROOT=Path(__file__).resolve().parent
api=json.loads((ROOT/'Metadata/game-api.json').read_text())
disassembly=json.loads((ROOT/'Native/focused-disassembly.json').read_text())
by_method={m['method_index']:m for m in disassembly['methods']}
types={t['name']:t for t in api['types'] if t['namespace']=='Royal.Scenes.Game.Utils.FlatLevel'}
primitive={'int':'<i','uint':'<I','sbyte':'<b','byte':'<B','short':'<h','ushort':'<H','long':'<q','ulong':'<Q','bool':'<?','float':'<f','double':'<d'}
schema={}
issues=[]
def constant(instructions,register):
    for ins in instructions:
        if ins['mnemonic']=='mov' and ins['operands'].startswith(register+', '):
            value=ins['operands'].split(', ',1)[1]
            if value in ('wzr','xzr'): return 0
            if value.startswith('#'): return int(value[1:],0)
    return None
for name,t in types.items():
    fields=[]
    methods={m['name']:m for m in t['methods']}
    for m in t['methods']:
        if not m['name'].startswith('Add') or m['index'] not in by_method: continue
        prop=m['name'][3:]
        if len(m['parameters'])!=2: continue
        ins=by_method[m['index']]['instructions']
        slot=constant(ins,'w1')
        if slot is None:
            issues.append(dict(type=name,method=m['name'],reason='no constant field slot')); continue
        arg=m['parameters'][1]['type_name']
        field=dict(name=prop,slot=slot,add_method=m['index'],add_address=m['address'],default=constant(ins,'w3') or 0)
        if arg in primitive: field.update(kind='scalar',scalar=arg)
        elif arg=='Google.FlatBuffers.StringOffset': field['kind']='string'
        elif arg=='Google.FlatBuffers.VectorOffset':
            getter=methods.get(prop)
            if not getter:
                issues.append(dict(type=name,field=prop,reason='no vector accessor')); continue
            ret=getter['return_type']
            if ret in primitive: field.update(kind='vector_scalar',scalar=ret)
            else:
                match=re.fullmatch(r'System.Nullable<Royal\.Scenes\.Game\.Utils\.FlatLevel\.(\w+)>',ret or '')
                if not match: issues.append(dict(type=name,field=prop,reason='unknown vector type',type_name=ret)); continue
                field.update(kind='vector_table',target=match.group(1))
        else:
            match=re.fullmatch(r'Google.FlatBuffers.Offset<Royal\.Scenes\.Game\.Utils\.FlatLevel\.(\w+)>',arg or '')
            if not match: issues.append(dict(type=name,field=prop,reason='unknown offset type',type_name=arg)); continue
            field.update(kind='table',target=match.group(1))
        # Independent accessor check: Offset(vtableSlot) must match 4+2*builderSlot.
        getter=methods.get('get_'+prop) or methods.get(prop)
        if getter and getter['index'] in by_method:
            getter_slot=constant(by_method[getter['index']]['instructions'],'w1')
            if getter_slot is not None:
                field['getter_vtable_slot']=getter_slot
                if getter_slot!=4+2*slot: raise ValueError(f'Slot disagreement {name}.{prop}: {slot}/{getter_slot}')
        fields.append(field)
    if fields: schema[name]=sorted(fields,key=lambda f:f['slot'])

out=ROOT/'Levels'
out.mkdir(exist_ok=True)
(out/'recovered-schema.json').write_text(json.dumps(dict(evidence='native Add* slot constants and metadata signatures',native_sha256=api['native_sha256'],tables=schema,issues=issues),indent=1),encoding='utf-8')

class Buffer:
    def __init__(self,data): self.data=data
    def read(self,fmt,pos):
        if pos<0 or pos+struct.calcsize(fmt)>len(self.data): raise ValueError(f'Out-of-bounds {pos}/{len(self.data)}')
        return struct.unpack_from(fmt,self.data,pos)[0]
    def indirect(self,pos): return pos+self.read('<I',pos)
    def table(self,name,pos,depth=0):
        if depth>24: raise ValueError('Excessive recursion')
        if name not in schema: return dict(_unknown_table=name,_position=pos)
        vpos=pos-self.read('<i',pos); vlen=self.read('<H',vpos)
        if vlen<4 or vpos<0 or vpos+vlen>len(self.data): raise ValueError('Invalid vtable')
        result={}
        for f in schema[name]:
            entry=4+2*f['slot']; offset=self.read('<H',vpos+entry) if entry<vlen else 0
            p=pos+offset
            if not offset:
                result[f['name']]=bool(f['default']) if f.get('scalar')=='bool' else f['default'] if f['kind']=='scalar' else [] if f['kind'].startswith('vector') else None
                continue
            if f['kind']=='scalar': value=self.read(primitive[f['scalar']],p)
            elif f['kind']=='string':
                s=self.indirect(p); length=self.read('<I',s)
                if s+4+length>len(self.data): raise ValueError('String out of bounds')
                value=self.data[s+4:s+4+length].decode('utf-8')
            elif f['kind']=='table':
                target=f['target']; value=self.table(target,self.indirect(p),depth+1)
            else:
                start=self.indirect(p); count=self.read('<I',start); start+=4
                if count>100000: raise ValueError('Excessive vector length')
                if f['kind']=='vector_scalar':
                    fmt=primitive[f['scalar']]; size=struct.calcsize(fmt)
                    value=[self.read(fmt,start+i*size) for i in range(count)]
                else:
                    target=f['target']
                    value=[self.table(target,self.indirect(start+i*4),depth+1) for i in range(count)]
            result[f['name']]=value
        return result

inventory=json.loads((ROOT/'Unity/asset-inventory.json').read_text())
assets=[o for o in inventory['objects'] if o['type']=='TextAsset' and (o['name'].isdigit() or o['name'].startswith('playable_')) and 'export' in o]
records=[]; errors=[]; dims=Counter(); verified_cells=0
for a in assets:
    try:
        data=(ROOT/a['export']).read_bytes(); buf=Buffer(data)
        result=buf.table('FTiledLevel',buf.read('<I',0))
        grid=result['Grid']; width,height=grid['Width'],grid['Height']
        if not(1<=width<=100 and 1<=height<=100): raise ValueError(f'Implausible grid {width}x{height}')
        if len(grid['Items'])!=width*height or len(grid['Cells'])!=width*height: raise ValueError('Grid arrays do not match dimensions')
        if not(0<=result['Move']<=1000): raise ValueError('Implausible move count')
        verified_cells+=width*height; dims[f'{width}x{height}']+=1
        dest=out/(a['name']+'.json')
        envelope=dict(source_name=a['name'],source_sha256=a['sha256'],source_export=a['export'],schema='recovered-schema.json',level=result)
        dest.write_text(json.dumps(envelope,ensure_ascii=False,indent=1),encoding='utf-8')
        records.append(dict(name=a['name'],embedded_name=result['Name'],move=result['Move'],width=width,height=height,items=len(grid['Items']),boards=len(grid.get('Boards',[])),export=dest.relative_to(ROOT).as_posix(),sha256=a['sha256']))
    except Exception as e: errors.append(dict(name=a['name'],error=str(e),source=a['export']))
summary=dict(decoded=len(records),numeric_levels=sum(r['name'].isdigit() for r in records),playable_levels=sum(not r['name'].isdigit() for r in records),validated_cells=verified_cells,dimensions=dict(dims),schema_tables=len(schema),schema_issues=issues,errors=errors)
(out/'index.json').write_text(json.dumps(dict(summary=summary,levels=records),indent=1),encoding='utf-8')
print(json.dumps(summary,indent=2))
print('FIRST',[(r['name'],r['move'],r['width'],r['height']) for r in sorted(records,key=lambda x:int(x['name']) if x['name'].isdigit() else 99999)[:15]])
