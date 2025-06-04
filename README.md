# ✈️ Airplane Companion

A strategic desktop air combat simulation featuring bombing aircraft, defensive fighters, and anti-air towers. Control enemy bombers with clicks, deploy Defense Wings with formations, and build tower defenses in this real-time strategy overlay.

![Version](https://img.shields.io/badge/version-2.0.0-blue.svg)
![.NET](https://img.shields.io/badge/.NET-6.0--windows-purple.svg)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg)

## ✨ Features

### 🎯 Strategic Bombing System
- **Click-to-Spawn**: One bomber per click with configurable cooldown (2.5s default)
- **Opposite-Edge Spawning**: Aircraft spawn from screen edge opposite to click location
- **7 Distinct Behavioral States**: Spawning, Flying, Targeting, Bombing, Exploding, HoldingPattern, Escaping
- **3-Bomb Mission System**: Each plane carries 3 bombs before retreating

### 🛡️ Defense Wing System
- **Formation Flying**: Deploy 3-aircraft V-formations for air defense
- **Strategic Cost**: 1000 points required to deploy Defense Wing
- **User-Controlled Patrols**: Defense Wing circles around clicked location
- **Dynamic Center Updates**: Shift+LMB while active smoothly relocates patrol center
- **120-Second Fuel Limit**: Auto-despawn after 2 minutes of operation
- **Air-to-Air Combat**: Engages enemy bombers with homing missiles
- **One Wing Limit**: Only one Defense Wing can be active at a time
- **Intelligent Targeting**: Automatically prioritizes nearest enemy aircraft

### 🗼 Tower Defense System
- **Anti-Air Placement**: Deploy up to 4 defensive towers
- **Automatic Targeting**: Towers track and engage aircraft within 512px range
- **Flak Projectiles**: Visual projectiles with perfect accuracy
- **Strategic Management**: Oldest tower replaced when limit exceeded
- **Continuous Defense**: Towers provide persistent area denial

### 💰 Scoring & Economics
- **Kill Rewards**: Earn 10-40 points per destroyed aircraft
- **Bomb Bonus**: Extra points for planes with remaining ammunition
- **Defense Investment**: Spend 1000 points to deploy Defense Wings
- **Strategic Resource Management**: Balance offense vs defense spending

### 🛩️ Advanced Flight Patterns
- **Two-Phase Approach**: Fly to screen center, then redirect to target
- **Circular Holding Patterns**: Perfect circular orbits around target areas (top-down view)
- **Multiple Bombing Runs**: Aircraft return for additional strikes until ammunition exhausted
- **Smart Escape Routes**: Planes exit via nearest screen edge when out of bombs
- **Formation Dynamics**: Defense Wing maintains coordinated V-formation flight

### 🎮 Interactive Features
- **Multi-Input Controls**: LMB, Ctrl+LMB, Shift+LMB for different functions
- **Global Mouse Tracking**: Responds to clicks anywhere on screen
- **Multi-Aircraft Support**: Unlimited simultaneous bombers + Defense Wing
- **Spawn Cooldown Protection**: Prevents click-spam with configurable timer
- **Multi-Monitor Support**: Works seamlessly across multiple displays
- **Real-Time Strategy**: Balance offensive and defensive capabilities

### 🎨 Visual System
- **Sprite Animation**: 8-frame flying animation + 12-frame explosion sequence
- **Direction-Aware Graphics**: Aircraft sprites flip based on flight direction
- **Color-Coded Forces**: RGB-inverted sprites distinguish Defense Wing aircraft
- **Explosion Effects**: Dynamic bomb detonations with fade animations
- **Projectile Trails**: Visual missiles and flak rounds
- **Transparent Overlay**: Fully transparent, click-through window

### ⚙️ Technical Implementation
- **WPF Framework**: Modern Windows desktop application
- **60 FPS Animation**: Smooth movement with 16ms frame timing
- **Dual State Machines**: Separate behavior systems for bombers and fighters
- **Formation Management**: Coordinated multi-aircraft movement
- **Instance Management**: Individual aircraft with independent state tracking
- **Collision Detection**: Precise hit detection for projectiles
- **Smooth Transitions**: Interpolated movement for Defense Wing center updates

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

### 🎮 Controls
- **Left/Right Click**: Spawns bomber aircraft targeting click location
- **Ctrl + Left Click**: Places anti-air tower (max 4, replaces oldest when full)
- **Shift + Left Click**: Deploys Defense Wing formation (costs 1000 points)
  - If Defense Wing active: Relocates patrol center to new clicked location
- **System Tray**: Right-click airplane icon for status and options

### ✈️ Bomber Flight Behavior
1. **Spawning**: Aircraft appears at screen edge opposite to target
2. **Flying**: Moves toward screen center (halfway point)
3. **Targeting**: Redirects toward clicked location
4. **Bombing**: Drops bomb when close to target
5. **Exploding**: Shows explosion animation at target location
6. **Holding Pattern**: Circular orbit around target if bombs remain
7. **Escaping**: Flies to nearest edge when ammunition exhausted

### 🛡️ Defense Wing Behavior
1. **Patrolling**: V-formation circles around user-defined center point
2. **Engaging**: Individual aircraft pursue nearest enemy targets
3. **Firing**: Launch homing missiles at enemies within 512px range
4. **Refueling**: Return to screen edge when fuel depleted (120 seconds)

### 🗼 Tower Defense
- **Automatic Operation**: Towers engage any aircraft within 512px range
- **Target Prioritization**: Focus on nearest threats
- **Perfect Accuracy**: Flak projectiles have 100% hit rate
- **Strategic Placement**: Position towers to create overlapping coverage zones

### 💰 Economy & Scoring
- **Bomber Destruction**: 10 base points + 10 per remaining bomb (10-40 total)
- **Defense Wing Cost**: 1000 points required for deployment
- **Resource Management**: Balance offensive spawning vs defensive investment

### 🎯 Mission Systems
#### Bomber Missions
- Each bomber carries **3 bombs total**
- After each bomb drop, plane enters **circular holding pattern**
- Completes 1 full orbit before next bombing run
- When ammunition exhausted, plane **escapes to nearest edge**

#### Defense Wing Missions
- **120-second fuel limit** per deployment
- Automatically engages all enemy aircraft in range
- **Smooth patrol center transitions** when relocated via Shift+LMB
- Formation maintains cohesion during combat operations

## 🏗️ Technical Architecture

### Bomber State Machine
```
Spawning → Flying → Targeting → Bombing → Exploding → HoldingPattern → Targeting
                                           ↓ (if bombs = 0)
                                        Escaping → Cleanup
```

### Defense Wing State Machine
```
Patrolling → Engaging → Firing → Patrolling
     ↓         ↓         ↓         ↓
  (target)  (in range) (cooldown) (fuel low)
     ↓         ↓         ↓         ↓
 Engaging → Firing → Patrolling → Refueling → Cleanup
```

### Key Components
- **AirplaneInstance**: Individual bombers with ammunition, position, and behavioral state
- **DefenseWingInstance**: Fighter aircraft with formation positions and combat capabilities
- **DefenseWingFormation**: Manages 3-aircraft V-formation with smooth center transitions
- **AntiAirTower**: Stationary defense platforms with automatic targeting systems
- **FlakProjectile**: Anti-air projectiles with homing behavior
- **DefenseWingProjectile**: Air-to-air missiles with target tracking
- **Multi-Aircraft Management**: Concurrent aircraft with independent lifecycles
- **Formation Mathematics**: Coordinated movement with relative positioning
- **Circular Motion Mathematics**: Perfect top-down orbital patterns
- **Spawn Logic**: Opposite-edge determination based on click X coordinate
- **Collision Detection**: Precise hit detection for all projectile types
- **Smooth Interpolation**: Center transition system for Defense Wing relocations

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

## 📝 Recent Updates & Changelog

### Version 2.0.0 - Major Defense Expansion
#### 🛡️ Defense Wing System
- **Added V-Formation Flying**: 3-aircraft Defense Wing formations
- **User-Controlled Patrols**: Defense Wings circle around clicked locations
- **Dynamic Center Updates**: Shift+LMB relocates patrol center smoothly
- **120-Second Fuel System**: Auto-despawn after fuel depletion
- **Air-to-Air Combat**: Homing missiles engage enemy bombers
- **Strategic Economics**: 1000-point cost for Defense Wing deployment

#### 🗼 Tower Defense System  
- **Anti-Air Towers**: Place up to 4 defensive towers with Ctrl+LMB
- **Automatic Targeting**: Towers track and engage aircraft within range
- **Flak Projectiles**: Visual projectiles with perfect accuracy
- **Strategic Management**: Oldest tower replaced when limit exceeded

#### 💰 Scoring & Economics
- **Dynamic Scoring**: 10-40 points per aircraft based on remaining bombs
- **Resource Management**: Balance offensive spawning vs defensive investment
- **Economic Strategy**: Earn points through destruction, spend on defense

#### 🔧 Critical Bug Fixes
- **Fixed Defense Wing Extreme Speed Bug**: Resolved movement conflicts causing aircraft to fly off-screen at impossible speeds
- **Improved Formation Logic**: Separated position setting from movement behavior to prevent feedback loops  
- **Enhanced Patrol System**: Smooth circular patterns around user-defined centers
- **Optimized Center Transitions**: Seamless movement between patrol locations without teleportation

## 🐛 Development Notes

### Current Implementation Status
- ✅ Core flight mechanics implemented
- ✅ State machine architecture complete
- ✅ Multi-aircraft support functional
- ✅ Circular holding patterns working
- ✅ Ammunition system operational
- ✅ Defense Wing formation system complete
- ✅ Tower defense system functional
- ✅ Scoring and economics implemented
- ✅ Critical movement bugs resolved
- ✅ Placeholder graphics generated
- ❓ Production graphics needed
- ❓ Sound effects (future enhancement)

### Known Considerations
- Placeholder graphics are basic geometric shapes for all aircraft types
- Production sprites should distinguish bombers vs fighters visually
- Performance tested with 10+ concurrent aircraft + Defense Wing + towers
- All escape routes optimize for nearest screen edge
- Defense Wing movement system completely rewritten for stability

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
