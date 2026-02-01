# Changelog

All notable changes to the LPT-UNO project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0] - 2026-01-31


### Added
- Initial release of LPT-UNO Parallel Port Printer Emulator
- Arduino Uno firmware with IEEE 1284 compatibility mode
- Web-based interface with Web Serial API support
- Auto-save feature (saves data every 10 seconds)
- Multi-language support (English, Portuguese, Spanish)
- Serial commands for monitoring and debugging (V, S, R, ?)
- 256-byte circular buffer for reliable data handling
- Hardware interrupt support for fast response (<2µs)
- Real-time data visualization in browser
- Manual save as TXT file
- Connection status indicators
- Firmware version display
- Buffer statistics monitoring
- **Controle de auto-print via Ativar_AutoPrint.bat e Desativar_AutoPrint.bat**
- **Automatic printing via LPT-UNO_MoveToData.ps1 (PowerShell)**
- **Launcher LPT-UNO_AutoPrint_Direct.bat para fluxo completo**

### Hardware
- Full pinout for DB25 to Arduino Uno
- Support for 8-bit parallel data (D0-D7)
- Control signals: STROBE, ACK, BUSY, SELECT
- Interrupt-driven STROBE detection on pin 2 (INT0)
- LED indicator on pin 13 (SELECT signal)

### Documentation
- Comprehensive README in English
- Detailed pinout diagram (PINOUT.txt)
- Wiring instructions and connection guide
- Troubleshooting section
- Technical specifications
- Development guidelines


### Files
- `LPT_Emulator/LPT_Emulator.ino` - Arduino firmware v1.0
- `web_interface.html` - Web interface v1.0
- `Ativar_AutoPrint.bat` - Ativa auto-print e abre interface
- `Desativar_AutoPrint.bat` - Desativa auto-print e abre interface
- `LPT-UNO_AutoPrint_Direct.bat` - Launcher principal (abre interface e monitor)
- `LPT-UNO_MoveToData.ps1` - Script PowerShell: move e imprime arquivos
- `PINOUT.txt` - ASCII art pinout diagram
- `README.md` - Complete documentation
- `LICENSE` - MIT License
- `.gitignore` - Git ignore rules
- `.github/copilot-instructions.md` - Coding guidelines

---

## [Unreleased]

### Future Ideas
- Support for other Arduino boards (Mega, Due, ESP32)
- Bidirectional communication (EPP/ECP modes)
- Mobile-friendly responsive interface
- Linux/macOS launcher scripts
- Advanced printer emulation features
- Configuration file support
- Data filtering and formatting options
- Multiple buffer size options
- Advanced timing adjustment
- Built-in data logger
- Network printing support
- Virtual printer driver

---

## Version Numbering

- **Major (X.0.0)**: Breaking changes, major new features, architectural changes
- **Minor (1.X.0)**: New features, improvements, significant bug fixes
- **Patch (1.0.X)**: Small bug fixes, documentation updates (optional)

---

*For a complete list of changes, see the [commit history](https://github.com/yourusername/LPT-UNO/commits).*
