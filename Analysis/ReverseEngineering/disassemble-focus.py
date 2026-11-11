"""Capstone disassembly of exact metadata-linked ARM64 entries; no decompiler claims."""
import sys,json,hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT/'Tools/PythonLibs'))
from elftools.elf.elffile import ELFFile
from capstone import Cs,CS_ARCH_ARM64,CS_MODE_LITTLE_ENDIAN
binary=ROOT/'Raw/arm64/lib/arm64-v8a/libil2cpp.so'
api=json.loads((ROOT/'Metadata/game-api.json').read_text())
types=api['types']
allmethods=[(int(m['address'],16),t,m) for t in types for m in t['methods'] if m['address']]
alladdresses=sorted(set(a for a,_,_ in allmethods))
import bisect
lookup={}
for address,t,m in allmethods: lookup.setdefault(address,[]).append(t['full_name']+'::'+m['name'])
out=ROOT/'Native'
out.mkdir(exist_ok=True)
elf_stream=binary.open('rb'); elf=ELFFile(elf_stream)
segments=[s for s in elf.iter_segments() if s['p_type']=='PT_LOAD']
def read(address,size):
    for s in segments:
        if s['p_vaddr']<=address< s['p_vaddr']+s['p_filesz']:
            off=s['p_offset']+address-s['p_vaddr']; elf_stream.seek(off)
            return elf_stream.read(min(size,s['p_filesz']-(address-s['p_vaddr'])))
    raise ValueError(hex(address))
decoder=Cs(CS_ARCH_ARM64,CS_MODE_LITTLE_ENDIAN)
results=[]
for address,t,m in allmethods:
    if not (t['namespace']=='Royal.Scenes.Game.Utils.FlatLevel' or 'Context.Units.Combo.' in t['full_name'] or t['name'] in ('MatchManager','LevelParser','ItemModelCreator','ItemSettings','TiledToTypeConverter','ItemExtensions','BoxItemModel','BoxItemView','ItemModel','PredefinedItemContainer','CellNeighbors') or 'SpecialItems.SpinnerItem.Strategy' in t['full_name']): continue
    nxt=bisect.bisect_right(alladdresses,address)
    # Next known method is a bounded heuristic; recorded as such, not a proven body extent.
    bound=min(alladdresses[nxt]-address if nxt<len(alladdresses) else 2048,65536)
    raw=read(address,bound)
    instructions=[]
    for ins in decoder.disasm(raw,address):
        row=dict(address=hex(ins.address),bytes=ins.bytes.hex(),mnemonic=ins.mnemonic,operands=ins.op_str)
        if ins.mnemonic in ('bl','b') and ins.op_str.startswith('#0x'):
            dest=int(ins.op_str[1:],16); row['target_names']=lookup.get(dest,[])
        instructions.append(row)
    results.append(dict(type_index=t['index'],type=t['full_name'],method=m['name'],method_index=m['index'],address=hex(address),extent_basis='up to next known metadata method, capped at 64KiB; not proven complete body',bytes_sha256=hashlib.sha256(raw).hexdigest(),instructions=instructions))
(out/'focused-disassembly.json').write_text(json.dumps(dict(binary_sha256=api['native_sha256'],decoder='Capstone',methods=results),indent=1),encoding='utf-8')
text=[]
for r in results:
    text.append(f"\n{r['type']}::{r['method']}  [{r['method_index']}] {r['address']}")
    for i in r['instructions']:
        text.append(f"{i['address']:>12}  {i['mnemonic']:8} {i['operands']}"+('  ; '+' | '.join(i['target_names']) if i.get('target_names') else ''))
(out/'focused-disassembly.txt').write_text('\n'.join(text),encoding='utf-8')
elf_stream.close()
print('DISASSEMBLED',len(results),'methods')
for r in results:
    if r['type'].endswith('FTiledLevel') and r['method'] in ('AddMove','AddName','get_Move'):
        print(r['type'],r['method'],r['instructions'])
