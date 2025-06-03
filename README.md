# ✈️ Airplane Companion

A strategic desktop companion featuring bombing aircraft that respond to user clicks. Watch as bombers spawn from screen edges, fly to clicked targets, execute bombing runs, and maintain holding patterns for multiple strikes.

![Version](https://img.shields.io/badge/version-1.0.0-blue.svg)
![.NET](https://img.shields.io/badge/.NET-6.0--windows-purple.svg)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg)

## ✨ Features

### 🎯 Strategic Bombing System
- **Click-to-Spawn**: One bomber per click with configurable cooldown (2.5s default)
- **Opposite-Edge Spawning**: Aircraft spawn from screen edge opposite to click location
- **7 Distinct Behavioral States**: Spawning, Flying, Targeting, Bombing, Exploding, HoldingPattern, Escaping
- **3-Bomb Mission System**: Each plane carries 3 bombs before retreating

### 🛩️ Advanced Flight Patterns
- **Two-Phase Approach**: Fly to screen center, then redirect to target
- **Circular Holding Patterns**: Perfect circular orbits around target areas (top-down view)
- **Multiple Bombing Runs**: Aircraft return for additional strikes until ammunition exhausted
- **Smart Escape Routes**: Planes exit via nearest screen edge when out of bombs

### 🎮 Interactive Features
- **Global Mouse Tracking**: Responds to clicks anywhere on screen
- **Multi-Aircraft Support**: Unlimited simultaneous bombers
- **Spawn Cooldown Protection**: Prevents click-spam with configurable timer
- **Multi-Monitor Support**: Works seamlessly across multiple displays

### 🎨 Visual System
- **Sprite Animation**: 8-frame flying animation + 12-frame explosion sequence
- **Direction-Aware Graphics**: Aircraft sprites flip based on flight direction
- **Explosion Effects**: Dynamic bomb detonations with fade animations
- **Transparent Overlay**: Fully transparent, click-through window

### ⚙️ Technical Implementation
- **WPF Framework**: Modern Windows desktop application
- **60 FPS Animation**: Smooth movement with 16ms frame timing
- **State Machine Architecture**: Clean behavioral state management
- **Instance Management**: Individual aircraft with independent state tracking

## 🚀 Getting Started

### Prerequisites
- Windows 10/11
- .NET 6.0 Runtime (Windows)
- Graphics folder with airplane and explosion sprites

### Building from Source
```bash
# Navigate to project directory
cd airplanes

# Build the project
dotnet build AirplaneCompanion.csproj

# Run the application
dotnet run
```

### Installation
1. Build the project or download compiled executable
2. Ensure Graphics folder contains required sprite files
3. Run `AirplaneCompanion.exe`
4. Click anywhere to spawn bombing aircraft!

## 🎯 Usage

### Basic Interaction
- **Left/Right Click**: Spawns new bomber targeting click location
- **Cooldown System**: 2.5 second delay between valid spawns
- **System Tray**: Right-click airplane icon for status and options

### Flight Behavior
1. **Spawning**: Aircraft appears at screen edge opposite to target
2. **Flying**: Moves toward screen center (halfway point)
3. **Targeting**: Redirects toward clicked location
4. **Bombing**: Drops bomb when close to target
5. **Exploding**: Shows explosion animation at target location
6. **Holding Pattern**: Circular orbit around target if bombs remain
7. **Escaping**: Flies to nearest edge when ammunition exhausted

### Mission System
- Each bomber carries **3 bombs total**
- After each bomb drop, plane enters **circular holding pattern**
- Completes 1 full orbit before next bombing run
- When ammunition exhausted, plane **escapes to nearest edge**

## 🏗️ Technical Architecture

### State Machine
```
Spawning → Flying → Targeting → Bombing → Exploding → HoldingPattern → Targeting
                                           ↓ (if bombs = 0)
                                        Escaping → Cleanup
```

### Key Components
- **AirplaneInstance**: Individual aircraft with ammunition, position, and state
- **Multi-Aircraft Management**: Concurrent planes with independent lifecycles
- **Circular Motion Mathematics**: Perfect top-down orbital patterns
- **Spawn Logic**: Opposite-edge determination based on click X coordinate

### File Structure
```
AirplaneCompanion/
├── Graphics/                   # Sprite animation files
│   ├── plane_fly_*.png        # Flying animation (8 frames)
│   └── bomb_explode_*.png     # Explosion animation (12 frames)
├── airplane_app.cs            # Main application logic
├── MainWindow.xaml            # UI layout
├── AirplaneCompanion.csproj   # Project configuration
└── create_placeholder_graphics.py # Sprite generation script
```

## 🎨 Asset Requirements for Production

### Airplane Sprites (Replace Placeholders)
- **Filename Pattern**: `plane_fly_01.png` through `plane_fly_08.png`
- **Dimensions**: 32x24 pixels recommended
- **Format**: PNG with transparency
- **Style**: Top-down view bomber aircraft
- **Animation**: 8-frame flying sequence (propeller rotation, wing flex, etc.)

### Explosion Sprites (Replace Placeholders)
- **Filename Pattern**: `bomb_explode_01.png` through `bomb_explode_12.png`
- **Dimensions**: 64x64 pixels recommended
- **Format**: PNG with transparency
- **Style**: Bomb explosion effect
- **Animation**: 12-frame explosion sequence (impact → fireball → smoke dissipation)

### Art Direction Guidelines
- **Perspective**: Top-down aerial view (aircraft viewed from above)
- **Style**: Military/strategic theme or cartoon style
- **Colors**: High contrast for visibility against varied desktop backgrounds
- **Animation**: Smooth frame transitions for fluid movement

## 🔧 Configuration

### Default Settings
- **Spawn Cooldown**: 2.5 seconds between clicks
- **Flight Speeds**: Flying (0.03), Targeting (0.025), Bombing (0.02), Escape (0.04)
- **Bombs Per Aircraft**: 3 bombs maximum
- **Holding Pattern Radius**: 120-200px (randomized per plane)

### Settings Storage
Configuration saved to: `%APPDATA%/AirplaneCompanion/settings.json`

## 🐛 Development Notes

### Current Implementation Status
- ✅ Core flight mechanics implemented
- ✅ State machine architecture complete
- ✅ Multi-aircraft support functional
- ✅ Circular holding patterns working
- ✅ Ammunition system operational
- ✅ Placeholder graphics generated
- ❓ Production graphics needed
- ❓ Sound effects (future enhancement)

### Known Considerations
- Placeholder graphics are basic geometric shapes
- Production sprites should match game theme and style
- Performance tested with 10+ concurrent aircraft
- Escape routes optimize for nearest edge

## 📋 Handoff Information

### For Graphics Team
Replace placeholder sprites in `/Graphics/` folder with production assets following the naming convention and specifications above. Each sprite should be optimized for the specified dimensions and maintain visual consistency.

### For Development Team
- Core architecture is complete and functional
- State machine handles all flight behaviors
- Sprite loading system supports easy asset swapping
- Configuration system ready for additional settings
- Multi-monitor support is built-in

### Next Steps
1. **Asset Creation**: Replace placeholder graphics with production sprites
2. **Testing**: Verify new graphics load correctly
3. **Polish**: Add sound effects and particle systems
4. **Distribution**: Package for deployment

---

**Ready for strategic bombing runs! ✈️💣**

*Built upon the SharkFin Companion architecture with enhanced multi-entity management and strategic gameplay mechanics.*
