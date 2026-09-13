# Stage asset authoring

`stage_concepts.json` holds the 80 stage concepts and individual American cartoon image prompts. `build_catalog.py` generates `StageCatalog.cs` from the template. Its `sections()` function also drives the audio score so hook times remain identical.

```bash
python3 Tools/StageAssets/build_catalog.py
python3 Tools/StageAssets/compose.py --output /absolute/path/outside-the-repository
```

Requires Python 3, NumPy, SciPy, FFmpeg and FFprobe. The composer writes original MIDI scores, note JSON, cue JSON and 80-second OGG recordings. `--map POP`, `--stage POP-01` and `--overwrite` support targeted revision. Instruments are synthesized from authored oscillators and percussion models; no third-party songs or samples are included. Keep renders, image outputs and composition exports outside Git and distribute them as the requested ZIP.

Image generation uses one distinct built-in image-generation request per stage, with the map's accepted first background as a style reference. Generated PNGs are not reproduced by the Python composer. See `Docs/StageAssets.md` for the Unity integration.
