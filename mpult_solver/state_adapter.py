"""One-way log-to-solver-input adapter; only definition and colors cross the boundary."""
from pathlib import Path
from .log_reader import read_log
from .state import SCHEMA,save

def export(source,destination):
    log=read_log(Path(source))
    first=log['description'][0]
    if not first.startswith('Puzzle '):raise ValueError('Missing puzzle name')
    name=first[len('Puzzle '):]
    if log['header'].split()[2]!=name:raise ValueError('Puzzle names in log header and definition differ')
    result={'schema':SCHEMA,'puzzle':{'name':name,'definition':log['description'][1:-1]},'colors':log['colors']}
    save(destination,result)
    return result

