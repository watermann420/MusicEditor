# MusicEngineEditor

See the root `README.md` for full usage. Highlights of the current editor build:
- Shared audio engine (no double-open MIDI), faster first run.
- Inline visuals and glow for notes/pattern steps and `midi.device(n)` literals (only while events occur).
- Output/Console/Errors tabs: MIDI logs route to Output/Console, errors stay in Errors.

## Performance profiles (modular on/off)
Set environment variables before starting the editor to trim unused systems (handy for game-engine embedding or low-power laptops):

- `ME_PERF_PROFILE=low` disables MIDI, sequencer start, inline visuals, and perf monitor; forces 44.1k/1024 buffers.
- `ME_PERF_PROFILE=balanced` (default) keeps prior behaviour.
- `ME_PERF_PROFILE=high` keeps everything on and nudges 48k/256 buffers.
- Fine-grained overrides: `ME_DISABLE_MIDI=1`, `ME_DISABLE_SEQUENCER=1`, `ME_DISABLE_PERF_MONITOR=1`, `ME_DISABLE_INLINE_VISUALS=1`, `ME_SAMPLE_RATE=48000`, `ME_BUFFER_SIZE=512`.
