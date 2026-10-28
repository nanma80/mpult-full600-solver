"""Physical-piece catalogs in transported sticker frames; no history input."""
from pathlib import Path
from collections import defaultdict
from array import array
import csv,json
from .paths import ROOT

class OrbitColorCatalog:
    def __init__(self,orbit_id,root=ROOT,routing_dir=None,required_group_order=1):
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

