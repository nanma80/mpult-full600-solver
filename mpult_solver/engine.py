"""Checked native rendering of planned corrections."""
from array import array
import csv,hashlib,subprocess
from .paths import ROOT,BASELINE
from .state import load,save
from .io import job,read_ints

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

