"""v1.55 log serialization, verified against supplied application files."""
from pathlib import Path
from .log_reader import read_log

MASK64 = (1 << 64)-1
def pack(move):
    axis, twist, angle, mask = move
    return axis | twist << 16 | angle << 32 | mask << 48

def history_codes(text):
    result=[]
    for token in text.replace('.', ' ').split():
        if token=='m|': continue
        result.append(-2 if token=='m[' else -1 if token=='m]' else pack(tuple(map(int,token.split(':')))))
    return result

def checksum(colors, codes, shuffle, pointer, timer):
    value=0
    for number in [len(colors),len(codes),shuffle,pointer,timer,*codes,*colors]:
        value=(value*0x12345675+number)&MASK64
    return int(f'{value:064b}'[::-1],2)

def verify_log(path):
    log=read_log(Path(path))
    head=log['header'].split()
    length,shuffle,pointer=map(int,head[3:])
    codes=history_codes(log['history'])
    metadata=dict(line.split(maxsplit=1) for line in log['metadata'])
    assert len(codes)==length, path
    actual=checksum(log['colors'],codes,shuffle,pointer,int(metadata['#timer']))
    assert actual==int(metadata['#CRC']), (str(path),actual,metadata['#CRC'])
    return log

def write_log(path, description, colors, moves):
    alphabet='0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ'
    data=''.join(alphabet[c%36]+alphabet[c//36] for c in colors)
    crc=checksum(colors,[pack(m) for m in moves],0,len(moves),0)
    lines=[f'MPUltimate v1.5 full-600-cell {len(moves)} 0 {len(moves)}',*description,
           f'2 {len(colors)}',*[data[i:i+256] for i in range(0,len(data),256)],
           '#timer 0',f'#CRC {crc}','*']
    if moves: lines+=['m| '+' '.join(':'.join(map(str,m)) for m in moves)+' .']
    Path(path).write_bytes(('\r\n'.join(lines)+'\r\n').encode('utf-8'))
    verify_log(path)

