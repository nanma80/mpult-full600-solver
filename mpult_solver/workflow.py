"""Deterministic solve/package integration for all currently supported orbits."""
from pathlib import Path
from array import array
import csv,json,subprocess,sys,shutil
from .state import load,save
from .state_adapter import export
from .solvers.trivial import TrivialOrbitSolver
from .solvers.binary import BinaryOrbitSolver
from .solvers.cyclic import CyclicOrbitSolver
from .solvers.corners import Corner35Solver
from .stages import solve_stage
from .paths import ROOT,BASELINE
from .engine import digest,render
from .macros import write_macros
from .log_format import verify_log,write_log

def selected_schedule(graph,orbits=None):
    schedule=list(map(int,orbits.split(','))) if orbits else graph['tested_order']
    if not schedule or not set(schedule)<=set(graph['nodes']) or len(schedule)!=len(set(schedule)):raise ValueError('Invalid supported orbit schedule')
    selected=set(schedule)
    if not all(b in selected and schedule.index(a)<schedule.index(b) for a,b in graph['direct_collateral_edges'] if a in selected):raise ValueError('Schedule omits required collateral repair or violates dependency graph')
    return schedule

def solve_supported(source,output,orbits=None):
    if sys.byteorder!='little':raise RuntimeError('Little-endian int32 runtime required')
    source=Path(source).resolve();out=Path(output).resolve()
    if (out/'manifest.json').exists():raise ValueError('Already packaged; choose a new output directory')
    if (out/'progress.json').exists():raise ValueError('Existing incomplete run; choose a new output directory')
    anchor_source=ROOT/'assets/reference/macro_anchor.png'
    if not anchor_source.is_file():raise ValueError('Missing macro reference screenshot: '+str(anchor_source))
    out.mkdir(parents=True,exist_ok=True);(out/'results').mkdir(exist_ok=True);internal=out/'engine_data';internal.mkdir(exist_ok=True)
    anchor_image=out/'macro_anchor.png'
    if anchor_source.resolve()!=anchor_image.resolve():shutil.copyfile(anchor_source,anchor_image)
    graph_path=ROOT/'analysis/solver_dependencies/graph.json';graph=json.loads(graph_path.read_text());schedule=selected_schedule(graph,orbits)
    groups={int(r['orbit']):int(r['group_order']) for r in csv.DictReader((ROOT/'analysis/pieces/orientation_groups.tsv').open(),delimiter='\t')}
    if any(groups[o] not in (1,2,5,10,60) or (groups[o]==60 and o!=35) for o in schedule):raise ValueError('Unsupported solver group')
    source_hash=digest(source)
    if source.suffix.lower()=='.log':verify_log(source);export(source,internal/'input.color.json');start=source
    else:save(internal/'input.color.json',load(source));start=out/'00_start_no_history.log'
    initial=load(internal/'input.color.json');data=initial;description=['Puzzle full-600-cell',*data['puzzle']['definition'],'EndPuzzle']
    if start!=source:write_log(start,description,data['colors'],[]);verify_log(start)
    piece_rows=list(csv.DictReader((ROOT/'analysis/pieces/pieces.tsv').open(),delimiter='\t'))
    positions={o:{int(p) for row in piece_rows if int(row['orbit'])==o for p in row['positions'].split(',')} for o in schedule}
    controlled=set().union(*positions.values());outside=set(range(259800))-controlled;done=set();steps=[];statistics=[];logs=[];native_rows=[]
    def progress(phase,**details):(out/'progress.json').write_text(json.dumps(dict(phase=phase,**details),indent=2))
    progress('solving',completed=[],schedule=schedule)
    for index,orbit in enumerate(schedule,1):
        collateral=sorted(b for a,b in graph['direct_collateral_edges'] if a==orbit)
        if any(o in schedule[:index-1] for o in collateral):raise ValueError('Collateral includes a solved orbit')
        folder=internal/f'{index:02}_orbit_{orbit}';stats=[]
        if groups[orbit]==1:
            solver=TrivialOrbitSolver(orbit)
            data,flat,row=render(folder,'solve',data,solver.solve(data),orbit,collateral=collateral);stats.append(row)
        elif orbit==4:
            data,flat,stats=solve_stage(data,4,folder,collateral)
        elif orbit==35:
            solver=Corner35Solver()
            data,flat,row=render(folder/'positions','positions',data,solver.position_plan(data),35,'nonabelian_positions',collateral);stats.append(row)
            data,orientation,row=render(folder/'orientations','orientations',data,solver.orientation_plan(data),35,'corner35_orientations',collateral);stats.append(row);flat.extend(orientation)
        else:
            solver=CyclicOrbitSolver(orbit) if groups[orbit]==5 else BinaryOrbitSolver(orbit)
            mode='cyclic' if groups[orbit]==5 else 'binary';kind='cyclic_orientations' if groups[orbit]==5 else 'compound_flips'
            data,flat,row=render(folder/'positions','positions',data,solver.position_plan(data),orbit,mode+'_positions',collateral);stats.append(row)
            data,orientation,row=render(folder/'orientations','orientations',data,solver.orientation_plan(data),orbit,mode+'_orientations',collateral,ROOT/f'analysis/{kind}/orbit{orbit}/candidate_01.word.txt');stats.append(row);flat.extend(orientation)
        done.update(positions[orbit])
        if any(data['colors'][p]!=p//433 for p in done):raise ValueError('Completed orbit is not solved')
        if any(data['colors'][p]!=initial['colors'][p] for p in outside):raise ValueError('Unsupported orbit changed')
        name=f'step_{index:02}_orbit_{orbit}';steps.append((name,flat))
        moves=folder/'complete.moves.int32.bin';moves.write_bytes(flat.tobytes());native_rows.append((name,[moves]))
        path=out/f'{index:02}_expected_after_{orbit}.log';write_log(path,description,data['colors'],[]);verify_log(path);logs.append(path)
        statistics.append(dict(orbit=orbit,turns=len(flat)//4,allowed_collateral=collateral,phases=stats,all_completed_orbits_solved=True,unsupported_colors_unchanged=True))
        progress('solving',completed=schedule[:index],schedule=schedule,turns_so_far=sum(len(w)//4 for n,w in steps))
    if done!=controlled or any(data['colors'][p]!=(p//433 if p in controlled else initial['colors'][p]) for p in range(259800)):raise ValueError('Final integration isolation check failed')
    progress('packaging',completed=schedule)
    joint='all_steps_'+ '_'.join(map(str,schedule));write_macros(out/'joint_solution.dat',steps,joint)
    native_rows.append((joint,[paths[0] for name,paths in native_rows]))
    (out/'macro_expected.tsv').write_text('\n'.join(name+'\t'+';'.join(map(str,paths)) for name,paths in native_rows))
    with (out/'native_macro_execution.txt').open('w') as log:
        subprocess.run([str(ROOT/'MoveEngine.exe'),str(BASELINE),'--verify-macro-package',str(out)],stdout=log,stderr=subprocess.STDOUT,check=True)
    if digest(source)!=source_hash:raise ValueError('Source changed during integration')
    whole_puzzle_solved=all(c==p//433 for p,c in enumerate(data['colors']))
    counts={n:len(w)//4 for n,w in steps};counts[joint]=sum(counts.values())
    files=[out/'joint_solution.dat',anchor_image,*logs]
    manifest=dict(status='model integration PASS; manual v1.55 integration pending',source_path=str(source),source_sha256=source_hash,
                  strict_color_input_sha256=digest(internal/'input.color.json'),engine_sha256=digest(ROOT/'MoveEngine.exe'),dependency_graph_sha256=digest(graph_path),
                  schedule=schedule,whole_puzzle_solved=whole_puzzle_solved,solved_colors=len(controlled),other_unchanged_colors=len(outside),macro_turns=counts,stages=statistics,
                  macro_entries=len(steps)+1,packed_move_roundtrip='PASS',reference_macro_reader='PASS',source_unchanged=True,
                  files={p.name:dict(bytes=p.stat().st_size,sha256=digest(p)) for p in files})
    (out/'manifest.json').write_text(json.dumps(manifest,indent=2))
    bullets='\n'.join(f'{i}. `{name}` â€” {len(word)//4:,} turns; expected save `{logs[i-1].name}`.' for i,(name,word) in enumerate(steps,1))
    (out/'README.md').write_text(f'''# Full-scramble integration of {len(schedule)} supported orbits

Start by opening this exact log in MPUlt1.55:

    {start}

Load `joint_solution.dat`. Opening a `.log` file starts with the white cell out of sight: hold the right mouse button and drag downward a little until it emerges from behind the camera and comes into view. Selecting the puzzle from the menu, or starting the app with it automatically loaded, already places the white cell at the front and needs no view adjustment. Use the established top-face anchor above the horizontal edge, slightly left. The red mark in the screenshot below shows the reference point. This package does not require replacing the source with a derived log. The source SHA256 is in manifest.json and was unchanged throughout generation.

![Macro reference point marked in red](macro_anchor.png)

The red mark is the user’s annotation. Match the cell geometry, not a fixed screen coordinate; sticker colors and camera scale may differ. Use the same reference point for every separate step.

Choose ONE option from that starting state:

- Run the individual steps below forward once each in order. Keep the same anchor. Optional saves after any step can be compared with the corresponding expected log.
- Or run `{joint}` forward once. It is the exact concatenation of all individual steps. Do not additionally run the individual steps on that same starting state.

{bullets}

For an individual-step run, save `results/after_steps.log` after the final step. For the combined run, independently reload the original starting log, apply only the combined macro, then save `results/after_combined.log`. Reloading avoids depending on undo behavior. Testing either execution option is sufficient; testing both checks their agreement.

The final prediction is `{logs[-1].name}`. These {len(schedule)} orbits should all be solved, with every other {len(outside):,} sticker color unchanged. {'The full puzzle should be solved.' if whole_puzzle_solved else 'Other orbits remain to be solved.'} The combined macro has {counts[joint]:,} turns.

Compare an actual saved result using:

    python check.py "{logs[-1]}" "{out/'results/after_steps.log'}"

All phases were computed from colors only and fully simulated. Every completed orbit was checked after every stage, unsupported colors were checked unchanged, expected-log checksums were verified, and all packed codes were read back. The recovered reference macro reader also loaded all {len(steps)+1} entries and matched every code to the simulated words. Real MPUlt1.55 integration remains pending until actual saved results are compared.

Reproduce from any valid full-600-cell log with:

    python solve.py INPUT.log --output NEW_OUTPUT_DIRECTORY
''',encoding='utf-8')
    progress('ready',completed=schedule,total_turns=counts[joint],manual_application_check='pending')
    return dict(status='READY',output=str(out),source=str(source),schedule=schedule,total_turns=counts[joint],macro_entries=len(steps)+1,solved_colors=len(controlled),whole_puzzle_solved=whole_puzzle_solved,other_unchanged_colors=len(outside),native_macro_reader='PASS')
