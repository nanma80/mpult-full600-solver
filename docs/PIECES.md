# Full 600-cell: piece locations and orientation groups



[Human-readable puzzle overview](OVERVIEW.html) Â· [Markdown version](OVERVIEW.md) â€” counts, orientation groups, dependency graph and tested solve order.



This reference describes physical piece orbits in the full shallow-cut 600-cell. These are the 35 movable piece-orbit IDs, not the 41 movable sticker-orbit IDs. There are also 600 fixed cell-center pieces.



## Why this is the shallow-cut 600-cell

The full puzzle, named `600-cell-full` in the latest download, inherits `Cuts 0.968 -0.968` from MPUlt's supplied definition. Older installations can enable the same puzzle by removing `Simplified` from a copied puzzle block. The cut value does not need to change.

Here, the shallow-cut regime means that making the cuts arbitrarily shallower preserves the same pieces, sticker incidences and corresponding move permutations. In MPUlt's normalization, larger positive cut values approach the cell's supporting hyperplane at `1`.

A cutting-plane event enumeration on the ideal regular geometry finds the first structural transition below `1` at approximately **0.9635254916**, consistent with the exact expression `(1 + 3 sqrt(5)) / 8`. There are no detected transitions between that threshold and `1`. Since **0.968 > 0.9635254916**, the inherited value lies in the stable shallow-cut regime: it gives the same abstract puzzle as cuts arbitrarily close to `1`, while keeping the pieces at a finite size.

This conclusion is supported by floating-point enumeration of candidate geometric events, rather than a certified exact-arithmetic proof. It concerns ideal geometry; values extremely close to `1` can encounter the simulator's numerical tolerances. The author's supplied `0.968` is an appropriate choice.

## Geometric locations



| Orbits | Physical location | Stickers per piece | Pieces per original feature |

|---|---|---:|---|

|1|Triangular face center|2|1 per face;1200 total|

|3|Relatively large pieces next to the orbit 1 face center (user visually identified in MPUlt 1.55)|2|3 per face;3600 total|

|9, 14, 22, 27, 33|Other triangular-face pieces|2|3 per face for each orbit;3600 per orbit|

|10, 12, 16, 17, 24, 29|Other triangular-face pieces|2|6 per face for each orbit;7200 per orbit|

|4|Edge-center piece|5|1 per edge;720 total|

|13,26|Off-center edge pieces|5|2 per edge for each orbit;1440 per orbit|

|35|Vertex/corner piece|20|1 per vertex;120 total|

|2, 5, 6, 7, 8, 11, 15, 18, 19, 20, 21, 23, 25, 28, 30, 31, 32, 34|Single-sticker pieces within tetrahedral cells|1|Varies by orbit|



Orbit 3 has three visible stickers per triangular face **as viewed from one cell**. Each belongs to a two-sticker piece shared by the two incident cells:3 pieces ×1200 original faces =3600 pieces, carrying7200 stickers in total. This accounts for both cell sides without doubling the physical piece count. The adjacency/relative size is a user visual observation; the piece and sticker counts are model-derived.



The 600-cell has600 tetrahedral cells,1200 triangular faces,720 edges and 120 vertices. A face is shared by 2 cells, an edge by 5 cells and a vertex by 20 cells; the multi-sticker incidences match these original features.



Along an original edge, from one vertex to the other:



    35 -- 26 -- 13 -- 4 -- 13 -- 26 -- 35



This shows order, not scale.13 is nearer the edge center;26 is nearer the vertices.4 contains the exact midpoint. With edge parameter t running from 0 to1, the verified five-sticker chamber intervals, rounded to five decimal places, are:



| Orbit | Intervals along the edge |

|---|---|

|4|[0.21933,0.78067]|

|13|[0.13555,0.21933] and [0.78067,0.86445]|

|26|[0.08378,0.13555] and [0.86445,0.91622]|



Intervals describe intersections with the original edge, not the complete 3D surface shapes of the pieces. Numerical endpoints come from the reconstructed validated geometry; rounding suppresses floating-point symmetry noise.



## Local orientation groups



A local orientation group describes how a piece's stickers can be permuted when it returns to a fixed position. It does not imply that one piece can be reoriented independently of all others.



| Orbits | Group | Order | Interpretation |

|---|---|---:|---|

|1, 3, 9, 14, 22, 27, 33|C2|2|Two-sticker flip|

|13,26|C5|5|Cyclic five-way orientation|

|4|D5|10|Five rotations and five reflection-type permutations of its five stickers|

|35|A5|60|Orientation-preserving icosahedral rotation group|

|All remaining movable orbits|Trivial|1|No independent orientation correction once position is correct|



D5 here means the group of order 10 (some conventions call it D10). Its reflection-type sticker permutations arise from legal orientation-preserving puzzle motions in 4D; they are not arbitrary physical mirror operations. In particular orbit 4 is richer than the cyclic edge orbits13/26.



The seven C2 orbits have global flip-parity restrictions.13/26 have zero total orientation residue modulo5 in the transported basis. Orbit 4's abelian orientation quotient is C2 (its commutator subgroup is C5); its nonabelian orientation requires a separate solver method.35's local group is the full nonabelian A5. These facts do not by themselves establish complete solver coverage; use [current solver status](VALIDATION.md).



## Reflection-related orbit pairs



- 6 <->7

- 15 <->18

- 20 <->21

- 28 <->30



All other movable orbits map to themselves under the verified coordinate reflection. This is a statement about reflection-related piece/move orbits, not a separate proof that each individual piece shape is intrinsically chiral. Reflected algorithms must also have their collateral effects mapped and independently checked.



## Evidence and provenance

Counts and orientation data are in ../analysis/pieces/. The solver documentation and validation summary describe the model and application tests. The complete exploratory search archive remains separate from this runtime repository. The recovered geometry source is vendored with its original license notice and upstream revision.
