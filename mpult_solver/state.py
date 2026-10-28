"""Strict history-free solver-input boundary. No log parser is imported here."""
from pathlib import Path
import json

SCHEMA='mpult-color-state/v1'
def validate(data):
    if type(data) is not dict or set(data)!={'schema','puzzle','colors'}:
        raise ValueError('Only schema, puzzle, and colors are allowed')
    if data['schema']!=SCHEMA:raise ValueError('Unsupported schema')
    puzzle=data['puzzle']
    if type(puzzle) is not dict or set(puzzle)!={'name','definition'}:
        raise ValueError('Only name and definition are allowed in puzzle')
    if puzzle['name']!='full-600-cell':raise ValueError('Unsupported puzzle')
    definition=puzzle['definition']
    if type(definition) is not list or not definition or any(type(s) is not str for s in definition):
        raise ValueError('Definition must be a nonempty list of strings')
    allowed={'Dim','NAxis','Faces','Group','Axis','Twists','Cuts','FixedMask'}
    if len(definition)!=8 or {s.split()[0] if s.split() else '' for s in definition}!=allowed:
        raise ValueError('Unexpected puzzle definition directives')
    if any('\n' in s or '\r' in s for s in definition):raise ValueError('Multiline directive')
    colors=data['colors']
    if type(colors) is not list or len(colors)!=259800 or any(type(c) is not int or not 0<=c<600 for c in colors):
        raise ValueError('Expected 259800 integer colors in range 0..599')
    counts=[0]*600
    for color in colors:counts[color]+=1
    if any(n!=433 for n in counts):raise ValueError('Invalid global color counts')
    return data

def load(path):return validate(json.loads(Path(path).read_text(encoding='utf-8-sig')))
def save(path,data):
    validate(data)
    Path(path).write_text(json.dumps(data,separators=(',',':'))+'\n',encoding='utf-8')
