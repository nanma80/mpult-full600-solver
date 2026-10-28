"""Routine color-only solve/verify/package commands with small JSON output.

Usage: mpult_workflow.py solve-three INPUT --output DIRECTORY
       mpult_workflow.py solve-supported INPUT --output DIRECTORY
       mpult_workflow.py check EXPECTED_LOG ACTUAL_LOG
"""
from pathlib import Path
from array import array
from itertools import chain
import argparse,csv,hashlib,json,subprocess,sys
from color_state import load,save
from export_color_state import export
from orbit29_solver import TrivialOrbitSolver
from binary_orbit_solver import BinaryOrbitSolver
from mpult_files import write_log,verify_log,pack
from workflow_io import job,read_ints

ROOT=Path(__file__).resolve().parent
BASELINE=ROOT/'assets/model/baseline.log'
ANCHOR_FILE=ROOT/'assets/model/anchor.dat'
def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def render(folder,name,data,plan,orbit,mode='trivial',collateral=(),override=None):
    folder.mkdir(parents=True,exist_ok=True)
    routing=ROOT/('analysis/isolated_routing_schreier' if orbit==29 else f'analysis/orbit_routing/orbit{orbit}')
    config=f'orbit={orbit}\nrouting_directory={routing}\nallowed_collateral='+','.join(map(str,collateral))+f'\norientation_mode={mode}\n'
    if override:config+='primitive_override='+str(override)+'\n'
    (folder/'renderer_config.txt').write_text(config)
    (folder/'jobs.tsv').write_text(job(folder,name,data,plan))
    with (folder/'execution.txt').open('w') as log:
        command='--render-dihedral4-kernel' if mode=='dihedral4_kernel' else '--render-corner35-orientations' if mode=='corner35_orientations' else '--render-trivial-orbit'
        run=subprocess.run([str(ROOT/'MoveEngine.exe'),str(BASELINE),command,str(folder)],stdout=log,stderr=subprocess.STDOUT)
    if run.returncode:raise RuntimeError('Engine check failed; see '+str(folder/'execution.txt'))
    stats=list(csv.DictReader((folder/'render_results.tsv').open(),delimiter='\t'))[0]
    state=dict(data,colors=read_ints(folder/(name+'.final.int32.bin')))
    save(folder/'result.color.json',state)
    flat=array('i');flat.frombytes((folder/(name+'.moves.int32.bin')).read_bytes())
    if len(flat)%4:raise ValueError('Invalid move word')
    return load(folder/'result.color.json'),flat,stats

def codes(parts):
    for flat in parts:
        for i in range(0,len(flat),4):yield pack(flat[i:i+4])

def write_macros(path,steps,joint_name='all_steps_33_34_29'):
    template=ANCHOR_FILE.read_text().splitlines();anchor=template[2].split();point=template[3]
    entries=[(name,[flat]) for name,flat in steps]+[(joint_name,[flat for _,flat in steps])]
    with path.open('w',encoding='utf-8',newline='') as w:
        w.write(f'MPUlt Macro File\r\nfull-600-cell 4 {len(entries)}\r\n')
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
        if r.readline().strip()!='MPUlt Macro File' or r.readline().strip()!=f'full-600-cell 4 {len(entries)}':raise ValueError('Macro header')
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

