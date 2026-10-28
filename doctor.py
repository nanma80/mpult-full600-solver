"""Validate shipped algorithm data and, optionally, the rebuilt geometry engine."""
from pathlib import Path
import argparse,hashlib,json,subprocess,sys,tempfile
ROOT=Path(__file__).resolve().parent

def verify_assets():
    expected=json.loads((ROOT/'ASSET_MANIFEST.json').read_text())['files']
    for name,record in expected.items():
        path=ROOT/name
        if not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest()!=record['sha256']:raise ValueError('Missing or changed solver asset: '+name)
    return len(expected)

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--engine',action='store_true');args=ap.parse_args()
    if sys.byteorder!='little' or sys.version_info<(3,10):raise RuntimeError('Requires Python3.10+ on a little-endian machine')
    count=verify_assets();report=dict(status='PASS',asset_files=count,python=sys.version.split()[0],engine_checked=False)
    if args.engine:
        exe=ROOT/'MoveEngine.exe'
        if not exe.is_file():raise RuntimeError('Run setup.ps1 first')
        for name in ['Microsoft.DirectX.dll','Microsoft.DirectX.Direct3D.dll','Microsoft.DirectX.Direct3DX.dll']:
            if not (ROOT/name).is_file():raise RuntimeError('Missing app-supplied dependency: '+name)
        with tempfile.TemporaryDirectory(prefix='mpult-engine-check-') as folder:
            run=subprocess.run([str(exe),str(ROOT/'assets/model/baseline.log'),'--self-check',folder],capture_output=True,text=True)
        if run.returncode:raise RuntimeError(run.stdout+run.stderr)
        report.update(engine_checked=True,engine_output=run.stdout.strip())
    print(json.dumps(report,indent=2))
if __name__=='__main__':main()
