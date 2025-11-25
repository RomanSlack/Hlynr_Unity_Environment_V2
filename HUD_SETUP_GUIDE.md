# Professional HUD Setup Guide

This guide will walk you through setting up the new professional HUD system with Picture-in-Picture cameras and help modal.

## Overview of New Features

1. **Clean HUD Layout** - Minimal on-screen clutter, telemetry only shown when interceptor is active
2. **Help Modal** - Press `H` or `?` to toggle a comprehensive controls overlay
3. **Picture-in-Picture Cameras** - Two mini camera views (interceptor POV and threat POV) in bottom-right corner

---

## Step 1: Create RenderTexture Assets

You need two RenderTexture assets for the PiP cameras.

### Create Interceptor RenderTexture
1. In Unity Project window, navigate to `Assets/`
2. Right-click → Create → Render Texture
3. Name it `InterceptorPiPTexture`
4. Select it and configure in Inspector:
   - **Size**: 512 x 288 (16:9 aspect ratio)
   - **Depth Buffer**: 16 bit or 24 bit
   - **Anti-aliasing**: 2x or 4x (optional, for better quality)

### Create Threat RenderTexture
1. Right-click → Create → Render Texture
2. Name it `ThreatPiPTexture`
3. Configure same as above: 512 x 288, 16-bit depth

---

## Step 2: Setup PiP Camera GameObjects

You need to create two camera GameObjects in your scene.

**IMPORTANT**: Before proceeding, make sure Unity has finished compiling the scripts. Look at the bottom-right of Unity Editor - wait until the spinning icon disappears and there are no compilation errors in the Console.

### Create Interceptor PiP Camera
1. In Hierarchy, right-click → Create Empty
2. Name it `InterceptorPiPCamera`
3. Add Component → Camera
4. Configure the Camera component:
   - **Clear Flags**: Skybox (or Solid Color if you prefer)
   - **Culling Mask**: Default (or customize to show only certain layers)
   - **Field of View**: 60 (adjust for wider/narrower view)
   - **Output → Output Texture**: Drag `InterceptorPiPTexture` here
     - *Note*: In Unity 2022+, this is called "Output Texture" not "Target Texture"
     - Look for the **Output** section in Camera inspector and expand it
   - **Priority (or Depth)**: 1 (higher than main camera to render after it)
5. Add Component → Search for `PiPCameraController` (or browse: Simulation → UI → PiP Camera Controller)
   - *If you don't see it*: Check Console for compilation errors, fix them, and wait for Unity to recompile
6. In PiPCameraController component:
   - **Pip Camera**: Should auto-assign (or drag the Camera component)
   - **Render Texture**: Drag `InterceptorPiPTexture` here
   - **Local Offset**: Try `(0, 0.5, -2)` - this positions camera behind and above the missile
   - **Look At Offset**: Try `(0, 0, 10)` - camera looks ahead of the missile
   - **Smoothing**: 0.7 (adjust for smoother/snappier following)

### Create Threat PiP Camera
1. Repeat the above process
2. Name it `ThreatPiPCamera`
3. Use `ThreatPiPTexture` for Target Texture
4. Add PiPCameraController component
5. Configure with same offsets (or customize for different view angle)

---

## Step 3: Setup Help Modal UI

### Create Help Modal UIDocument GameObject
1. In Hierarchy, find your existing Canvas or UI root (or create one)
2. Right-click → UI Toolkit → UI Document
3. Name it `HelpModal`
4. In the UIDocument component:
   - **Source Asset**: Drag `Assets/UI/HelpModal.uxml` here
5. Add Component → Scripts → UI → **Help Modal Controller**
6. In HelpModalController component:
   - **Help Document**: Drag the UIDocument component (should auto-assign)
   - **Toggle Key**: Slash (this is the `?` key with Shift)
   - **Alternative Key**: H

---

## Step 4: Update Main HUD

### Update Existing MissileHUD UIDocument
1. Find your existing HUD UIDocument GameObject in the scene
2. Select it and verify the UIDocument component uses `Assets/UI/MissileHUD.uxml`
3. The UXML file has been updated automatically with the new layout