def solve_three(source,output):
    if sys.byteorder!='little':raise RuntimeError('This workflow requires little-endian int32 artifacts')
    out=Path(output).resolve();source=Path(source).resolve()
    if (out/'manifest.json').exists():raise ValueError('Output already packaged; use a new directory')
    out.mkdir(parents=True,exist_ok=True);(out/'results').mkdir(exist_ok=True);internal=out/'engine_data';internal.mkdir(exist_ok=True)
    if source.suffix.lower()=='.log':export(source,internal/'input.color.json')
    else:save(internal/'input.color.json',load(source))
    initial=load(internal/'input.color.json');data=initial;description=['Puzzle full-600-cell',*data['puzzle']['definition'],'EndPuzzle']
    write_log(out/'00_start_no_history.log',description,data['colors'],[])
    expected_logs=[];steps=[];statistics=[]
    binary=BinaryOrbitSolver();plan=binary.position_plan(data)
    data,position,stats=render(internal/'33_positions','positions',data,plan,33,'binary_positions',[34]);statistics.append(stats)
    data,orientation,stats=render(internal/'33_orientations','orientations',data,binary.orientation_plan(data),33,'binary_orientations',[34],ROOT/'analysis/compound_flips/orbit33/candidate_01.word.txt');statistics.append(stats)
    position.extend(orientation);steps.append(('step_01_orbit_33',position))
    path=out/'01_expected_after_33.log';write_log(path,description,data['colors'],[]);expected_logs.append(path)
    for index,orbit in [(2,34),(3,29)]:
        solver=TrivialOrbitSolver(orbit)
        data,flat,stats=render(internal/f'{orbit}_repair',f'repair{orbit}',data,solver.solve(data),orbit);statistics.append(stats)
        steps.append((f'step_{index:02}_orbit_{orbit}',flat))
        path=out/f'{index:02}_expected_after_{orbit}.log';write_log(path,description,data['colors'],[]);expected_logs.append(path)
    controlled=set()
    for row in csv.DictReader((ROOT/'analysis/pieces/pieces.tsv').open(),delimiter='\t'):
        if int(row['orbit']) in {33,34,29}:controlled.update(map(int,row['positions'].split(',')))
    if len(controlled)!=24000 or any(data['colors'][p]!=(p//433 if p in controlled else initial['colors'][p]) for p in range(259800)):
        raise ValueError('Final three-orbit isolation check failed')
    for path in [out/'00_start_no_history.log',*expected_logs]:verify_log(path)
    write_macros(out/'joint_solution.dat',steps)
    counts={name:len(flat)//4 for name,flat in steps};counts['all_steps_33_34_29']=sum(counts.values())
    manifest=dict(status='model verified; actual joint v1.55 test pending',source_sha256=digest(source),
        strict_solver_input_sha256=digest(internal/'input.color.json'),solved_orbits=[33,34,29],other_unchanged_colors=235800,
        macro_turns=counts,engine_checks=statistics,macro_code_roundtrip='PASS',
        files={p.name:dict(bytes=p.stat().st_size,sha256=digest(p)) for p in [out/'joint_solution.dat',out/'00_start_no_history.log',*expected_logs]})
    (out/'manifest.json').write_text(json.dumps(manifest,indent=2))
    (out/'README.md').write_text('''# Joint three-orbit test

Load `joint_solution.dat`, then open `00_start_no_history.log`. Use the same white-cell top-face reference point as before.

Choose ONE execution option:

- Run `step_01_orbit_33`, then `step_02_orbit_34`, then `step_03_orbit_29`, each forward once, keeping the same reference point. Optional intermediate saves: `results/after_33.log` and `results/after_34.log`.
- Or run `all_steps_33_34_29` forward once. It is the exact concatenation of those three words; do not also run the individual steps on that same starting state.

Save the final state as `results/after_all.log`. The final prediction is `03_expected_after_29.log`. All three orbits should be solved, with the other235,800 colors unchanged; the full puzzle remains scrambled. Expected logs after each step are provided.

The solutions are state-specific, computed from colors only. Each solver stage reads the actual simulated colors from the preceding stage. No scramble history enters the solvers. All stages were fully simulated, output log checksums verified and every macro code read back. The four macro entries share the original tested anchor metadata. Turn counts are in manifest.json.

Check a saved result with `mpult_workflow.py check EXPECTED_LOG ACTUAL_LOG`. This emits one small JSON summary and writes a comparison beside the actual result. Actual joint validation remains pending until these saves are checked.
''',encoding='utf-8')
    return dict(status='READY',output=str(out),macro_turns=counts,verified_solved_orbits=[33,34,29],other_unchanged_colors=235800)

def check(expected,actual):
    expected=Path(expected);actual=Path(actual);a=verify_log(expected);b=verify_log(actual)
    if a['description']!=b['description'] or len(a['colors'])!=len(b['colors']):raise ValueError('Puzzle/state size mismatch')
    result=dict(match=a['colors']==b['colors'],color_mismatches=sum(x!=y for x,y in zip(a['colors'],b['colors'])),
                positions=len(a['colors']),crc_verified=True,expected_sha256=digest(expected),actual_sha256=digest(actual))
    actual.with_suffix('.comparison.json').write_text(json.dumps(result,indent=2))
    return result

def main():
    parser=argparse.ArgumentParser();commands=parser.add_subparsers(dest='command',required=True)
    solve=commands.add_parser('solve-three');solve.add_argument('input');solve.add_argument('--output',required=True)
    supported=commands.add_parser('solve-supported');supported.add_argument('input');supported.add_argument('--output',required=True);supported.add_argument('--orbits',help='Optional explicit dependency-closed orbit order')
    compare=commands.add_parser('check');compare.add_argument('expected');compare.add_argument('actual')
    args=parser.parse_args()
    if args.command=='solve-supported':
        from supported_workflow import solve_supported
        result=solve_supported(args.input,args.output,orbits=args.orbits)
    else:result=solve_three(args.input,args.output) if args.command=='solve-three' else check(args.expected,args.actual)
    print(json.dumps(result,separators=(',',':')))
    if args.command=='check' and not result['match']:raise SystemExit(1)

if __name__=='__main__':main()
