"""Color-only, deterministic isolated solver for one model piece orbit.

This module imports no log/history tools. Catalog labels are chosen lifts of
indistinguishable colors, never labels recovered from a scramble.
"""
from pathlib import Path
from collections import defaultdict
from array import array
import csv,json,hashlib,random
from color_state import load,validate

ROOT=Path(__file__).resolve().parent

def permutation_parity(p):
    seen=set();sign=0
    for start in range(len(p)):
        if start in seen:continue
        q=start;length=0
        while q not in seen:seen.add(q);length+=1;q=p[q]
        if q!=start:raise ValueError('Not a permutation')
        sign^=(length-1)&1
    return sign

def apply_cycle(values,cycle):
    a,b,c=cycle
    values[a],values[b],values[c]=values[c],values[a],values[b]

def three_cycles(permutation):
    if sorted(permutation)!=list(range(len(permutation))):raise ValueError('Not a permutation')
    swaps=[];seen=set()
    for start in range(len(permutation)):
        if start in seen:continue
        cycle=[];q=start
        while q not in seen:seen.add(q);cycle.append(q);q=permutation[q]
        swaps.extend((cycle[0],p) for p in cycle[1:])
    if len(swaps)%2:raise ValueError('Odd permutation has no three-cycle decomposition')
    result=[]
    for first,second in zip(swaps[::2],swaps[1::2]):
        common=set(first)&set(second)
        if len(common)==2:continue
        if common:
            def image(x):
                for a,b in (first,second):
                    if x==a:x=b
                    elif x==b:x=a
                return x
            a=min(set(first)|set(second));b=image(a);c=image(b)
            assert image(c)==a
            result.append((a,b,c))
        else:
            a,b=first;c,d=second
            result.extend([(a,c,d),(a,c,b)])
    values=permutation.copy()
    for cycle in result:apply_cycle(values,cycle)
    assert values==list(range(len(values)))
    assert len(result)<=len(permutation)
    return result

class OrbitColorCatalog:
    def __init__(self,orbit_id=29,root=ROOT,routing_dir=None,required_group_order=1):
        self.root=Path(root);self.orbit_id=int(orbit_id);folder=self.root/'analysis/pieces'
        self.definition=json.loads((self.root/'analysis/model_definition.json').read_text())
        raw=(folder/'transport_base_to_slot.bytes.bin').read_bytes();offset=0
        by_piece={}
        for row in csv.DictReader((folder/'pieces.tsv').open(),delimiter='\t'):
            n=row['positions'].count(',')+1
            if row['orbit']==str(self.orbit_id):
                positions=tuple(map(int,row['positions'].split(',')));t=raw[offset:offset+n]
                assert sorted(t)==list(range(n))
                by_piece[int(row['piece'])]=tuple(positions[i] for i in t)
            offset+=n
        group=next(r for r in csv.DictReader((folder/'orientation_groups.tsv').open(),delimiter='\t') if r['orbit']==str(self.orbit_id))
        if int(group['group_order'])!=required_group_order:raise ValueError('Unsupported local orientation image for this solver')
        self.group_order=int(group['group_order']);self.degree=int(group['stickers_per_piece'])
        if offset!=259800:raise ValueError('Invalid piece catalog length')
        routing_dir=Path(routing_dir) if routing_dir else self.root/('analysis/isolated_routing_schreier' if self.orbit_id==29 else f'analysis/orbit_routing/orbit{self.orbit_id}')
        metadata=dict(line.split('=',1) for line in (routing_dir/'routing_metadata.txt').read_text().splitlines())
        if metadata.get('conjugation_validation','PASS')!='PASS':raise ValueError('Routing catalog validation is pending')
        if metadata['piece_orbit']!=str(self.orbit_id) or metadata['connected_stabilizer_graphs']!='True':raise ValueError('Unverified routing catalog')
        slots=array('i');slots.frombytes((routing_dir/'orbit_slots.int32.bin').read_bytes())
        self.piece_ids=list(slots);self.positions=[by_piece[p] for p in slots]
        self.n=len(self.positions)
        assert self.n==int(metadata['slots']) and self.n==len(by_piece)
        self.solved=[tuple(p//433 for p in positions) for positions in self.positions]
        self.buckets=defaultdict(list)
        for slot,key in enumerate(self.solved):self.buckets[key].append(slot)

class TrivialOrbitSolver(OrbitColorCatalog):
    def __init__(self,orbit_id=29,root=ROOT,routing_dir=None):super().__init__(orbit_id,root,routing_dir,1)

    def solve(self,data):
        validate(data)
        if data['puzzle']['definition']!=self.definition:raise ValueError('Definition does not match the validated geometry')
        keys=[tuple(data['colors'][p] for p in ps) for ps in self.positions]
        observed=defaultdict(list)
        for slot,key in enumerate(keys):observed[key].append(slot)
        if {k:len(v) for k,v in observed.items()}!={k:len(v) for k,v in self.buckets.items()}:
            raise ValueError('Target orbit oriented color identities have invalid multiplicities')
        lift=[-1]*self.n
        for key,slots in observed.items():
            fixed={slot for slot in slots if self.solved[slot]==key}
            for slot in fixed:lift[slot]=slot
            remaining_slots=[slot for slot in slots if slot not in fixed]
            remaining_labels=[slot for slot in self.buckets[key] if slot not in fixed]
            for slot,label in zip(remaining_slots,remaining_labels):lift[slot]=label
        parity_repair=None
        if permutation_parity(lift):
            choices=[]
            for slots in observed.values():
                if len(slots)<2:continue
                slots=sorted(slots,key=lambda s:(lift[s]==s,s))
                choices.append((sum(lift[s]==s for s in slots[:2]),slots[:2]))
            if not choices:raise ValueError('No even lift demonstrated')
            _,(a,b)=min(choices)
            lift[a],lift[b]=lift[b],lift[a];parity_repair=[a,b]
        assert not permutation_parity(lift)
        assert all(self.solved[source]==keys[slot] for slot,source in enumerate(lift))
        cycles=three_cycles(lift)
        return dict(orbit=self.orbit_id,slots=self.n,initial_wrong_colors=sum(data['colors'][p]!=p//433 for ps in self.positions for p in ps),
                    displaced_lift_labels=sum(i!=p for i,p in enumerate(lift)),parity_repair_slots=parity_repair,
                    chosen_lift_source_at_slot=lift,cycles=cycles,cycle_count=len(cycles),
                    caveat='One-orbit model solver; chosen labels are gauge choices. Other orbits are not solved.')

class Orbit29Solver(TrivialOrbitSolver):
    def __init__(self,root=ROOT):super().__init__(29,root)

def self_test():
    rng=random.Random(29155)
    for n in range(3,51):
        for _ in range(20):
            p=list(range(n));rng.shuffle(p)
            if permutation_parity(p):p[0],p[1]=p[1],p[0]
            three_cycles(p)
    assert len(three_cycles([1,0,3,2]))==2
    assert three_cycles(list(range(20)))==[]

if __name__=='__main__':
    import sys
    if len(sys.argv)!=3:raise SystemExit('Usage: orbit29_solver.py input.color.json output.plan.json')
    self_test();source=Path(sys.argv[1]);result=Orbit29Solver().solve(load(source))
    result['input_sha256']=hashlib.sha256(source.read_bytes()).hexdigest()
    Path(sys.argv[2]).write_text(json.dumps(result,separators=(',',':'))+'\n')
    print('Orbit-29 three-cycles:',result['cycle_count'])
