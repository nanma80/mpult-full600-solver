"""MPUlt macro packing and exact read-back verification."""
from .paths import ANCHOR_FILE
from .log_format import pack
from .state import validate_name

def codes(parts):
    for flat in parts:
        for i in range(0,len(flat),4):yield pack(flat[i:i+4])

def write_macros(path,steps,joint_name='all_steps',puzzle_name='full-600-cell'):
    validate_name(puzzle_name)
    template=ANCHOR_FILE.read_text().splitlines();anchor=template[2].split();point=template[3]
    entries=[(name,[flat]) for name,flat in steps]+[(joint_name,[flat for _,flat in steps])]
    with path.open('w',encoding='utf-8',newline='') as w:
        w.write(f'MPUlt Macro File\r\n{puzzle_name} 4 {len(entries)}\r\n')
        for name,parts in entries:
            count=sum(len(p)//4 for p in parts)
            w.write(f'{name} {anchor[1]} {count} '+' '.join(anchor[3:])+'\r\n'+point+'\r\n')
            block=[]
            for code in codes(parts):
                block.append(str(code))
                if len(block)==16:w.write(' '.join(block)+' \r\n');block=[]
            if block:w.write(' '.join(block)+' \r\n')
    # Read every emitted code back, including the full concatenated entry.
    with path.open() as r:
        if r.readline().strip()!='MPUlt Macro File' or r.readline().strip()!=f'{puzzle_name} 4 {len(entries)}':raise ValueError('Macro header')
        for name,parts in entries:
            head=r.readline().split();count=sum(len(p)//4 for p in parts)
            if head[:3]!=[name,anchor[1],str(count)] or head[3:]!=anchor[3:]:raise ValueError('Macro metadata')
            if r.readline().strip()!=point.strip():raise ValueError('Macro point')
            expected=codes(parts);seen=0
            while seen<count:
                line=r.readline()
                if not line:raise ValueError('Truncated macro')
                for value in line.split():
                    if int(value)!=next(expected):raise ValueError('Macro code mismatch')
                    seen+=1
            if seen!=count or next(expected,None) is not None:raise ValueError('Macro length mismatch')
        if r.read().strip():raise ValueError('Unexpected macro tail')

