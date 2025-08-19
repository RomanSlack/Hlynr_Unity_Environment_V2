# Unity Project Setup Guide

## Prerequisites
- Install **Git** and **Git LFS**
  - **Windows**: Install Git for Windows, then run `git lfs install` in Git Bash
  - **macOS**: Run `brew install git git-lfs && git lfs install`
  - **Linux (Ubuntu/Debian)**: Run `sudo apt install git git-lfs && git lfs install`
- Install **Unity Hub**
- Install **Unity Editor** version **6000.1.1f1** via Unity Hub

## Setup Steps
1. Clone the repository:
   ```bash
   git clone git@github.com:RomanSlack/Hlynr_Unity_Environment_V2.git
   cd Hlynr_Unity_Environment_V2
   ```
2. Open **Unity Hub → Projects → Add → Add project from disk** → select the cloned folder.
3. Choose **Editor version 6000.1.1f1** (install if prompted).
4. Daily workflow:
   ```bash
   git pull
   git checkout -b feature/<name>
   # Make Unity edits, save
   git add -A
   git commit -m "Change description"
   git push -u origin feature/<name>
   ```
5. Open a **Pull Request on GitHub** and merge to `main`.

## Notes
- Do not commit `Library/`, `Temp/`, `Logs/`, or `Builds/` directories.
- Always commit `.meta` files.
- Large assets are automatically handled by **Git LFS**.
