"""Color-only cyclic-five position/orientation solving in transported bases."""
import base64,csv
from collections import defaultdict
from pathlib import Path
from color_state import validate
from orbit29_solver import ROOT,OrbitColorCatalog,permutation_parity,three_cycles

class CyclicOrbitSolver(OrbitColorCatalog):
    def __init__(self,orbit_id=26,root=ROOT):
        super().__init__(orbit_id,root,required_group_order=5)
        if self.degree!=5:raise ValueError('Requires five-sticker cyclic5 orbit')
        row=next(r for r in csv.DictReader((Path(root)/'analysis/pieces/orientation_characters.tsv').open(),delimiter='\t') if int(r['orbit'])==orbit_id)
        if row['derived_order']!='1' or row['quotient_order']!='5':raise ValueError('Character must be faithful cyclic5')
        self.permutations={int(item.split(':')[1]):tuple(base64.b64decode(item.split(':')[0])) for item in row['character_base64_residue'].split(',')}
        assert set(self.permutations)==set(range(5))
        self.identities=[self.identity(key) for key in self.solved];self.identity_buckets=defaultdict(list)
        for i,key in enumerate(self.identities):self.identity_buckets[key].append(i)
        routing=Path(root)/f'analysis/orbit_routing/orbit{orbit_id}'
        meta=dict(x.split('=',1) for x in (routing/'routing_metadata.txt').read_text().splitlines())
        roots=list(map(int,meta['primitive_piece_cycle'].split(',')))
        pair=next(csv.DictReader((Path(root)/f'analysis/cyclic_orientations/orbit{orbit_id}/candidates.tsv').open(),delimiter='\t'))
        support=[tuple(map(int,item.split(':'))) for item in pair['piece_residues'].split(',')]
        self.root_indices=[roots.index(p) for p,d in support];self.root_delta=support[0][1]
        assert len(set(self.root_indices))==2 and sum(d for p,d in support)%5==0

    def orient(self,key,residue):
        output=[None]*5
        for i,j in enumerate(self.permutations[residue]):output[j]=key[i]
        return tuple(output)

    def identity(self,key):return min(self.orient(key,k) for k in range(5))

    def residue(self,actual,source):
        options=[k for k in range(5) if self.orient(source,k)==actual]
        if len(options)!=1:raise ValueError('Invalid or ambiguous cyclic orientation')
        return options[0]

    def checked(self,data):
        validate(data)
        if data['puzzle']['definition']!=self.definition:raise ValueError('Changed geometry')
        basis=[tuple(data['colors'][p] for p in ps) for ps in self.positions]
        if any(len(set(key))!=5 for key in basis):raise ValueError('Invalid five-color piece')
        return basis

    def position_plan(self,data):
        basis=self.checked(data);observed=defaultdict(list)
        for i,key in enumerate(basis):observed[self.identity(key)].append(i)
        if {k:len(v) for k,v in observed.items()}!={k:len(v) for k,v in self.identity_buckets.items()}:raise ValueError('Invalid cyclic piece identity multiplicities')
        lift=[-1]*self.n
        for key,slots in observed.items():
            fixed={p for p in slots if self.identities[p]==key}
            for p in fixed:lift[p]=p
            for p,q in zip((p for p in slots if p not in fixed),(p for p in self.identity_buckets[key] if p not in fixed)):lift[p]=q
        repair=None
        if permutation_parity(lift):
            choices=[]
            for slots in observed.values():
                if len(slots)>1:
                    slots=sorted(slots,key=lambda p:(lift[p]==p,p));choices.append((sum(lift[p]==p for p in slots[:2]),slots[:2]))
            if not choices:raise ValueError('Odd uniquely identifiable piece permutation')
            _,(a,b)=min(choices);lift[a],lift[b]=lift[b],lift[a];repair=[a,b]
        residues=[self.residue(basis[p],self.solved[source]) for p,source in enumerate(lift)]
        if sum(residues)%5:raise ValueError('Nonzero global cyclic orientation sum')
        cycles=three_cycles(lift)
        return dict(orbit=self.orbit_id,phase='positions',cycles=cycles,cycle_count=len(cycles),parity_repair_slots=repair,initial_orientation_sum=0)

    def orientation_plan(self,data):
        basis=self.checked(data)
        if any(self.identity(key)!=identity for key,identity in zip(basis,self.identities)):raise ValueError('Positions must be solved first')
        residues=[self.residue(key,source) for key,source in zip(basis,self.solved)]
        if sum(residues)%5:raise ValueError('Nonzero global cyclic orientation sum')
        triples=[];corrections=[];anchor=0
        for i,r in enumerate(residues):
            if i==anchor or r==0:continue
            dummy=next(p for p in range(self.n) if p not in (i,anchor));triple=[dummy]*3
            triple[self.root_indices[0]]=i;triple[self.root_indices[1]]=anchor
            power=(-r*pow(self.root_delta,-1,5))%5
            triples.append(triple+[power]);corrections.append([i,anchor,(-r)%5])
        return dict(orbit=self.orbit_id,phase='orientations',cycles=triples,cycle_count=len(triples),orientation_residues=residues,corrections=corrections)
