"""One-way log-to-solver-input adapter; only definition and colors cross the boundary."""
from pathlib import Path
import sys
from log_reader import read_log
from color_state import SCHEMA,save

def export(source,destination):
    log=read_log(Path(source))
    if log['description'][0]!='Puzzle full-600-cell':raise ValueError('Unsupported puzzle')
    result={'schema':SCHEMA,'puzzle':{'name':'full-600-cell','definition':log['description'][1:-1]},'colors':log['colors']}
    save(destination,result)
    return result

if __name__=='__main__':
    if len(sys.argv)!=3:raise SystemExit('Usage: export_color_state.py input.log output.color.json')
    export(*sys.argv[1:])
