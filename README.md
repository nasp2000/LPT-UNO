# LPT-UNO - Parallel Port Printer Emulator

[![Arduino](https://img.shields.io/badge/Arduino-Uno-00979D?logo=arduino)](https://www.arduino.cc/)
[![Firmware](https://img.shields.io/badge/Firmware-v1.0-blue)]()
[![License](https://img.shields.io/badge/License-MIT-green)]()
[![Web Serial API](https://img.shields.io/badge/Web%20Serial%20API-Chrome%2FEdge-orange)]()

Transform your Arduino Uno into a **parallel port printer emulator (LPT/DB25)** that receives data through the parallel interface and forwards it via USB Serial to your PC for visualization and printing in a modern web browser with advanced features.

[![Technical details](https://img.shields.io/badge/Docs-Technical%20Details-blue?logo=book)](docs/TECHNICAL.md)  `→` See full technical reference (timings, wiring, commands) in `docs/TECHNICAL.md`

You no longer need to keep your parallel printer running!!

Perfect for reviving old DOS applications, legacy software testing, or educational purposes!

---

<p align="center">
  <img src="images/image1.png" alt="Screenshot 1" width="45%" />
  <img src="images/image2.png" alt="Screenshot 2" width="45%" />
</p>

<p align="center">
  <img src="images/image3.png" alt="Screenshot 3" width="45%" />
  <img src="images/image4.png" alt="Screenshot 4" width="45%" />
</p>

## Features

- Full IEEE 1284 compatibility (Parallel LPT/DB25)
- Web UI with real-time visualization and selectable encodings (UTF‑8 default)
- Auto-save and optional auto-print via provided scripts
- 256‑byte circular buffer, interrupt-driven (low-latency)

---

## Quick Start (3 steps)

1. Upload firmware: open `LPT_Emulator/LPT_Emulator.ino` in Arduino IDE and upload (115200 baud).
2. Launch the web UI: run `LPT-UNO_AutoPrint_Direct.bat` (Windows launcher) or open `web_interface.html` in Chrome/Edge/Opera.
3. Auto-print (optional): enable with `Ativar_AutoPrint.bat` (creates `.autoprint_enabled` in `DATA`); `Desativar_AutoPrint.bat` disables it.

> Connect to the Arduino from the web UI to start receiving data.

---

## 🔌 Hardware Setup

### Hardware

- **Arduino**: Uno R3 (ATmega328P) recommended
- **DB25 connector**: use 5V TTL signalling
- See `PINOUT.txt` for a full wiring diagram (STROBE → D2, D0..D7 → D3..D10, ACK → D11, BUSY → D12, SELECT → D13, GND pins 18–25 → GND)
```

## Important (short)

- **STROBE → Arduino D2 (INT0)** — critical.
- Use **5V TTL** levels; avoid 3.3V boards without level shifting.
- Ground DB25 pins **18–25** to Arduino GND.
- Firmware: upload `LPT_Emulator/LPT_Emulator.ino` at **115200** baud.
- Web UI: `web_interface.html` (Chrome/Edge/Opera) or run `LPT-UNO_AutoPrint_Direct.bat` on Windows.

**More details:** see [docs/TECHNICAL.md](docs/TECHNICAL.md) (timings, wiring, troubleshooting).

```
> V
Firmware Version: 1.0
Build Date: Jan 25 2026 14:30:00

> S
Buffer usage: 42/256 bytes

> R
Buffer reset
```

---

<details>
<summary>Advanced Usage (click to expand)</summary>

- **Silent Printing (Windows):** Use `Ativar_AutoPrint.bat` / `Desativar_AutoPrint.bat` to enable/disable automatic printing (see **Auto-Print** for details).
- **Custom Printer:** Set your desired printer as the system default before launching the app.
- **Testing Without Hardware:** Open `web_interface.html` and use the developer 'Simulate Test Data' feature.

</details>

---

## 📁 Project Structure

<details>
<summary>Project structure (click to expand)</summary>

- `LPT_Emulator/LPT_Emulator.ino`
- `web_interface.html`
- `Ativar_AutoPrint.bat`
- `Desativar_AutoPrint.bat`
- `LPT-UNO_AutoPrint_Direct.bat`
- `LPT-UNO_MoveToData.ps1`
- `PINOUT.txt`
- `README.md`

</details>

---

<details>
<summary>Troubleshooting (click to expand)</summary>

- **Arduino doesn't respond:** check USB cable, COM port, reset the board, re-upload firmware.
- **Web interface can't connect:** use Chrome/Edge/Opera, close other apps using the serial port, refresh, check the console (F12).
- **No data received:** verify wiring and confirm STROBE is on pin 2; test with Arduino Serial Monitor.
- **Auto-print issues:** ensure `.autoprint_enabled` exists in `DATA` and a default printer is configured.
- **Characters garbled:** try different encoding (CP‑437 for DOS), check cables and grounding.

</details>

---

## 🔧 Technical Specifications

### Hardware
- **Microcontroller**: ATmega328P (Arduino Uno)
- **Input pins**: 9 (8 data + 1 interrupt)
- **Output pins**: 3 (ACK, BUSY, SELECT)
- **Buffer size**: 256 bytes (circular)
- **Response time**: < 2 µs (interrupt-driven)

### Software
- **Firmware version**: 1.0
- **Web interface version**: 1.0
- **Build date**: 2026-01-25
- **Serial speed**: 115200 baud
- **Encoding**: UTF-8
- **Monitoring and printing**: via LPT-UNO_MoveToData.ps1

### Compatibility
- **Arduino boards**: Uno R3, Uno R4 (5V variants)
- **Browsers**: Chrome 89+, Edge 89+, Opera 76+
- **Operating systems**: Windows 10/11, Linux, macOS
- **Protocols**: IEEE 1284 compatibility mode

---

## 📝 Development Guidelines

### Version Control

**Always update** when modifying code:

#### Arduino Firmware
Define version constants in the firmware (example): `#define FIRMWARE_VERSION "X.Y"` and `#define BUILD_DATE __DATE__`.

#### Web Interface
The web interface displays its build info in the footer (example: `<p id="firmwareInfo">Web Interface v1.0 | Build: 2026-01-25</p>`).

### Encoding (development)
- Prefer **UTF-8** for all source files and user-facing text. The web UI supports selectable encodings (UTF-8 default; CP‑437 is available for DOS-era content).

### Code Style
- **Comments**: Portuguese (for this project)
- **Variable names**: English (standard practice)
- **User-facing text**: Multi-language support

---

## 🤝 Contributing

Contributions are welcome! Please:

1. Fork this repository
2. Create a feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

### Ideas for Contributions
- Support for other Arduino boards (Mega, Due, etc.)
- Bidirectional communication (EPP/ECP modes)
- Mobile-friendly web interface
- Additional printer emulation modes
- Linux/macOS launcher scripts
- Automated testing suite

---

## 📜 License

This project is licensed under the **MIT License** - see below for details:

```
MIT License

Copyright (c) 2026 LPT-UNO Project

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

## 🙏 Acknowledgments

- **IEEE 1284 Standard** for parallel port specifications
- **Web Serial API** for enabling browser-hardware communication
- **Arduino Community** for extensive documentation and support
- **DOS/Legacy Software Enthusiasts** for keeping retro computing alive

---

## 📞 Support

- **Issues**: [GitHub Issues](https://github.com/nasp2000/lpt-uno/issues)
- **Discussions**: [GitHub Discussions](https://github.com/nasp2000/lpt-uno/discussions)
- **Documentation**: See `PINOUT.txt` for detailed wiring diagrams

---

## 🌟 Star This Project

If you find this project useful, please give it a ⭐ on GitHub!

---

**Made with ❤️ for the retro computing community**

*Last updated: January 25, 2026 | Version 1.0*
