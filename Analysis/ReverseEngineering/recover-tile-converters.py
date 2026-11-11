"""Derive item/layer/color/fill mappings by interpreting bounded pure ARM64 lookups.
Only listed arithmetic/branch/read-only instructions are supported. No calls, writes,
OS effects or original app execution are allowed; unsupported instructions fail.
"""
import sys,json,struct,hashlib,re
from pathlib import Path
ROOT=Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT/'Tools/PythonLibs'))
from elftools.elf.elffile import ELFFile
binary=ROOT/'Raw/arm64/lib/arm64-v8a/libil2cpp.so'
f=binary.open('rb'); elf=ELFFile(f)
segments=[s for s in elf.iter_segments() if s['p_type']=='PT_LOAD']
def read(addr,size):
    for s in segments:
        if s['p_vaddr']<=addr and addr+size<=s['p_vaddr']+s['p_filesz']:
            f.seek(s['p_offset']+addr-s['p_vaddr']); return f.read(size)
    raise ValueError(f'Not file-backed: {addr:#x}')
methods=json.loads((ROOT/'Native/focused-disassembly.json').read_text())['methods']
chosen={m['method']:m for m in methods if m['type'].endswith('TiledToTypeConverter') and m['method'] in ('AsItemType','AsLayerCount','AsCellType','GetColorForTiledId')}
chosen['ToMatchType']=next(m for m in methods if m['address']=='0x228c2d0')
goal_item=next(m for m in methods if m['address']=='0x228c36c')
goal_match=next(m for m in methods if m['address']=='0x228c3b0')
def signed(value): return value-0x100000000 if value&0x80000000 else value
def run(method,value):
    instructions={int(i['address'],16):i for i in method['instructions']}
    pc=int(method['address'],16); regs={0:value&0xffffffff}; flags=dict(N=False,Z=False,C=False,V=False); trace=[]; memory=[]
    def reg(name):
        if name in ('wzr','xzr'): return 0
        result=regs.get(int(name[1:]),0)
        return result&0xffffffff if name.startswith('w') else result&0xffffffffffffffff
    def val(token): return int(token[1:],0) if token.startswith('#') else reg(token)
    def put(name,value): regs[int(name[1:])]=value& (0xffffffff if name.startswith('w') else 0xffffffffffffffff)
    def condition(cc):
        n,z,c,v=flags['N'],flags['Z'],flags['C'],flags['V']
        return {'eq':z,'ne':not z,'hs':c,'lo':not c,'hi':c and not z,'ls':not c or z,'le':z or n!=v,'lt':n!=v,'ge':n==v,'gt':not z and n==v}[cc]
    for step in range(150):
        i=instructions[pc]; trace.append(hex(pc)); op=i['mnemonic']; args=i['operands']; parts=args.split(', '); next_pc=pc+4
        if op=='ret': return dict(value=signed(reg('w0')),trace=trace,memory=memory)
        if op in ('mov','adrp'): put(parts[0],val(parts[1]))
        elif op in ('sub','add','lsl'):
            a,b=val(parts[1]),val(parts[2]); width=32 if parts[0].startswith('w') else 64
            put(parts[0],a-b if op=='sub' else a+b if op=='add' else a<<(b&(width-1)))
        elif op=='cmp':
            a,b=val(parts[0])&0xffffffff,val(parts[1])&0xffffffff; result=(a-b)&0xffffffff
            flags=dict(N=bool(result&0x80000000),Z=result==0,C=a>=b,V=bool((a^b)&(a^result)&0x80000000))
        elif op=='tst':
            result=(val(parts[0])&val(parts[1]))&0xffffffff
            flags=dict(N=bool(result&0x80000000),Z=result==0,C=False,V=False)
        elif op=='b': next_pc=val(parts[0])
        elif op.startswith('b.'):
            if condition(op[2:]): next_pc=val(parts[0])
        elif op=='cset': put(parts[0],int(condition(parts[1])))
        elif op in ('csinc','csel'):
            put(parts[0],val(parts[1]) if condition(parts[3]) else val(parts[2])+(1 if op=='csinc' else 0))
        elif op=='ldr':
            match=re.fullmatch(r'(w\d+), \[(x\d+), (w\d+), sxtw #(\d+)\]',args)
            if not match: raise ValueError('Unsupported memory operand: '+args)
            dest,base,index,shift=match.groups(); addr=reg(base)+(signed(reg(index))<<int(shift)); raw=read(addr,4)
            put(dest,struct.unpack('<I',raw)[0]); memory.append(dict(address=hex(addr),bytes=raw.hex()))
        else: raise ValueError('Unsupported instruction: '+op+' '+args)
        pc=next_pc
    raise ValueError('Bounded interpreter exceeded step allowance')
enums=json.loads((ROOT/'Metadata/enums.json').read_text())
enum={e['type'].split('.')[-1]:{v['value']:v['name'] for v in e['values']} for e in enums}
tiled=next(e for e in enums if e['type'].endswith('.TiledId'))
rows=[]
for tile in tiled['values']:
    results={name:run(method,tile['value']) for name,method in chosen.items()}
    match_color=results['ToMatchType']['value']; item_type=results['AsItemType']['value']
    results['GoalType']=run(goal_match,match_color) if match_color else run(goal_item,item_type)
    goal_type=results['GoalType']['value']
    rows.append(dict(tile_id=tile['value'],tile_name=tile['name'],item_type=item_type,item_name=enum['ItemType'].get(item_type),layer_count=results['AsLayerCount']['value'],cell_type=results['AsCellType']['value'],match_color=match_color,special_color=results['GetColorForTiledId']['value'],goal_type=goal_type,goal_name=enum['GoalType'].get(goal_type),lookups=results))
byid={r['tile_id']:r for r in rows}
assert byid[21]['item_name']=='Box' and byid[21]['layer_count']==1
assert byid[22]['item_name']=='Box' and byid[22]['layer_count']==2
assert byid[23]['item_name']=='Box' and byid[23]['layer_count']==3
assert byid[10]['item_name']=='Spinner'
assert byid[20]['item_name']==byid[30]['item_name']=='Rocket'
assert byid[40]['item_name']=='Dynamite' and byid[50]['item_name']=='ElectroBall'
report=dict(binary_sha256=hashlib.sha256(binary.read_bytes()).hexdigest(),basis='bounded interpretation of pure ARM64 lookup methods and read-only data tables; no app execution',methods={name:dict(address=m['address'],method_index=m['method_index']) for name,m in dict(chosen,GoalFromItem=goal_item,GoalFromMatch=goal_match).items()},mapped_tiles=len(rows),mappings=rows,limitations=['Static and overlay item-type conversion methods still need separate handling; item_type=None is not proof that a tile is absent.','Most prototype blockers and level parsing are not yet implemented.'])
(ROOT/'Levels/tile-converters.json').write_text(json.dumps(report,indent=1),encoding='utf-8')
print('Mapped',len(rows),'tile IDs through original item/layer/fill/color lookups')
print([{k:r[k] for k in ('tile_id','tile_name','item_name','layer_count','cell_type','match_color','special_color')} for r in rows[:28]])
f.close()
