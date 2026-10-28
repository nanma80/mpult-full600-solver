"""Public full-puzzle solve command; all planning uses colors only."""
import argparse,json
from pathlib import Path
from mpult_solver.workflow import solve_supported

def main():
    ap=argparse.ArgumentParser(description='Generate a complete MPUlt full600-cell solution from current colors.')
    ap.add_argument('input',type=Path);ap.add_argument('--output',required=True,type=Path)
    args=ap.parse_args()
    if not args.input.is_file():ap.error('Input file does not exist')
    if not (Path(__file__).resolve().parent/'MoveEngine.exe').is_file():ap.error('Run setup.ps1 before solving')
    print(json.dumps(solve_supported(args.input,args.output),indent=2))
if __name__=='__main__':main()
