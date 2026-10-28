# Python implementation layout

The public commands remain solve.py, check.py and doctor.py. Their implementation lives inside mpult_solver/:

| Module | Role |
|---|---|
| paths.py | Repository-relative resource locations |
| catalog.py | Shared physical-piece and transported-sticker catalogs |
| permutations.py | Parity and three-cycle decomposition |
| solvers/trivial.py | Generic positions-only orbit solving |
| solvers/binary.py | C2 position/paired-flip plans |
| solvers/cyclic.py | C5 position/cyclic-orientation plans |
| solvers/dihedral.py | Orbit4 D5 position/quotient/kernel plans |
| solvers/corners.py | Orbit35 A5 position/orientation plans |
| state.py, state_adapter.py | Strict color-only schema and one-way input adapter |
| log_reader.py, log_format.py | Application log parsing, checksums and serialization |
| engine.py, io.py | Checked native rendering and binary batch I/O |
| macros.py | Macro encoding and read-back checks |
| stages.py | Multi-phase execution of one orbit solver |
| workflow.py | Dependency-ordered full solve and output packaging |
| verification.py | Expected versus actual state comparison |
| diagnostics.py | Asset integrity and native engine diagnostics |

The historical orbit29_solver.py has been split into catalog.py, permutations.py and solvers/trivial.py. Its common code was not specific to orbit29. The obsolete three-orbit prototype command and orbit29-only wrapper were removed; the supported public CLI is unchanged.

Algorithm data under analysis/ and assets/ remains byte-for-byte unchanged. Some frozen research notes may refer to historical module filenames. The native code remains under engine/, with original reference source under vendor/mpult/.

Run focused checks with: python -m unittest discover -s tests -v

The full refactoring regression is a clean-clone setup followed by solving examples/full_scramble_03.log and comparing macro bytes and all35 expected state logs against the actual-app-validated output. PORTABILITY_CHECK.json records the result.
