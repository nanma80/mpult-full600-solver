# Provenance and attribution

The original application is available from [Andrey Astrelin’s MPUlt page](https://superliminal.com/andrey/mpu/), through “Download the puzzle here”. Melinda Green updated the download package on 2026-10-28 to include `600-cell-full` alongside `600-cell-simplified`. The root README recommends the latest package and retains manual setup instructions for older installations.

The bundled `examples/full_scramble_06.log` is an untouched, app-generated scramble supplied by the maintainer from the updated download. It is preserved byte-for-byte, with its original `600-cell-full` name. SHA256: `f096157eae41b12bc044748fa0bf9e2d3f4084cebca441ceff38abf742111140`. It replaces the briefly included renamed copy of scramble 03. Historical validation records retain the original inputs, names and hashes actually tested at the time. Internal model and calibration identifiers remain unchanged.

MPUlt was created by Andrey Astrelin. MPUlt 1.55 and its generated states are the behavioral reference for this solver. The recovered source is a reference snapshot and is not asserted to be the source of 1.55.

The geometry/reference-reader source under vendor/mpult is pinned to the supplied cutelyaware/MPUlt checkout at commit 0e2b86cbb713b8286083fa964ea67d6582337d5a. Its original README and MIT license notice (copyright 2018 Melinda Green) are preserved. No upstream Git history or executable is bundled.

The new solver was developed collaboratively by the project maintainer and OpenAI Codex. It uses current sticker colors only, plus geometry/algorithm data built and checked during investigation. The macro-anchor image was supplied and annotated by the maintainer, and is preserved unchanged.

This repository contains selected reusable runtime data and code; the complete research archive, rejected searches and large generated runs remain outside it. No claim about being the first such solver is made here.

The maintainer also supplied `assets/reference/log_start_view.png`, showing the viewpoint immediately after loading a log, before dragging. It is preserved unchanged and paired with the marked macro-anchor screenshot in the usage guide and future generated solution folders.
