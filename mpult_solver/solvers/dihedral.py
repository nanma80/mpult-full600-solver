"""Color-only D5 edge-midpoint solver: positions, C2 quotient, C5 kernel."""
import base64,csv
from pathlib import Path
from ..state import validate
from ..paths import ROOT
from ..catalog import OrbitColorCatalog
from ..permutations import permutation_parity,three_cycles

class Dihedral4Solver(OrbitColorCatalog):
    def __init__(self,root=ROOT):
        super().__init__(4,root,required_group_order=10)
        self.identities=[tuple(sorted(k)) for k in self.solved]
        self.by_identity={k:i for i,k in enumerate(self.identities)}
        if len(self.by_identity)!=720:raise ValueError('Expected unique720 edge identities')
        row=next(r for r in csv.DictReader((Path(root)/'analysis/pieces/orientation_characters.tsv').open(),delimiter='\t') if r['orbit']=='4')
        if row['derived_order']!='5' or row['quotient_order']!='2':raise ValueError('Unexpected D5 quotient')
        self.character={tuple(base64.b64decode(z.split(':')[0])):int(z.split(':')[1]) for z in row['character_base64_residue'].split(',')}
        self.elements=sorted(self.character,key=lambda p:(self.character[p],p))
        if len(self.elements)!=10 or sum(self.character.values())!=5:raise ValueError('Incomplete D5 catalog')
        for a in self.elements:
            for b in self.elements:
                if self.character[tuple(a[b[i]] for i in range(5))]!=(self.character[a]+self.character[b])%2:raise ValueError('Character not homomorphism')
        meta=dict(z.split('=',1) for z in (Path(root)/'analysis/dihedral4_kernel/metadata.txt').read_text().splitlines())
        if meta['checks']!='PASS' or meta['slot_full_label_checks']!='720':raise ValueError('Kernel/pair library not verified')
        self.pair_indices=list(map(int,meta['pair_root_indices'].split(',')))

    @staticmethod
    def orient(colors,p):
        result=[None]*5
        for i,j in enumerate(p):result[j]=colors[i]
        return tuple(result)

    def checked(self,data):
        validate(data)
        if data['puzzle']['definition']!=self.definition:raise ValueError('Changed geometry')
        basis=[tuple(data['colors'][p] for p in ps) for ps in self.positions];lift=[];maps=[]
        for key in basis:
            identity=tuple(sorted(key))
            if identity not in self.by_identity:raise ValueError('Unknown edge identity')
            source=self.by_identity[identity];solved=self.solved[source]
            p=tuple(key.index(color) for color in solved)
            if p not in self.character:raise ValueError('Orientation outside D5')
            lift.append(source);maps.append(p)
        if sorted(lift)!=list(range(self.n)):raise ValueError('Duplicate/missing edge identity')
        if permutation_parity(lift):raise ValueError('Odd identifiable edge permutation')
        bits=[self.character[p] for p in maps]
        if sum(bits)%2:raise ValueError('Odd global D5 quotient sum')
        return basis,lift,maps,bits

    def position_plan(self,data):
        basis,lift,maps,bits=self.checked(data);cycles=three_cycles(lift)
        return dict(orbit=4,phase='positions',slots=self.n,cycles=cycles,cycle_count=len(cycles),chosen_lift_source_at_slot=lift,parity_repair_slots=None,quotient_sum=0)

    def quotient_plan(self,data):
        basis,lift,maps,bits=self.checked(data)
        if lift!=list(range(self.n)):raise ValueError('Solve edge positions first')
        flipped=[p for p,b in enumerate(bits) if b];triples=[]
        for a,b in zip(flipped[::2],flipped[1::2]):
            dummy=next(p for p in range(self.n) if p not in (a,b));triple=[dummy]*3;triple[self.pair_indices[0]]=a;triple[self.pair_indices[1]]=b;triples.append(triple)
        return dict(orbit=4,phase='quotient',cycles=triples,cycle_count=len(triples),quotient_slots=flipped)

    def kernel_plan(self,data):
        basis,lift,maps,bits=self.checked(data)
        if lift!=list(range(self.n)) or any(bits):raise ValueError('Solve positions and quotient before kernel')
        return dict(orbit=4,phase='kernel',cycles=[],cycle_count=0)
