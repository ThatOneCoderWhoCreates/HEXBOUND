# Modly model prep

Reduces Modly .glb exports (vertex colored, often 150k to 700k triangles) to game-friendly counts and writes clean .glb files.

Requirements: Python 3 with numpy, scipy, trimesh and fast_simplification.

`export.py` reads `/mnt/user-data/uploads/*_final.glb`; change the glob at the bottom and the `plan` table (file key, output name, triangle target) for new models. Output goes to `out/`.
Colors are kept as exported by Modly. For the lantern, warm bright faces of the lamp get a separate glowing material.
