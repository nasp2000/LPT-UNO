# GitHub Upload Preparation Guide

## ✅ Pre-Upload Checklist


### 1. Files to Include
- ✅ `LPT_Emulator/LPT_Emulator.ino` - Arduino firmware
- ✅ `web_interface.html` - Web interface
- ✅ `Ativar_AutoPrint.bat` - Ativa auto-print e abre interface
- ✅ `Desativar_AutoPrint.bat` - Desativa auto-print e abre interface
- ✅ `LPT-UNO_AutoPrint_Direct.bat` - Launcher principal (abre interface e monitor)
- ✅ `LPT-UNO_MoveToData.ps1` - PowerShell: move e imprime arquivos
- ✅ `README.md` - Complete documentation (English)
- ✅ `CHANGELOG.md` - Version history
- ✅ `LICENSE` - MIT License
- ✅ `PINOUT.txt` - Wiring diagram
- ✅ `.gitignore` - Git ignore rules
- ✅ `.github/copilot-instructions.md` - Development guidelines

### 2. Files to EXCLUDE (Not Related to Project)
- ❌ `apps-script-email-sender.gs` - Google Apps Script (unrelated)
- ❌ `GOOGLE_APPS_SCRIPT.md` - Google documentation (unrelated)
- ❌ `LPT-UNO.code-workspace` - Personal VS Code settings (optional, add to .gitignore)

### 3. Verification Steps

#### Check Version Numbers
```bash
# Arduino firmware
grep "FIRMWARE_VERSION" LPT_Emulator/LPT_Emulator.ino
# Should show: #define FIRMWARE_VERSION "1.0"

# Web interface
grep "Web Interface v" web_interface.html
# Should show: Web Interface v1.0 | Build: 2026-01-25
```

#### Check File Encoding
- All files must be UTF-8 encoded
- Test Portuguese characters: ç, á, é, í, ó, ú, ã, õ


#### Test Functionality
- [ ] Arduino code compiles without errors
- [ ] Web interface opens in browser
- [ ] Ativar_AutoPrint.bat ativa auto-print e abre interface
- [ ] Desativar_AutoPrint.bat desativa auto-print e abre interface
- [ ] Impressão automática só ocorre se `.autoprint_enabled` existir na pasta DATA
- [ ] LPT-UNO_MoveToData.ps1 move arquivos de Downloads para DATA e imprime

---

## 🚀 GitHub Upload Steps

### Option 1: GitHub Web Interface

1. **Create Repository**
   - Go to https://github.com/new
   - Repository name: `LPT-UNO`
   - Description: "Arduino-based parallel port printer emulator with web interface"
   - Choose: **Public** (to share with community)
   - ✅ Add README file: **NO** (we already have one)
   - ✅ Add .gitignore: **NO** (we already have one)
   - Choose license: **MIT** (we already have LICENSE file)
   - Click **Create repository**

2. **Upload Files**
   - Click "uploading an existing file"
   - Drag and drop files/folders **EXCEPT** the ones marked with ❌ above
   - Commit message: "Initial release v1.0 - LPT-UNO Parallel Port Emulator"
   - Click **Commit changes**

### Option 2: Git Command Line

```bash
# Navigate to project folder
cd "C:\Users\nasp2\Desktop\Code\LPT-UNO"

# Remove unrelated files first
Remove-Item "apps-script-email-sender.gs" -Force
Remove-Item "GOOGLE_APPS_SCRIPT.md" -Force

# Initialize git repository (if not done)
git init

# Add all files (except those in .gitignore)
git add .

# Commit
git commit -m "Initial release v1.0 - LPT-UNO Parallel Port Emulator"

# Add remote (replace YOUR_USERNAME)
git remote add origin https://github.com/YOUR_USERNAME/LPT-UNO.git

# Push to GitHub
git branch -M main
git push -u origin main
```

---

## 📋 Post-Upload Tasks

### 1. Add Topics/Tags on GitHub
Navigate to repository settings and add:
- `arduino`
- `parallel-port`
- `lpt`
- `printer-emulator`
- `web-serial-api`
- `ieee-1284`
- `retro-computing`
- `dos`
- `legacy-hardware`

### 2. Create Release (Optional)
- Go to Releases → Draft a new release
- Tag: `v1.0`
- Title: `LPT-UNO v1.0 - Initial Release`
- Description: Copy from CHANGELOG.md
- Attach files: ZIP with all project files

### 3. Add GitHub Pages (Optional)
- Settings → Pages
- Source: Deploy from branch `main`
- Folder: `/` (root)
- This will host `web_interface.html` at: https://yourusername.github.io/LPT-UNO/

### 4. Enable Discussions (Optional)
- Settings → Features
- ✅ Enable Discussions
- Great for community support and Q&A

### 5. Add Issue Templates
Create `.github/ISSUE_TEMPLATE/bug_report.md`:
```markdown
---
name: Bug Report
about: Create a report to help improve LPT-UNO
---

**Describe the bug**
A clear description of what the bug is.

**Hardware Setup**
- Arduino board: [e.g., Uno R3]
- Browser: [e.g., Chrome 120]
- OS: [e.g., Windows 11]

**Steps to Reproduce**
1. Go to '...'
2. Click on '...'
3. See error

**Expected behavior**
What you expected to happen.

**Screenshots**
If applicable, add screenshots.

**Additional context**
Any other relevant information.
```

---

## 🌟 Promote Your Project

### Share On:
- Reddit: r/arduino, r/retrocomputing, r/vintagecomputing
- Arduino Forums: https://forum.arduino.cc/
- Hackster.io: Create project page
- Hackaday.io: Submit project
- Twitter/X: @arduino, #Arduino, #RetroComputing

### Add Badges to README
Already included:
- Arduino badge
- Firmware version
- License badge
- Web Serial API badge

---

## 📊 Monitor Success

### GitHub Insights
- Watch: Stars ⭐
- Watch: Forks 🍴
- Watch: Issues opened
- Watch: Pull requests

### Suggested README Updates
After GitHub is live:
1. Replace placeholder URLs:
   - `https://github.com/yourusername/LPT-UNO` → actual URL
   - `https://github.com/yourusername/LPT-UNO/issues` → actual URL
2. Add real screenshot (replace placeholder)
3. Update Support section with actual links

---

## ✨ Final Check

Before uploading, run this command to see what will be committed:

```bash
git status
git diff --staged
```

Make sure:
- ✅ No personal information in files
- ✅ No API keys or passwords
- ✅ No unnecessary large files
- ✅ All documentation is clear and complete
- ✅ All paths and URLs are correct

---

**Ready to upload? Good luck! 🚀**

*Remember to replace `yourusername` with your actual GitHub username in README.md before committing!*
