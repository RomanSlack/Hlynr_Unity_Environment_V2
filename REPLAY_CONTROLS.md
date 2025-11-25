# Replay System Controls Guide

## 🎮 Playback Controls

| Key | Action |
|-----|--------|
| **SPACE** | Play / Pause |
| **R** | Restart from beginning |
| **← →** | Step backward/forward one frame (when paused) |

## ⏩ Speed Controls

| Key | Speed |
|-----|-------|
| **1** | 0.1x (Very Slow) |
| **2** | 0.25x (Slow) |
| **3** | 0.5x (Half Speed) |
| **4** | 1.0x (Normal) |
| **5** | 2.0x (Double) |
| **6** | 4.0x (Fast) |
| **[** | Decrease speed by 0.25x |
| **]** | Increase speed by 0.25x |

## 📹 Camera Controls (FlyCamera)

**Camera works independently - you can move around even when paused!**

| Key | Action |
|-----|--------|
| **W/A/S/D** | Move forward/left/back/right |
| **Q/E** | Move down/up |
| **Mouse** | Look around |
| **ESC** | Show/unlock cursor |

## 📊 On-Screen Display

The HUD shows:
- Episode ID and outcome (intercepted/miss)
- Current time / total duration
- Playback speed
- Frame count and replay mode
- Metrics: fuel used, final distance, total reward

## 🎯 Tips

- **Pause to inspect**: Hit SPACE to freeze the action and fly around with the camera
- **Slow motion analysis**: Use keys 1-3 for detailed observation
- **Frame-by-frame**: Pause, then use arrow keys to step through individual frames
- **Quick replay**: Press R to instantly restart the episode

## 📁 File Location

Place replay JSONL files in: `Assets/StreamingAssets/Replays/`

Set the file path in the ReplayDirector component's `episodePath` field.
