# Technical Reference — LPT-UNO

## Important Notes

- **STROBE must be on Arduino Pin 2 (INT0)**: Arduino Uno only has hardware interrupts on pins 2 and 3 (INT0/INT1). The STROBE signal MUST be connected to pin 2 (INT0) for the emulator to work.
- **Voltage levels**: Parallel ports use 5V TTL logic (Arduino Uno compatible).
- **Do NOT use** 3.3V Arduinos without proper level shifting.
- **Cable length**: Prefer cables < 2 meters to avoid noise and data corruption.
- **Ground connection**: Connect all DB25 ground pins (18-25) to Arduino GND.

---

## Software Installation

### Step 1: Upload Arduino Firmware

1. Download or clone this repository
2. Open `LPT_Emulator/LPT_Emulator.ino` in Arduino IDE
3. Tools → Board → Arduino Uno
4. Tools → Port → [Your COM Port]
5. Upload (Ctrl+U)

### Step 2: Test the Connection

Open Arduino Serial Monitor (115200 baud):
- `V` - show firmware version
- `S` - show statistics
- `R` - reset buffer
- `?` - help

### Step 3: Launch Native App

- Windows (recommended): build and run the `LPTUnoApp` application (see `LPTUnoApp/README.md`) to open the native interface and enable auto-print/move monitor.
- The legacy `web_interface.html` has been removed in this branch; use the native app for connection and printing.

---

## Web Interface Details

- **Encodings**: UTF-8 (default), ISO-8859-1 (Latin), CP-437 (DOS), Windows-1252. For CP-437 the UI uses a conversion table; other encodings use TextDecoder.
- **Auto-save**: configurable inactivity timer (default 10s).
- **Auto-print**: controlled by the native app (AutoPrint flag and Move Monitor). Files are moved from Downloads to `DATA` and printed by the app's `MoveManager` / `PrintManager` components (replacing `LPT-UNO_MoveToData.ps1`).

---

## Communication & Timing (IEEE 1284 compatibility)

1. PC places data on D0-D7 pins
2. PC toggles STROBE (HIGH → LOW transition)
3. Arduino detects STROBE via hardware interrupt
4. Arduino sets BUSY while processing
5. Arduino reads D0-D7 and stores the byte in a 256-byte circular buffer
6. Arduino pulses ACK (~5 μs low)
7. Arduino clears BUSY and forwards data via USB Serial in the main loop

**Timing specs:**
- STROBE detection: < 2 μs
- BUSY activation: ~10 μs
- ACK pulse: ~5 μs
- Max theoretical throughput: ~100 kB/s
- Serial baud rate: 115200

---

## Serial Commands

Type commands at 115200 baud in the Serial Monitor:

| Command | Action |
|---------|--------|
| `V` / `v` | Show firmware version and build date |
| `S` / `s` | Show statistics (buffer usage) |
| `R` / `r` | Reset buffer |
| `?` | Help |

Example:
```
> V
Firmware Version: 1.0
Build Date: Jan 25 2026 14:30:00

> S
Buffer usage: 42/256 bytes
```

---

## Pinout & Wiring

See `PINOUT.txt` for a complete ASCII diagram. Key mappings:
- STROBE: DB25 pin 1 → Arduino D2
- D0..D7: DB25 pins 2..9 → Arduino D3..D10
- ACK: DB25 pin 10 → Arduino D11
- BUSY: DB25 pin 11 → Arduino D12
- SELECT: DB25 pin 13 → Arduino D13
- GND: DB25 pins 18..25 → Arduino GND

---

## Debugging Tips

- If no data: confirm wiring and STROBE pin; test with Arduino Serial Monitor first.
- If characters are garbled: try CP-437 (DOS) or ISO-8859-1 in the UI; verify encoding of the source data.
- If auto-print doesn't run: ensure `.autoprint_enabled` exists in `DATA` and the native app's Move Monitor is running (see `LPTUnoApp` MoveManager).

---

(End of technical reference)
