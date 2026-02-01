# LPT-UNO v1.0 — Release Notes

Release: v1.0
Date: 2026-01-31

Highlights
- Initial stable release of LPT-UNO emulator (minimal, prod-ready package).
- Fixed web UI bug (console TypeError null guard) and multiple UI improvements.
- English is now the default UI language for new installations.
- Added encoding selector (UTF-8 default, CP‑437 support included) and persistence of preferences.
- Added recipients/email automation using Google Apps Script integration.
- Improved documentation: condensed README and created docs/TECHNICAL.md.

Included files
- `LPT_Emulator.ino` — Arduino firmware source (ATmega328P, STROBE on D2, 115200 baud)
- `web_interface.html` — Full distributable web UI (interface + JavaScript)
- `PINOUT.txt` — Hardware pin mapping
- `docs/TECHNICAL.md` — Detailed technical reference & timings
- `CHANGELOG.md`, `LICENSE`, helper scripts (`*.bat`, `*.ps1`) and packaging files.

Notes
- This is the minimal packaged release (source + web UI). If you want a compiled firmware binary (`.hex`) included, request and we will add it using Arduino CLI.
- To publish: create a Git tag `v1.0` and upload `LPT-UNO_v1.0.zip` (created in `dist/`) to your release platform.

SHA256: 27D42C9E78EAA22A1C42304ED6240C6C145870C64E4E739D57F0825FF0C726A8
