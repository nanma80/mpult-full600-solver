# Validation

The solver covers259800 sticker positions,177,120 sticker-bearing pieces,176,520 movable pieces and 35 movable piece orbits. Every emitted move is simulated, every completed orbit is checked after each stage, log checksums are verified and the recovered macro reader checks every packed code.

## Original application tests

| Input | Solution moves | Application evidence |
|---|---:|---|
|full_scramble.log|7,943,906|User ran the production workflow and reported a solved puzzle|
|full_scramble_02.log|7,985,585|Separate-step save exactly matches all 259,800 colors with valid CRC; combined solve also user-reported|
|full_scramble_03.log|7,982,494|Fresh post-development scramble, workflow run independently; saved result exactly matches all 259,800 colors with valid CRC|

The included example is the original small full_scramble_03 input, not a solution log. It contains 12000 scramble moves, but planning receives only its current colors and definition. The third test is especially useful because the scramble was created after solver development.

The orientation/routing methods also passed model checks on broad synthetic legal states, exact label permutations and invalid inputs. See the overview for local group constraints and the strategy. Actual-app tests are concrete end-to-end evidence; the completeness/termination argument comes from the constructive model methods.

## Portable checkout verification

The clean-clone restore result is recorded in PORTABILITY_CHECK.json at repository root when the release preparation check is complete. Building in another folder and reproducing the validated macro output checks that no original research-tree paths or missing algorithm data are required.

The clean-clone check PASSED:7,982,494 moves,36 macro entries, exact macro-byte equality and all 35 expected logs identical to the original validated package.324 frozen data assets verified. The newly compiled engine also passed its19341000 map checks and100-move label round trip. See ../PORTABILITY_CHECK.json.

## Package restructuring regression

The reorganized mpult_solver package passed six focused behavior checks and a full fresh-clone build/solve. All 21 implementation modules resolved inside the clone. The result is the identical 7,982,494-move macro, with all 35 expected state logs byte-identical to the actual-app-validated package. The algorithm tables and native source were not changed. PORTABILITY_CHECK.json identifies the tested code revision. Public solve.py/check.py/doctor.py commands retain their arguments.

## Custom puzzle names

Custom names are read from the input log and preserved in generated macro headers, expected-state logs and package manifests. The full puzzle definition remains strictly constrained; `Simplified` and changed cuts are rejected regardless of name. Names are nonempty tokens without whitespace or control characters, matching the MPUlt file formats.

Automated tests cover `full-600-cell`, `600-cell-full`, `600-cell`, and another custom name; they check color-only export, checksummed log round-trips and macro headers. Orbit-29 packaging runs from the same scrambled colors under `600-cell-full` and `600-cell` pass the native reference macro reader and produce identical move payloads after accounting for the name. Solving tables and move algorithms are unchanged.

On 2026-10-28, the maintainer loaded a scrambled log named `600-cell-full` in MPUlt 1.55, loaded its generated custom-name macro, applied one white-cell turn and saved the result. The actual save matched the predicted state at all 259800 sticker positions, with a valid checksum; 4592 sticker colors changed from the starting state. This confirms actual-app custom-name loading and macro execution. Actual-save SHA256: `5a17df107db725ee029cd58a245c20c0f02259b0302c915820b6906e22d41e71`. This is a focused compatibility test; earlier full-solve validations remain the evidence for the complete solving algorithms.

After updating to this feature, rerun `setup.ps1` to rebuild the native verifier alongside the Python changes.
