# Where the algorithms are saved

The expensive discovery work is preserved as code AND immutable data. Keep both.

- `analysis/orbit_routing/orbitN/primitive_word.txt`:selected position three-cycle. Orbit 29 uses `analysis/isolated_routing_schreier` instead.
- `routing_stage_0/1/2.tsv`:setup moves that place any required triple at the working anchors.
- `analysis/compound_flips/orbitN`:paired binary flips and their working positions.
- `analysis/cyclic_orientations/orbitN`:paired cyclic twists.
- `analysis/dihedral4_kernel`:the five-element kernel library and paired-reflection word for D5.
- `analysis/corner35_orientations`:the 60-element corner orientation library.
- `analysis/pieces`:physical-piece positions, transported sticker frames, orientation groups and characters.
- `analysis/solver_dependencies/graph.json`:the selected algorithms' dependency graph and tested order.

Move words are explicit numeric `axis:twist:turn_amount:layer_mask` sequences. Long algorithms are stored literally. A setup and inverse setup are assembled around a saved primitive when solving. There is no LLM memory dependency and no need to repeat the discovery search.

Do not globally Git-ignore .bin files:some are essential piece/routing catalogs. Ignore generated output directories instead. `doctor.py` verifies every shipped data asset against ASSET_MANIFEST.json.

The implementation now lives in mpult_solver/. See ARCHITECTURE.md for the shared catalog/permutation modules and orientation-specific solvers. Frozen algorithm data keeps its original paths and bytes.
