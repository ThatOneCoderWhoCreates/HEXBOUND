Sky Ruins scene builder.

python3 textures.py   (RuinsFloor.jpg, RuinsWall.jpg)
python3 build.py      (Ruins.glb, read from Scripts/Runtime/Simulation/Collision/Arena.cs so walls, platforms and stairs match the game logic; set ARENA_CS in build.py)
python3 preview.py    (rough top and side renders)
python3 prep_model.py <modly.glb> <Name> <triangles>   (Modly shape-only output to a stone-colored, decimated model in models/)
