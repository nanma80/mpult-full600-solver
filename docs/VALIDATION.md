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

The clean-clone check PASSED:7,982,494 moves,36 macro entries, exact macro-byte equality and all35 expected logs identical to the original validated package.324 frozen data assets and13 local production imports verified. The newly compiled engine also passed its19341000 map checks and100-move label round trip. See ../PORTABILITY_CHECK.json.
