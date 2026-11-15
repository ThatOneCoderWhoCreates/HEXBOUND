# Celestial Sanctuary builder

Python scripts that generate the Celestial Sanctuary map model (`Sanctuary.glb`) and its textures.

Requirements: Python 3 with numpy, trimesh and Pillow.

```
python textures.py
python build.py
python preview.py
```

- `textures.py` writes `Rune.png` and `Banner.jpg` (if missing) and `Floor.jpg`, the courtyard flagstones. The floor and banner textures are embedded in the model.
- `build.py` writes `Sanctuary.glb`. Copy it and `Rune.png` to `Assets/_Project/Resources/Arena/Sanctuary/`.
- `preview.py` saves rough preview renders to `/tmp/v1.png` ... `/tmp/v4.png`.

The scene is authored in Unity coordinates (+Z is the gate side) and mirrored on X when written, because glTFast mirrors X on import.
The playable edge (12 m) and the four crystal pillars must match `Arena.cs` in the simulation.
