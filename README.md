# MPUlt full 600-cell solver

A deterministic, color-only solver for all 35 movable piece orbits of MPUlt 1.55's **full shallow-cut600-cell**. It is a constructive solver, not a short-solution optimizer. Typical solutions contain about eight million moves.

The complete workflow has been exercised in the real app. Most importantly, a user created a fresh scramble after solver development, ran the command independently, applied all 35 macros, and saved a solved result matching every 259,800 predicted sticker color. See [validation](docs/VALIDATION.md).

## Requirements

- 64-bit Windows; the build uses the .NET Framework 4.x C# compiler.
- Python 3.10 or newer. Solving uses the Python standard library; no pip packages are required.
- Your own working MPUlt 1.55 application folder containing the three Microsoft.DirectX DLLs. The app and DLLs are not included here.
- The full 600-cell puzzle, named `600-cell-full` in the latest MPUlt download. The included example already has the right definition.

## Download MPUlt and enable the full 600-cell

1. Open the [Magic Puzzle Ultimate (MPUlt) page](https://superliminal.com/andrey/mpu/) and follow **“Download the puzzle here”**. Extract the download into a folder. This solver is tested with MPUlt 1.55; make sure the application runs before setting up the solver.
2. In the latest package (updated 2026-10-28), select **`600-cell-full`** from the puzzle menu before scrambling. The full puzzle is already included; no text-file editing is required. `600-cell-simplified` is the separate simplified version.

### Existing installations

The simplest option is to download the latest ZIP and extract it into a new folder. To keep an older installation, you can instead copy `MPUlt_puzzles.txt` from the latest package into that installation; preserve any personal puzzle definitions before replacing the file.

Alternatively, edit the older installation's `MPUlt_puzzles.txt` manually:

1. Find the simplified 600-cell block, typically named `Puzzle 600-cell` in older downloads. Copy the entire block, stopping before the next `Puzzle` line (or at the end of the file). Menu definitions do **not** use `EndPuzzle`; that marker is used in logs.
2. Name the copy `Puzzle 600-cell-full` and remove its `Simplified` line. Leave all other settings unchanged, including `Cuts 0.968 -0.968`. Keep the original simplified block; you may rename it `Puzzle 600-cell-simplified`.
3. Save the file, reopen MPUlt and select `600-cell-full`.

For a complete ready-to-copy menu block, use [examples/600-cell-full.puzzle.txt](examples/600-cell-full.puzzle.txt).

**Custom puzzle names are supported:** `600-cell-full`, `full-600-cell`, or simply `600-cell` all work. The solver reads the name from your input log and preserves it in generated macros and expected-state logs. Names must be a single token without whitespace. The remaining definition must exactly match the supported full puzzle, including `Cuts 0.968 -0.968` and no `Simplified` line; changing the name does not relax the geometry check. The bundled example uses `600-cell-full`.

The recovered reference source is pinned and vendored under `vendor/mpult`, with its original MIT notice. It supplies geometry and the reference macro reader; the actual1.55 application remains authoritative.

## Set up

Clone/download this repository, then from its root run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\setup.ps1 -MPUltDirectory "C:\path\to\MPUlt_1.55"
python doctor.py --engine
```

Setup compiles the native engine and copies the required DLLs from your application folder. These generated/local files are Git-ignored. The execution-policy option applies only to that setup process. No personal source-directory paths or original research folder are needed.

## Generate a solution

Use a new output directory for each run:

```powershell
python solve.py examples/full_scramble_06.log --output runs/example06
```

Or scramble the full puzzle in MPUlt, save a log and pass that file instead. The original input is not changed. The strict adapter passes only colors and puzzle definition into solver planning; scramble history never becomes solving input.

The command simulates every correction, checks that completed orbits stay solved, and produces:

- `joint_solution.dat`:35 orbit entries and one concatenated entry.
- `README.md` and `macro_anchor.png`:application instructions and the click reference.
- Expected state logs, a manifest, progress and diagnostic files.

Large generated macros/logs are deliberately not committed. They are disposable, reproducible outputs. `runs/` and `outputs/` are ignored. Keep the small source inputs and saved algorithm data.

## Apply in MPUlt

Open the **same exact input log** used for generation. Load the generated `joint_solution.dat`. After opening a log, hold the right mouse button and drag downward a little until the white cell emerges into view; selecting the puzzle from the menu starts with that cell already in front. Apply each step at the red-marked reference point, keeping the same point for all steps. See the [view instructions and reference screenshot](docs/MACRO_ANCHOR.md).

Apply either all 35 steps separately in order OR the combined entry once, from that original state. Follow the generated README. A combined run is long; separate entries allow staged execution.

If you save the final app state, compare it with:

```powershell
python check.py runs/example06/35_expected_after_29.log runs/example06/results/after_steps.log
```

The picture marks a geometric location, not a fixed screen coordinate; camera scale and sticker colors can differ. The result should be a completely solved puzzle.

## Code layout

```text
solve.py / check.py / doctor.py   Public Python commands
setup.ps1                        Native build setup
mpult_solver/                    Internal Python package
  catalog.py / permutations.py   Shared solving foundations
  solvers/                       Solvers by orientation structure
engine/                          C# engine extensions
analysis/                        Frozen algorithms and routing/catalog data
assets/                          Model calibration and reference screenshot
docs/                            Human overview, architecture and validation
examples/                        Small reproducible inputs
vendor/mpult/                    Pinned reference source and original license
```

See [architecture](docs/ARCHITECTURE.md) for the internal module map. Run the focused checks with `python -m unittest discover -s tests -v`. The public commands are unchanged; no package installation or pip dependencies are required.

## What is preserved

- Production Python planning/serialization code and native C# engine extensions.
- All selected building-block move sequences, routing tables, orientation libraries and piece catalogs.
- A small real scramble, baseline/anchor calibration and the screenshot.
- [Human overview](docs/OVERVIEW.html), [Markdown overview](docs/OVERVIEW.md), [dependency graph](docs/DEPENDENCIES.md), [algorithm storage guide](docs/ALGORITHMS.md), and [provenance](docs/PROVENANCE.md).
- `ASSET_MANIFEST.json`:SHA256 fingerprints of the immutable runtime data.

No algorithm discovery, AI session or stored eight-million-move solution is needed for a new solve.

## Sharing and preservation

This repository is the maintained source for future solver development and use. The original research scripts and experiments outside it are deprecated as production entry points and retained as a separate historical archive. Use Git commits to preserve revisions, and push/copy the repository off this computer to protect against computer failure. A local Git repository alone is not an off-computer backup.

The new solver code, generated algorithm data and documentation are available under the [MIT license](LICENSE). The bundled MPUlt reference source retains its [original MIT notice](vendor/mpult/LICENSE). No original MPUlt executable or DirectX binaries are distributed by this repository.

See the [publishing and backup checklist](docs/PUBLISHING.md) for the remaining maintainer steps.

## Clean-clone restore check

A separate Git clone in a path with spaces was rebuilt and used to solve the included scramble03. It reproduced the **same 7,982,494-move macro bytes and all 35 expected state logs** as the earlier actual-app-validated package. All 324 data assets passed their hashes, and production imports resolved inside the clone. This was a relocation check on the same Windows computer, not a separate-machine test. See PORTABILITY_CHECK.json for the tested code commit and exact macro hash.
