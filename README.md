# Hlynr Intercept Unity Environment

A high-fidelity missile defense simulation environment built in Unity for developing and testing interceptor control systems and reinforcement learning algorithms.

![Unity](https://img.shields.io/badge/Unity-2022.3+-000000?style=flat&logo=unity)
![C#](https://img.shields.io/badge/C%23-10.0-239120?style=flat&logo=csharp)
![Physics](https://img.shields.io/badge/Physics-6DOF-blue?style=flat)
![AI Ready](https://img.shields.io/badge/AI-Ready-green?style=flat)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Mac%20%7C%20Linux-lightgrey?style=flat)

## Overview

Hlynr Intercept is a sophisticated missile interception simulation that provides realistic physics-based modeling of interceptor missiles engaging incoming threats. The environment features full 6-degree-of-freedom (6DOF) physics, proportional navigation guidance, and comprehensive telemetry logging, making it ideal for developing advanced control algorithms and training reinforcement learning agents.

## Key Features

### Physics & Dynamics
- **6DOF Flight Model**: Complete rigid body dynamics with torque-based attitude control
- **PID Control System**: Closed-loop attitude stabilization with configurable gains
- **Thrust Modeling**: Realistic propulsion with thrust curves and fuel consumption
- **Aerodynamic Forces**: Drag and lift calculations for atmospheric flight

### Guidance & Control
- **Proportional Navigation**: Industry-standard PN guidance law implementation
- **Seeker Sensor**: Target acquisition with field-of-view and range constraints
- **Proximity Fuse**: Configurable detonation radius for target intercept
- **Multi-mode Threats**: Support for both ballistic and maneuvering targets

### Simulation Infrastructure
- **Telemetry System**: CSV-based data logging for analysis and replay
- **HUD Display**: Real-time visualization of missile state and guidance parameters
- **Configurable Scenarios**: ScriptableObject-based configuration system
- **Force Visualization**: Debug rendering of applied forces and torques

### AI/ML Integration
- **RL-Ready Architecture**: Modular design supporting Python backend integration
- **JSON Communication Protocol**: Structured data exchange for external controllers
- **State Space Access**: Complete observability of simulation state
- **Control Interface**: Direct access to attitude and thrust commands

## Project Structure

```
Assets/
├── Scripts/
│   ├── Core/               # Simulation bootstrap and camera control
│   ├── Missile/            # Interceptor dynamics and control systems
│   ├── Entities/           # Threat spawning and behavior
│   ├── Config/             # Settings and configuration
│   ├── Analytics/          # Telemetry and visualization
│   └── UI/                 # HUD and user interface
├── Prefabs/                # Missile, threat, and effect prefabs
├── Materials/              # Shaders and visual materials
├── Scenes/                 # Main simulation scene
└── ScriptableObjects/      # Configuration assets
```

## Core Components

### Missile Systems
- `Missile6DOFController`: Applies torques and forces for attitude control
- `PIDAttitudeController`: Converts rate commands to control torques
- `GuidanceProNav`: Implements proportional navigation guidance
- `SeekerSensor`: Handles target tracking and lock management
- `ThrustModel`: Manages propulsion and thrust vectoring
- `FuelSystem`: Tracks fuel consumption and mass changes

### Threat Systems
- `ThreatSpawner`: Generates incoming threats at configured intervals
- `ThreatGuidance`: Simple guidance for legacy threat model
- `ThreatRocketController`: Advanced 6DOF threat with realistic dynamics

### Support Systems
- `TelemetryLogger`: Records simulation data to CSV files
- `MissileHUDController`: Displays real-time missile and target information
- `GameSimSettings`: Central configuration for simulation parameters

## Getting Started

### Requirements
- Unity 2022.3 LTS or newer
- Universal Render Pipeline (URP)
- Input System package
- Mathematics package

### Running the Simulation
1. Open the project in Unity
2. Load the `SampleScene` from Assets/Scenes/
3. Press Play to start the simulation
4. Press 'I' to launch an interceptor when a threat is present

### Controls
- **I**: Launch interceptor missile
- **P**: Pause/unpause simulation
- **R**: Reset scene
- **Mouse**: Camera rotation (hold right-click)
- **WASD**: Camera movement
- **Shift/Ctrl**: Camera altitude adjustment

## Configuration

The simulation behavior can be customized through the `GameSimSettings` ScriptableObject:
- Threat spawn parameters
- Defended target position
- Threat behavior modes (ballistic vs maneuvering)
- Physics timestep settings
- Telemetry output configuration

## AI Integration

The architecture supports integration with external AI/ML systems through:
1. Modular control interfaces allowing guidance system replacement
2. Comprehensive state observation through telemetry system
3. Documented feasibility for Python RL backend integration
4. Clean separation between physics simulation and control logic

See `Assets/Scripts/Unity_RL_API_Integration_Feasibility.md` for detailed integration guidelines.

## Development Roadmap

- Phase 1: Core simulation and physics (Complete)
- Phase 2: Python RL API integration layer
- Phase 3: Multi-agent coordination support
- Phase 4: Advanced scenario generation
- Phase 5: Performance optimization and scaling

## Technical Details

- **Fixed Timestep**: 0.01s (100Hz) physics update rate
- **Coordinate System**: Unity standard (Y-up, left-handed)
- **Units**: Metric (meters, kilograms, seconds)
- **Performance**: 60+ FPS with multiple simultaneous engagements

## License

Hippocratic License 2.1 - Copyright (c) 2025 Roman Slack

This software is provided for research and educational purposes only. Usage is restricted to applications that respect human rights and international law. The software explicitly prohibits use in:
- Lethal autonomous weapons systems
- Military or defense applications including weapon targeting
- Mass surveillance or predictive policing
- Any activities violating universal human rights

See [https://firstdonoharm.dev](https://firstdonoharm.dev) for full license details.

## Contact

For technical inquiries or collaboration opportunities, please contact the Hlynr development team.