### Configure MissileHUDController References
1. Select the MissileHUD GameObject
2. In the **Missile HUD Controller** component:
   - **Hud Document**: Should already be assigned (the UIDocument component)
   - **Interceptor Camera**: Drag the `InterceptorPiPCamera` GameObject here
   - **Threat Camera**: Drag the `ThreatPiPCamera` GameObject here

---

## Step 5: Verify Everything is Connected

### Checklist
- [ ] Two RenderTexture assets created (InterceptorPiPTexture, ThreatPiPTexture)
- [ ] Two Camera GameObjects with PiPCameraController components
- [ ] PiP cameras have RenderTextures assigned in both Camera and PiPCameraController
- [ ] Help Modal UIDocument GameObject with HelpModalController
- [ ] HelpModal.uxml assigned to Help Modal's UIDocument
- [ ] MissileHUD UIDocument uses updated MissileHUD.uxml
- [ ] MissileHUDController has both PiP cameras assigned

---

## Step 6: Test in Play Mode

### Important: Where to Look

- **PiP cameras are NOT visible in Scene view** - they only render to the Game view HUD
- The camera GameObjects in Hierarchy are just invisible controllers
- **You must look at the Game window** to see the PiP feeds

### Testing Steps

1. **Press Play** in Unity
2. **Initial State**: Look at **Game window** (not Scene view)
   - You should see a clean HUD with just "HLYNR INTERCEPT" in top-left
   - "Press H for help" hint below it
   - Status shows "SIM READY"
3. **Press H or ?**: Help modal should appear as a centered overlay
4. **Press H/? or ESC**: Help modal should close
5. **Wait for threat to spawn** (red missile appears in scene)
6. **Press I** to launch interceptor
7. **Expected Results in Game Window**:
   - Telemetry panel appears in **bottom-left** with Speed/Fuel/Lock/Miss data
   - Two PiP camera views appear in **bottom-right corner**:
     - Top feed: "INTERCEPTOR VIEW" (blue border) - camera behind blue missile
     - Bottom feed: "THREAT VIEW" (red border) - camera behind red missile
   - PiP cameras should follow their targets smoothly as missiles fly
   - You can watch the pursuit from multiple angles at once!

---

## Quick Visual Reference

### Camera Inspector Setup (Unity 2022+)
```
Camera Component:
├─ Projection: Perspective
├─ Field of View: 60
├─ Clipping Planes: 0.3 to 1000
├─ [Scroll down...]
└─ Output (section)
    └─ Output Texture: <-- Drag RenderTexture here!
```

### What You'll See in Game View

```
┌─────────────────────────────────────────────┐
│ HLYNR INTERCEPT              SIM READY      │ ← Top bar (always visible)
│ Press H for help                            │
│                                             │
│        [Main 3D view of simulation]         │
│                                             │
│                            ┌──────────────┐ │
│ INTERCEPTOR                │ INTERCEPTOR  │ │ ← PiP feeds appear
│ Speed: 245 m/s             │ VIEW (blue)  │ │   in bottom-right
│ Fuel: 12.3 kg              └──────────────┘ │   when missile launches
│ Lock: YES                  ┌──────────────┐ │
│ Miss: 23.4 m               │ THREAT VIEW  │ │
└────────────────────────────│ (red)        │─┘
                             └──────────────┘
```

---

## Customization Tips

### Adjust PiP Camera Angles
Edit the **Local Offset** in PiPCameraController:
- `(0, 0.5, -2)` = behind and slightly above
- `(2, 0, -2)` = behind and to the right
- `(0, 1, -3)` = further back and higher (chase cam style)

Edit **Look At Offset** to aim camera:
- `(0, 0, 10)` = look ahead down the flight path
- `(0, 0, 0)` = look at the missile center
- `(0, -2, 5)` = look ahead and slightly down

### Change PiP Display Size
Edit `Assets/UI/MissileHUD.uxml`:
- Find `<ui:VisualElement name="interceptor-camera-view">`
- Change `width: 240px; height: 135px;` to your preferred size
- Maintain 16:9 aspect ratio (e.g., 320x180, 400x225)

