"""Production int32 move/state I/O and batch-job serialization."""
from array import array
import json

def read_ints(path):
    data=array('i');data.frombytes(path.read_bytes());return list(data)
def write_ints(path,values):path.write_bytes(array('i',values).tobytes())
def job(folder,name,data,plan):
    source=folder/(name+'.input.int32.bin');cycles=folder/(name+'.cycles.tsv')
    write_ints(source,data['colors']);cycles.write_text('\n'.join('\t'.join(map(str,t)) for t in plan['cycles']))
    (folder/(name+'.plan.json')).write_text(json.dumps(plan,separators=(',',':')))
    return f'{name}\t{source}\t{cycles}'
