# 🔧 Build Instructions - Airplane Companion

## 📋 Prerequisites

### System Requirements
- **Operating System**: Windows 10/11 (64-bit)
- **.NET SDK**: 6.0 or later
- **IDE**: Visual Studio 2022 OR VS Code with C# extension

### Installation Links
- **.NET 6.0 SDK**: https://dotnet.microsoft.com/download/dotnet/6.0
- **Visual Studio 2022**: https://visualstudio.microsoft.com/downloads/
- **VS Code**: https://code.visualstudio.com/

## 🚀 Quick Start

### 1. Clone Repository
```bash
git clone <repository-url>
cd airplane-companion
```

### 2. Verify Prerequisites
```bash
dotnet --version
# Should show 6.0.x or later
```

### 3. Build Project
```bash
dotnet build
```

### 4. Run Development Version
```bash
dotnet run
```

## 📦 Distribution Builds

### Lite Version (Framework-Dependent)
Creates a lightweight package requiring .NET runtime on target machine:
```bash
dotnet publish -c Release --framework net6.0-windows --output ./dist-lite
```

**Output**: `dist-lite/` folder (~25MB)  
**Requirement**: Target machine must have .NET 6.0 Desktop Runtime installed

### Full Version (Self-Contained)
Creates a portable package with complete .NET runtime included:
```bash
dotnet publish -c Release --framework net6.0-windows --runtime win-x64 --self-contained true --output ./dist-full
```

**Output**: `dist-full/` folder (~150MB)  
**Requirement**: No dependencies - runs on any Windows 10/11 machine

## 🛠️ Development Workflow

### Debug Build
```bash
dotnet build --configuration Debug
```

### Release Build
```bash
dotnet build --configuration Release
```

### Clean Build
```bash
dotnet clean
dotnet build
```

### Run with Specific Configuration
```bash
dotnet run --configuration Release
```

## 📁 Project Structure

```
airplane-companion/
├── airplane_app.cs              # Main application logic
├── MainWindow.xaml              # UI definition
├── AirplaneCompanion.csproj     # Project configuration
├── Graphics/                    # Sprite assets
│   ├── plane_fly_*.png         # Aircraft animation (8 frames)
│   └── bomb_explode_*.png       # Explosion animation (12 frames)
├── README.md                    # Project overview
├── BUILD_INSTRUCTIONS.md        # This file
├── .gitignore                   # Git exclusion rules
└── [Documentation files]       # Additional project docs
```

## 🎨 Graphics Asset Management

### Current Assets
The project includes placeholder graphics for development:
- **Aircraft**: 8-frame animation sequence (32x24 pixels)
- **Explosions**: 12-frame animation sequence (64x64 pixels)

### Replacing Graphics
To use custom sprites:
1. Replace files in `Graphics/` folder
2. Maintain original file names and numbering
3. Keep same dimensions (32x24 for aircraft, 64x64 for explosions)
4. Use PNG format with transparency support
5. Rebuild project: `dotnet build`

### Generating Placeholder Graphics
If graphics are missing, regenerate using:
```bash
python create_placeholder_graphics.py
```

## 🧪 Testing and Validation

### Build Verification
```bash
# Test build process
dotnet build --verbosity normal

# Test both distribution types
dotnet publish -c Release --framework net6.0-windows --output ./test-lite
dotnet publish -c Release --framework net6.0-windows --runtime win-x64 --self-contained true --output ./test-full
```

### Runtime Testing
1. **Development**: `dotnet run`
2. **Lite Distribution**: `./dist-lite/AirplaneCompanion.exe`
3. **Full Distribution**: `./dist-full/AirplaneCompanion.exe`

### Features to Test
- [ ] Application launches without errors
- [ ] Click anywhere on screen spawns aircraft
- [ ] Aircraft flies from opposite screen edge
- [ ] Aircraft rotates to face flight direction
- [ ] Bomb explosion plays at target location
- [ ] Multiple aircraft can be active simultaneously
- [ ] System tray icon appears and functions
- [ ] Application can be closed via system tray

## 🐛 Troubleshooting

### Common Build Issues

**"SDK not found"**
```bash
# Install .NET 6.0 SDK from Microsoft
# Restart terminal/IDE after installation
dotnet --version
```

**"Graphics files missing"**
```bash
# Regenerate placeholder graphics
python create_placeholder_graphics.py
# Or ensure Graphics/ folder contains all 20 PNG files
```

**"WPF not supported"**
```bash
# Ensure using Windows and .NET 6.0-windows framework
# Check AirplaneCompanion.csproj contains:
# <TargetFramework>net6.0-windows</TargetFramework>
# <UseWPF>true</UseWPF>
```

### Runtime Issues

**"Application doesn't start"**
- Verify Windows 10/11 (64-bit)
- Check all files extracted to same folder
- Ensure Graphics folder is present

**"Aircraft don't appear"**
- Try clicking in different screen areas
- Check multi-monitor setup
- Verify graphics files are present and valid

## 🔄 Development Cycle

### Typical Workflow
1. **Edit Code**: Modify `airplane_app.cs` or `MainWindow.xaml`
2. **Build**: `dotnet build`
3. **Test**: `dotnet run`
4. **Debug**: Use IDE debugging features
5. **Package**: `dotnet publish` for distribution

### IDE-Specific Instructions

**Visual Studio 2022:**
- Open `AirplaneCompanion.csproj`
- Use F5 to build and run
- Use Ctrl+Shift+B to build only
- Use Build → Publish for distribution

**VS Code:**
- Open project folder
- Install C# extension
- Use Ctrl+Shift+P → ".NET: Build"
- Use Ctrl+F5 to run without debugging

## 📊 Performance Notes

### Build Times
- **Debug Build**: ~5-10 seconds
- **Release Build**: ~10-15 seconds
- **Lite Publish**: ~15-30 seconds
- **Full Publish**: ~45-90 seconds

### System Resources
- **Development**: ~50MB RAM
- **Runtime**: ~50-100MB RAM depending on number of active aircraft
- **Disk Space**: Source ~5MB, Distributions 25MB/150MB

---

**Ready to build your strategic desktop air force! ✈️💥**
