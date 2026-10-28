"""Color-only orbit35 position plans and nonabelian orientation validation."""
from pathlib import Path
import base64,csv
from color_state import validate
from orbit29_solver import ROOT,OrbitColorCatalog,permutation_parity,three_cycles

class Corner35Solver(OrbitColorCatalog):
    def __init__(self,root=ROOT):
        super().__init__(35,root,required_group_order=60)
        if self.degree!=20 or self.n!=120:raise ValueError('Unexpected corner catalog')
        self.identities=[tuple(sorted(key)) for key in self.solved]
        if len(set(self.identities))!=120:raise ValueError('Corners must be uniquely identifiable')
        self.identity_source={key:i for i,key in enumerate(self.identities)}
        row=next(r for r in csv.DictReader((Path(root)/'analysis/pieces/orientation_groups.tsv').open(),delimiter='\t') if r['orbit']=='35')
        self.orientations=[tuple(base64.b64decode(x)) for x in row['permutations_base64'].split(',')]
        self.orientation_set=set(self.orientations)
        assert len(self.orientation_set)==60

    def orient(self,key,permutation):
        result=[None]*20
        for i,j in enumerate(permutation):result[j]=key[i]
        return tuple(result)

    def checked(self,data):
        validate(data)
        if data['puzzle']['definition']!=self.definition:raise ValueError('Changed geometry')
        basis=[tuple(data['colors'][p] for p in ps) for ps in self.positions];sources=[]
        for actual in basis:
            source=self.identity_source.get(tuple(sorted(actual)))
            if source is None:raise ValueError('Invalid corner color identity')
            lookup={color:i for i,color in enumerate(self.solved[source])};observed=[None]*20
            for dest,color in enumerate(actual):observed[lookup[color]]=dest
            if tuple(observed) not in self.orientation_set:raise ValueError('Corner orientation outside group60')
            sources.append(source)
        if sorted(sources)!=list(range(120)):raise ValueError('Invalid corner identity multiplicities')
        if permutation_parity(sources):raise ValueError('Odd corner permutation is unreachable')
        return basis,sources

    def position_plan(self,data):
        basis,sources=self.checked(data);cycles=three_cycles(sources)
        return dict(orbit=35,phase='positions',cycles=cycles,cycle_count=len(cycles),piece_source_at_slot=sources,unique_identities=120)

    def orientation_plan(self,data):
        basis,sources=self.checked(data)
        if sources!=list(range(120)):raise ValueError('Corner positions must be solved first')
        wrong=[i for i,key in enumerate(basis) if key!=self.solved[i]]
        # Native orientation renderer derives exact local corrections from these
        # same colors using its independently checked60-state library/routes.
        return dict(orbit=35,phase='orientations',cycles=[],cycle_count=0,misoriented_slots=wrong,correction_count=len(wrong),renderer='corner35_orientations')
