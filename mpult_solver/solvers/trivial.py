"""Position solving for any orbit with trivial local orientation."""
from collections import defaultdict
from ..paths import ROOT
from ..catalog import OrbitColorCatalog
from ..state import validate
from ..permutations import permutation_parity,three_cycles

class TrivialOrbitSolver(OrbitColorCatalog):
    def __init__(self,orbit_id,root=ROOT,routing_dir=None):super().__init__(orbit_id,root,routing_dir,1)

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

