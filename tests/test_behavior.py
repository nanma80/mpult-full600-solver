"""Behavioral checks for the shared planning core and public check command."""
import json,random,shutil,subprocess,sys,tempfile,unittest
from pathlib import Path
from mpult_solver.paths import ROOT,BASELINE
from mpult_solver.permutations import permutation_parity,three_cycles,apply_cycle
from mpult_solver.state import validate
from mpult_solver.state_adapter import export
from mpult_solver.log_format import write_log,verify_log
from mpult_solver.macros import write_macros

class PermutationTests(unittest.TestCase):
    def test_even_permutations_are_corrected(self):
        rng=random.Random(29155)
        for n in range(3,76):
            for _ in range(10):
                p=list(range(n));rng.shuffle(p)
                if permutation_parity(p):p[0],p[1]=p[1],p[0]
                for cycle in three_cycles(p):apply_cycle(p,cycle)
                self.assertEqual(p,list(range(n)))
    def test_odd_permutation_rejected(self):
        with self.assertRaises(ValueError):three_cycles([1,0,2])

class InputTests(unittest.TestCase):
    def test_custom_names_roundtrip_without_changing_colors_or_moves(self):
        original=verify_log(ROOT/'examples/full_scramble_06.log')
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder)
            for name in ['full-600-cell','600-cell-full','600-cell','my_puzzle']:
                source=root/'input.log'
                text=(ROOT/'examples/full_scramble_06.log').read_bytes().replace(b'600-cell-full',name.encode())
                source.write_bytes(text)
                verify_log(source)
                state=export(source,root/'colors.json')
                self.assertEqual(state['puzzle']['name'],name)
                self.assertEqual(state['colors'],original['colors'])
                description=['Puzzle '+name,*state['puzzle']['definition'],'EndPuzzle']
                write_log(root/'expected.log',description,state['colors'],[])
                self.assertEqual(export(root/'expected.log',root/'expected.json'),state)
                write_macros(root/'macro.dat',[('test',[0,0,1,1])],puzzle_name=name)
                self.assertEqual((root/'macro.dat').read_text().splitlines()[1],name+' 4 2')
    def test_custom_names_do_not_relax_geometry(self):
        definition=json.loads((ROOT/'analysis/model_definition.json').read_text())
        state={'schema':'mpult-color-state/v1','puzzle':{'name':'600-cell','definition':definition},'colors':[i//433 for i in range(259800)]}
        for name in ['', 'two words','bad\nname']:
            with self.assertRaises(ValueError):validate(dict(state,puzzle=dict(state['puzzle'],name=name)))
        for changed in [definition+['Simplified'],[s.replace('0.968','0.97') for s in definition]]:
            with self.assertRaises(ValueError):validate(dict(state,puzzle=dict(state['puzzle'],definition=changed)))
    def test_history_excluded_at_both_levels(self):
        state={'schema':'mpult-color-state/v1','puzzle':{'name':'full-600-cell','definition':json.loads((ROOT/'analysis/model_definition.json').read_text())},'colors':[i//433 for i in range(259800)]}
        validate(state)
        for bad in [dict(state,history=[]),dict(state,puzzle=dict(state['puzzle'],history=[]))]:
            with self.assertRaises(ValueError):validate(bad)

class PublicCommandTests(unittest.TestCase):
    def compare(self,actual,expected_match):
        with tempfile.TemporaryDirectory() as folder:
            path=Path(folder)/'actual.log';shutil.copyfile(actual,path)
            expected=Path(folder)/'expected.log'
            name=verify_log(path)['description'][0][len('Puzzle '):]
            expected.write_bytes(BASELINE.read_bytes().replace(b'full-600-cell',name.encode()))
            run=subprocess.run([sys.executable,str(ROOT/'check.py'),str(expected),str(path)],cwd=folder,capture_output=True,text=True)
            self.assertEqual(run.returncode,0 if expected_match else 1,run.stderr)
            result=json.loads(run.stdout);self.assertEqual(result['match'],expected_match);self.assertTrue(result['crc_verified'])
    def test_check_matches_from_another_working_directory(self):self.compare(BASELINE,True)
    def test_check_reports_scrambled_state(self):self.compare(ROOT/'examples/full_scramble_06.log',False)
    def test_doctor_locates_assets_from_another_directory(self):
        with tempfile.TemporaryDirectory() as folder:
            run=subprocess.run([sys.executable,str(ROOT/'doctor.py')],cwd=folder,capture_output=True,text=True)
            self.assertEqual(run.returncode,0,run.stderr)
            expected=len(json.loads((ROOT/'ASSET_MANIFEST.json').read_text())['files'])
            self.assertEqual(json.loads(run.stdout)['asset_files'],expected)

if __name__=='__main__':unittest.main()