### Customize Help Modal Content
Edit `Assets/UI/HelpModal.uxml` to add/remove/modify controls listed in the help screen.

### Change Colors/Styling
All colors are inline in the UXML files:
- **HUD accent color**: `rgb(0, 180, 255)` - cyan/blue theme
- **Interceptor color**: `rgb(0, 100, 255)` - blue
- **Threat color**: `rgb(255, 50, 50)` - red
- **Success color**: `rgb(0, 255, 150)` - green

Search and replace these RGB values in the UXML files to theme your HUD.

---

## Troubleshooting

### Scripts don't appear in Add Component menu
- **Check Console**: Look for compilation errors (red text in the Console window)
- **Wait for compilation**: Bottom-right corner of Unity should show spinning icon while "Compiling", wait for it to finish
- **Clear Console and Recompile**: Menu → Assets → Reimport All (this forces Unity to recompile everything)
- **Search by name**: In Add Component, just type `PiPCameraController` directly instead of browsing menus
- **Restart Unity**: Sometimes Unity needs a restart to recognize new scripts properly
- **Check file locations**: Make sure scripts are in `Assets/Scripts/UI/` folder

### Compilation errors about "cannot convert RenderTexture to Background"
- This was fixed in the updated code - make sure you're using the latest version of MissileHUDController.cs
- The fix uses `Background.FromRenderTexture(rt)` instead of `new StyleBackground(rt)`

### I don't see "Target Texture" in Camera inspector
- **In Unity 2022+, it's called "Output Texture"** not "Target Texture"
- Scroll down in the Camera component to find the **Output** section
- Expand it and you'll see "Output Texture" field
- Drag your RenderTexture asset there

### PiP camera feeds don't appear in Game view
- Make sure you're looking at the **Game window**, not Scene view
- PiP feeds only appear **after you press 'I'** to launch the interceptor
- Check that MissileHUDController has the two PiP camera GameObjects assigned in Inspector
- Verify both cameras have Output Texture assigned

### PiP cameras show nothing/black screen
- Check that RenderTexture is assigned in **both places**:
  1. Camera component → Output → Output Texture
  2. PiPCameraController component → Render Texture
- Verify cameras have proper Culling Mask (not set to "Nothing")
- Check that camera GameObject is active in Hierarchy

### Help modal doesn't appear
- Verify HelpModal.uxml is assigned to UIDocument
- Check that HelpModalController is attached to the same GameObject
- Look in Console for errors during Awake()

### Telemetry panel doesn't update
- Ensure MissileHUDController.AttachMissile() is being called when interceptor launches
- Check that the InterceptorSpawner or MissileLauncher calls this method
- Verify missile has all required components (Missile6DOFController, Rigidbody, FuelSystem, SeekerSensor)

### PiP cameras are too jerky/laggy
- Decrease the **Smoothing** value in PiPCameraController (try 0.3 or 0.0 for instant follow)
- Consider using FixedUpdate or LateUpdate timing

---

## Architecture Summary

```
Scene Hierarchy:
├── Main Camera (your existing free-fly camera)
├── MissileHUD (UIDocument)
│   ├── UIDocument → MissileHUD.uxml
│   └── MissileHUDController
│       ├── hudDocument → (self)
│       ├── interceptorCamera → InterceptorPiPCamera
│       └── threatCamera → ThreatPiPCamera
├── HelpModal (UIDocument)
│   ├── UIDocument → HelpModal.uxml
│   └── HelpModalController
├── InterceptorPiPCamera
│   ├── Camera → targetTexture: InterceptorPiPTexture
│   └── PiPCameraController
│       └── renderTexture: InterceptorPiPTexture
└── ThreatPiPCamera
    ├── Camera → targetTexture: ThreatPiPTexture
    └── PiPCameraController
        └── renderTexture: ThreatPiPTexture
```

---

## Next Steps

Once everything is working, consider:
- Adding audio feedback when toggling help modal
- Creating a settings panel to adjust PiP camera angles at runtime
- Adding more telemetry data (altitude, acceleration, time to impact)
- Creating preset camera angles accessible via number keys
- Adding minimap or trajectory prediction overlay

Enjoy your professional-grade HUD system!
