"""Color-only two-sticker binary-orientation solver; no log/history dependency."""
from collections import defaultdict
from pathlib import Path
import json
from ..state import validate
from ..paths import ROOT
from ..catalog import OrbitColorCatalog
from ..permutations import permutation_parity,three_cycles

class BinaryOrbitSolver(OrbitColorCatalog):
    def __init__(self,orbit_id=33,root=ROOT,routing_dir=None):
        super().__init__(orbit_id,root,routing_dir,2)
        if self.degree!=2:raise ValueError('Binary solver requires exactly two stickers per piece')
        self.identities=[tuple(sorted(key)) for key in self.solved]
        self.identity_buckets=defaultdict(list)
        for i,key in enumerate(self.identities):self.identity_buckets[key].append(i)
        routing=Path(root)/f'analysis/orbit_routing/orbit{orbit_id}'
        metadata=dict(line.split('=',1) for line in (routing/'routing_metadata.txt').read_text().splitlines())
        roots=list(map(int,metadata['primitive_piece_cycle'].split(',')))
        flip_lines=(Path(root)/f'analysis/compound_flips/orbit{orbit_id}/candidates.tsv').read_text().splitlines()
        if len(flip_lines)<2:raise ValueError(f'Paired-flip primitive not established for orbit{orbit_id}; orientation solver pending')
        flip_row=flip_lines[1].split('\t')
        flipped=list(map(int,flip_row[4].split(',')))
        self.flip_root_indices=[roots.index(p) for p in flipped]
        if len(set(self.flip_root_indices))!=2:raise ValueError('Invalid paired-flip support')

    def checked(self,data):
        validate(data)
        if data['puzzle']['definition']!=self.definition:raise ValueError('Definition does not match validated geometry')
        basis=[tuple(data['colors'][p] for p in ps) for ps in self.positions]
        if any(len(set(key))!=2 for key in basis):raise ValueError('Repeated color on a two-sticker piece')
        return basis

    def position_plan(self,data):
        basis=self.checked(data);keys=[tuple(sorted(key)) for key in basis];observed=defaultdict(list)
        for i,key in enumerate(keys):observed[key].append(i)
        if {k:len(v) for k,v in observed.items()}!={k:len(v) for k,v in self.identity_buckets.items()}:
            raise ValueError('Invalid target identity multiplicities')
        lift=[-1]*self.n
        for key,slots in observed.items():
            fixed={p for p in slots if self.identities[p]==key}
            for p in fixed:lift[p]=p
            for p,q in zip((p for p in slots if p not in fixed),(p for p in self.identity_buckets[key] if p not in fixed)):lift[p]=q
        repair=None
        if permutation_parity(lift):
            choices=[]
            for slots in observed.values():
                if len(slots)<2:continue
                slots=sorted(slots,key=lambda p:(lift[p]==p,p));choices.append((sum(lift[p]==p for p in slots[:2]),slots[:2]))
            if not choices:raise ValueError('No compatible even position lift')
            _,(a,b)=min(choices);lift[a],lift[b]=lift[b],lift[a];repair=[a,b]
        assert not permutation_parity(lift)
        bits=[int(basis[p]!=self.solved[source]) for p,source in enumerate(lift)]
        assert all(basis[p]==self.solved[source] or basis[p]==self.solved[source][::-1] for p,source in enumerate(lift))
        if sum(bits)%2:raise ValueError('Odd global binary orientation sum is unreachable in this model')
        cycles=three_cycles(lift)
        return dict(orbit=self.orbit_id,phase='positions',slots=self.n,cycles=cycles,cycle_count=len(cycles),parity_repair_slots=repair,
                    initial_flip_count_in_chosen_lift=sum(bits),initial_flip_sum=0,chosen_lift_source_at_slot=lift)

    def orientation_plan(self,data):
        basis=self.checked(data)
        if any(tuple(sorted(key))!=identity for key,identity in zip(basis,self.identities)):
            raise ValueError('Piece identities must be solved before orientation repair')
        flipped=[i for i,key in enumerate(basis) if key!=self.solved[i]]
        if len(flipped)%2:raise ValueError('Odd flip count cannot be repaired by paired flips')
        triples=[];pairs=[]
        for a,b in zip(flipped[::2],flipped[1::2]):
            dummy=next(p for p in range(self.n) if p!=a and p!=b);triple=[dummy]*3
            triple[self.flip_root_indices[0]]=a;triple[self.flip_root_indices[1]]=b
            assert len(set(triple))==3
            triples.append(triple);pairs.append([a,b])
        return dict(orbit=self.orbit_id,phase='orientations',slots=self.n,flipped_slots=flipped,pairs=pairs,
                    cycles=triples,cycle_count=len(triples),pair_count=len(pairs),flip_root_indices=self.flip_root_indices)
