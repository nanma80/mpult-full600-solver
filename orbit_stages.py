"""Shared deterministic stage execution for regression/research workflows."""
from pathlib import Path
from orbit29_solver import ROOT,TrivialOrbitSolver
from binary_orbit_solver import BinaryOrbitSolver
from cyclic_orbit_solver import CyclicOrbitSolver
from corner35_solver import Corner35Solver
from mpult_workflow import render

def solve_stage(data,orbit,folder,collateral=None):
    folder=Path(folder);routing=ROOT/('analysis/isolated_routing_schreier' if orbit==29 else f'analysis/orbit_routing/orbit{orbit}')
    metadata=dict(x.split('=',1) for x in (routing/'routing_metadata.txt').read_text().splitlines())
    if collateral is None:collateral=list(map(int,filter(None,metadata.get('allowed_collateral','').split(','))))
    mode=metadata.get('orientation_mode','trivial')
    if mode=='trivial':
        solver=TrivialOrbitSolver(orbit);data,word,stats=render(folder,'solve',data,solver.solve(data),orbit,collateral=collateral);return data,word,[stats]
    if mode=='dihedral10':
        from dihedral4_solver import Dihedral4Solver
        solver=Dihedral4Solver()
        data,word,a=render(folder/'positions','positions',data,solver.position_plan(data),4,'dihedral_positions',collateral)
        data,last,b=render(folder/'quotient','quotient',data,solver.quotient_plan(data),4,'dihedral_quotient',collateral,ROOT/'analysis/dihedral4_kernel/paired_reflection.word.txt');word.extend(last)
        data,last,c=render(folder/'kernel','kernel',data,solver.kernel_plan(data),4,'dihedral4_kernel',collateral);word.extend(last)
        return data,word,[a,b,c]
    solver=Corner35Solver() if mode=='nonabelian60' else CyclicOrbitSolver(orbit) if mode=='cyclic' else BinaryOrbitSolver(orbit)
    position_mode='nonabelian_positions' if mode=='nonabelian60' else mode+'_positions'
    data,word,first=render(folder/'positions','positions',data,solver.position_plan(data),orbit,position_mode,collateral)
    orientation_mode='corner35_orientations' if mode=='nonabelian60' else mode+'_orientations'
    override=None if mode=='nonabelian60' else ROOT/f'analysis/{"cyclic_orientations" if mode=="cyclic" else "compound_flips"}/orbit{orbit}/candidate_01.word.txt'
    data,last,second=render(folder/'orientations','orientations',data,solver.orientation_plan(data),orbit,orientation_mode,collateral,override);word.extend(last)
    return data,word,[first,second]
