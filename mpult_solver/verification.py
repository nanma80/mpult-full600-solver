"""Comparison of expected and application-saved puzzle states."""
from pathlib import Path
import json
from .log_format import verify_log
from .engine import digest

def compare_logs(expected,actual):
    expected=Path(expected);actual=Path(actual);a=verify_log(expected);b=verify_log(actual)
    if a['description']!=b['description'] or len(a['colors'])!=len(b['colors']):raise ValueError('Puzzle/state size mismatch')
    result=dict(match=a['colors']==b['colors'],color_mismatches=sum(x!=y for x,y in zip(a['colors'],b['colors'])),
                positions=len(a['colors']),crc_verified=True,expected_sha256=digest(expected),actual_sha256=digest(actual))
    actual.with_suffix('.comparison.json').write_text(json.dumps(result,indent=2))
    return result

