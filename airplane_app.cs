using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Forms;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;

namespace AirplaneCompanion
{
    // Settings configuration class
    public class AirplaneSettings
    {
        public BehaviorSettings BehaviorSettings { get; set; } = new BehaviorSettings();
        public double SpeedMultiplier { get; set; } = 1.0;
        public bool MultipleAirplanesEnabled { get; set; } = true;
        public bool AutoStartEnabled { get; set; } = false;
        public double SpawnCooldownSeconds { get; set; } = 2.5;
    }
    
    public class BehaviorSettings
    {
        public bool HoldingPatterns { get; set; } = true;
        public bool CircularOrbiting { get; set; } = true;
        public bool EscapeRoutes { get; set; } = true;
        public bool VariableFlightPaths { get; set; } = true;
    }
    
    // Airplane states for behavior tracking
    public enum AirplaneState 
    { 
        Spawning, Flying, Targeting, Bombing, ClearingArea, Exploding, ShotDown, HoldingPattern, Escaping 
    }
    
    // Defense Wing states for behavior tracking
    public enum DefenseWingState
    {
        Patrolling, Engaging, Firing, Refueling, Escaping
    }
    
    // Bomb instance for delayed explosions
    public class Bomb
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public Point DropLocation { get; set; }
        public DateTime DropTime { get; set; }
        public TimeSpan DescentDuration { get; set; } = TimeSpan.FromSeconds(2);
        public bool HasExploded { get; set; } = false;
        public Image? ExplosionImage { get; set; }
        public int CurrentExplosionFrame { get; set; } = 0;
        public bool ExplosionAnimationPlaying { get; set; } = false;
    }
    
    // Anti-air tower for defense
    public class AntiAirTower
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public Point Position { get; set; }
        public double Range { get; set; } = 512; // pixels
        public double FireRate { get; set; } = 1.0; // shots per second
        public DateTime LastShotTime { get; set; } = DateTime.MinValue;
        public DateTime LastTargetTime { get; set; } = DateTime.MinValue;
        public AirplaneInstance? CurrentTarget { get; set; }
        public Image? TowerImage { get; set; }
        public bool IsTargeting { get; set; } = false;
        
        public AntiAirTower(Point position)
        {
            Position = position;
        }
    }
    
    // Flak projectile
    public class FlakProjectile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public Point Position { get; set; }
        public Point StartPosition { get; set; }
        public Point TargetPosition { get; set; }
        public AirplaneInstance TargetAirplane { get; set; }
        public double Speed { get; set; } = 8.0; // pixels per frame
        public DateTime LaunchTime { get; set; }
        public Image? ProjectileImage { get; set; }
        public Vector Velocity { get; set; }
        public bool HasHitTarget { get; set; } = false;
        
        public FlakProjectile(Point startPos, AirplaneInstance target)
        {
            StartPosition = startPos;
            Position = startPos;
            TargetAirplane = target;
            TargetPosition = target.Position;
            LaunchTime = DateTime.Now;
            
            // Calculate velocity towards target
            var deltaX = TargetPosition.X - StartPosition.X;
            var deltaY = TargetPosition.Y - StartPosition.Y;
            var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            
            if (distance > 0)
            {
                Velocity = new Vector(
                    (deltaX / distance) * Speed,
                    (deltaY / distance) * Speed
                );
            }
        }
    }
    
    // Defense Wing aircraft instance
    public class DefenseWingInstance
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public Point Position { get; set; }
        public Point PreviousPosition { get; set; }
        public Point FormationPosition { get; set; } // Position relative to formation center
        public double FlightAngle { get; set; } = 0;
        public double TargetAngle { get; set; } = 0;
        public double CurrentSpeed { get; set; } = 0.0396; // 10% faster than normal planes (0.036 * 1.1)
        public double TargetSpeed { get; set; } = 0.0396;
        public double FiringSpeed { get; set; } = 0.0324; // 10% slower when firing (0.036 * 0.9)
        public DateTime SpawnTime { get; set; } = DateTime.Now;
        public DateTime LastStateChange { get; set; } = DateTime.Now;
        public DateTime LastShotTime { get; set; } = DateTime.MinValue;
        
        // Mission properties
        public AirplaneInstance? CurrentTarget { get; set; }
        public double FuelRemaining { get; set; } = 120.0; // 120 seconds of fuel
        public bool IsRefueling { get; set; } = false;
        
        // State management
        public DefenseWingState CurrentState { get; set; } = DefenseWingState.Patrolling;
        public int CurrentFlyFrame { get; set; } = 0;
        
        // UI components
        public Image? AirplaneImage { get; set; }
        
        public DefenseWingInstance(Point formationPos)
        {
            FormationPosition = formationPos;
            Position = formationPos;
        }
    }
    
    // Defense Wing projectile (air-to-air missile)
    public class DefenseWingProjectile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public Point Position { get; set; }
        public Point StartPosition { get; set; }
        public AirplaneInstance TargetAirplane { get; set; }
        public double Speed { get; set; } = 32.0; // 4x plane speed (8 * 4)
        public double Range { get; set; } = 1024.0; // pixels
        public DateTime LaunchTime { get; set; }
        public Image? ProjectileImage { get; set; }
        public Vector Velocity { get; set; }
        public bool HasHitTarget { get; set; } = false;
        
        public DefenseWingProjectile(Point startPos, AirplaneInstance target)
        {
            StartPosition = startPos;
            Position = startPos;
            TargetAirplane = target;
            LaunchTime = DateTime.Now;
            
            // Calculate initial velocity towards target (will be updated for homing)
            UpdateHomingVelocity();
        }
        
        public void UpdateHomingVelocity()
        {
            // Homing missile - constantly update direction towards target
            var deltaX = TargetAirplane.Position.X - Position.X;
            var deltaY = TargetAirplane.Position.Y - Position.Y;
            var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            
            if (distance > 0)
            {
                Velocity = new Vector(
                    (deltaX / distance) * Speed,
                    (deltaY / distance) * Speed
                );
            }
        }
    }
    
    // Defense Wing formation manager
    public class DefenseWingFormation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public Point CenterPosition { get; set; }
        public Point TargetCenterPosition { get; set; }
        public Point PatrolTarget { get; set; }
        public List<DefenseWingInstance> Aircraft { get; set; } = new List<DefenseWingInstance>();
        public DateTime SpawnTime { get; set; } = DateTime.Now;
        public double PatrolAngle { get; set; } = 0;
        public double PatrolRadius { get; set; } = 200;
        public bool IsActive { get; set; } = true;
        public bool IsTransitioning { get; set; } = false;
        public double TransitionSpeed { get; set; } = 0.02; // How fast to move to new center
        
        public DefenseWingFormation(Point spawnLocation)
        {
            CenterPosition = spawnLocation;
            TargetCenterPosition = spawnLocation;
            PatrolTarget = spawnLocation;
            
            // Create formation of 3 aircraft in V-formation
            CreateFormation();
        }
        
        private void CreateFormation()
        {
            // V-formation: leader in front, two wingmen behind and to the sides
            var formationSpacing = 60.0;
            
            // Leader
            Aircraft.Add(new DefenseWingInstance(new Point(0, 0)));
            
            // Left wingman
            Aircraft.Add(new DefenseWingInstance(new Point(-formationSpacing, formationSpacing)));
            
            // Right wingman
            Aircraft.Add(new DefenseWingInstance(new Point(formationSpacing, formationSpacing)));
        }
        
        public void UpdateFormationCenter(Point newCenter)
        {
            CenterPosition = newCenter;
            
            // Don't directly set aircraft positions - let them fly toward their formation positions
            // This prevents the extreme speed feedback loop
        }
        
        public Point GetAircraftFormationTarget(DefenseWingInstance aircraft)
        {
            return new Point(
                CenterPosition.X + aircraft.FormationPosition.X,
                CenterPosition.Y + aircraft.FormationPosition.Y
            );
        }
        
        public bool AllAircraftNeedRefuel()
        {
            return Aircraft.All(a => a.FuelRemaining <= 0 || a.IsRefueling);
        }
        
        public bool HasActiveAircraft()
        {
            return Aircraft.Any(a => !a.IsRefueling && a.FuelRemaining > 0);
        }
        
        public void SetNewPatrolCenter(Point newCenter)
        {
            TargetCenterPosition = newCenter;
            IsTransitioning = true;
        }
        
        public void UpdateCenterTransition()
        {
            if (!IsTransitioning) return;
            
            // Calculate distance to target center
            var deltaX = TargetCenterPosition.X - CenterPosition.X;
            var deltaY = TargetCenterPosition.Y - CenterPosition.Y;
            var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            
            // If close enough, snap to target and stop transitioning
            if (distance < 5.0)
            {
                CenterPosition = TargetCenterPosition;
                IsTransitioning = false;
                return;
            }
            
            // Move toward target center
            var moveDistance = TransitionSpeed * 60; // Scale for smooth movement
            CenterPosition = new Point(
                CenterPosition.X + (deltaX / distance) * moveDistance,
                CenterPosition.Y + (deltaY / distance) * moveDistance
            );
        }
    }
    
    // Individual airplane instance
    public class AirplaneInstance
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public Point Position { get; set; }
        public Point PreviousPosition { get; set; }
        public Point TargetLocation { get; set; }
        public Point SpawnLocation { get; set; }
        public double FlightAngle { get; set; } = 0; // Current rotation angle in degrees
        public double TargetAngle { get; set; } = 0; // Target rotation for smooth transitions
        public bool FacingRight { get; set; } = true; // Kept for backward compatibility
        public double CurrentSpeed { get; set; } = 0.03;
        public double TargetSpeed { get; set; } = 0.03;
        public DateTime SpawnTime { get; set; } = DateTime.Now;
        public DateTime LastStateChange { get; set; } = DateTime.Now;
        
        // Ammunition and mission system
        public int BombsRemaining { get; set; } = 3;
        public int TotalBombsDropped { get; set; } = 0;
        
        // Holding pattern properties
        public double HoldingPatternRadius { get; set; } = 150;
        public double HoldingPatternAngle { get; set; } = 0;
        public double OrbitDirection { get; set; } = 1; // 1 for clockwise, -1 for counter-clockwise
        public double OrbitSpeed { get; set; } = 0.03;
        public int CompletedOrbits { get; set; } = 0;
        public double OrbitStartAngle { get; set; } = 0;
        
        // State management
        public AirplaneState CurrentState { get; set; } = AirplaneState.Spawning;
        public int CurrentFlyFrame { get; set; } = 0;
        public int CurrentExplosionFrame { get; set; } = 0;
        public bool ExplosionAnimationPlaying { get; set; } = false;
        
        // UI components for this airplane
        public Image? AirplaneImage { get; set; }
        public Image? ExplosionImage { get; set; }
        
        public AirplaneInstance(Point targetLocation, Point spawnLocation)
        {
            TargetLocation = targetLocation;
            SpawnLocation = spawnLocation;
            Position = spawnLocation;
            
            // Randomize holding pattern properties
            var random = new Random();
            HoldingPatternRadius = 120 + random.NextDouble() * 80; // 120-200px radius
            OrbitDirection = random.NextDouble() > 0.5 ? 1 : -1; // Random direction
            OrbitSpeed = (0.02 + random.NextDouble() * 0.02) * 0.75; // Varied orbit speeds (-25% from new values)
        }
    }
    
    public partial class MainWindow : Window
    {
        // Win32 API imports for mouse hooks
        private const int WH_MOUSE_LL = 14;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        private const int VK_CONTROL = 0x11;
        private const int VK_SHIFT = 0x10;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        // Class members
        private IntPtr _hookID = IntPtr.Zero;
        private LowLevelMouseProc _proc = HookCallback;
        private static MainWindow _instance;
        
        private DispatcherTimer _animationTimer;
        private DateTime _lastMouseActivity;
        private DateTime _lastSpawnTime = DateTime.MinValue;
        
        private Canvas _airplaneCanvas;
        
        private Point _currentMousePos;
        private Point _lastClickPos;
        private const double DIRECTION_THRESHOLD = 1.0; // Minimum movement to trigger direction change
        
        // Speed constants (+20% increase from original values)
        private const double FLYING_SPEED = 0.036;
        private const double TARGETING_SPEED = 0.030;
        private const double BOMBING_SPEED = 0.024;
        private const double ESCAPE_SPEED = 0.048;
        
        // Smooth transition variables
        private const double SPEED_TRANSITION_RATE = 0.08; // How fast speed changes
        
        // Aircraft management
        private List<AirplaneInstance> _activeAirplanes = new List<AirplaneInstance>();
        private List<Bomb> _activeBombs = new List<Bomb>();
        private Random _flightRandom = new Random();
        
        // Tower defense system
        private List<AntiAirTower> _activeTowers = new List<AntiAirTower>();
        private List<FlakProjectile> _activeProjectiles = new List<FlakProjectile>();
        private const int MAX_TOWERS = 4;
        
        // Scoring system
        private int _playerScore = 0;
        
        // Defense Wing system
        private DefenseWingFormation? _activeDefenseWing = null;
        private List<DefenseWingProjectile> _activeDefenseProjectiles = new List<DefenseWingProjectile>();
        private const int DEFENSE_WING_COST = 1000; // Points required to spawn Defense Wing
        
        // Sprite animation system
        private List<BitmapImage> _flySprites;
        private List<BitmapImage> _explosionSprites;
        private List<BitmapImage> _defenseWingSprites;
        private int _frameCounter = 0;
        
        // System tray components
        private NotifyIcon _notifyIcon;
        private ContextMenuStrip _contextMenu;
        private AirplaneSettings _settings;
        private string _settingsPath;

        public MainWindow()
        {
            _instance = this;
            SetupWindow();
            SetupAirplaneGraphics();
            SetupTimers();
            SetupMouseHook();
            SetupSystemTray();
            LoadSettings();
        }

        private void SetupWindow()
        {
            // Make window transparent and click-through
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            
            // Cover entire virtual screen (all monitors)
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;
            
            // Don't use Maximized state for multi-monitor support
            WindowState = WindowState.Normal;
        }

        private void SetupAirplaneGraphics()
        {
            _airplaneCanvas = new Canvas();
            Content = _airplaneCanvas;
            
            // Load sprite images
            LoadSprites();
        }

        private void SetupTimers()
        {
            // Animation timer for smooth movement
            _animationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
            };
            _animationTimer.Tick += AnimationTimer_Tick;
            _animationTimer.Start();
            
            _lastMouseActivity = DateTime.Now;
        }

        private void SetupMouseHook()
        {
            _hookID = SetHook(_proc);
        }

        private IntPtr SetHook(LowLevelMouseProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_MOUSE_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                _instance?.OnMouseActivity(wParam);
            }
            return CallNextHookEx(_instance._hookID, nCode, wParam, lParam);
        }

        private void OnMouseActivity(IntPtr wParam)
        {
            _lastMouseActivity = DateTime.Now;
            
            // Get current mouse position
            GetCursorPos(out POINT point);
            _currentMousePos = new Point(point.x, point.y);
            
            // Handle click detection
            if (wParam == (IntPtr)WM_LBUTTONDOWN || wParam == (IntPtr)WM_RBUTTONDOWN)
            {
                _lastClickPos = _currentMousePos;
                
                // Check if Ctrl or Shift is pressed
                bool isCtrlPressed = (GetKeyState(VK_CONTROL) & 0x8000) != 0;
                bool isShiftPressed = (GetKeyState(VK_SHIFT) & 0x8000) != 0;
                
                if (isCtrlPressed && wParam == (IntPtr)WM_LBUTTONDOWN)
                {
                    // Ctrl+LMB: Place tower
                    PlaceTower(_lastClickPos);
                }
                else if (isShiftPressed && wParam == (IntPtr)WM_LBUTTONDOWN)
                {
                    // Shift+LMB: Spawn Defense Wing
                    SpawnDefenseWing(_lastClickPos);
                }
                else
                {
                    // Normal click: Spawn airplane (with cooldown)
                    var timeSinceLastSpawn = DateTime.Now - _lastSpawnTime;
                    if (timeSinceLastSpawn.TotalSeconds >= _settings.SpawnCooldownSeconds)
                    {
                        SpawnAirplane(_lastClickPos);
                        _lastSpawnTime = DateTime.Now;
                    }
                }
            }
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            UpdateAllAirplanes();
            UpdateActiveBombs();
            UpdateTowers();
            UpdateProjectiles();
            UpdateDefenseWing();
            UpdateDefenseProjectiles();
            UpdateSpriteAnimations();
            CleanupFinishedAirplanes();
            CleanupFinishedBombs();
            CleanupFinishedProjectiles();
            CleanupFinishedDefenseWing();
        }
        
        private void SpawnAirplane(Point targetLocation)
        {
            // Determine spawn location (opposite side based on target X)
            Point spawnLocation;
            var screenWidth = SystemParameters.VirtualScreenWidth;
            var screenHeight = SystemParameters.VirtualScreenHeight;
            
            if (targetLocation.X < screenWidth / 2)
            {
                // Target on left side, spawn from right edge
                spawnLocation = new Point(
                    screenWidth + 50, 
                    _flightRandom.NextDouble() * screenHeight
                );
            }
            else
            {
                // Target on right side, spawn from left edge
                spawnLocation = new Point(
                    -50, 
                    _flightRandom.NextDouble() * screenHeight
                );
            }
            
            // Create new airplane instance
            var airplane = new AirplaneInstance(targetLocation, spawnLocation);
            
            // Create UI elements for this airplane
            airplane.AirplaneImage = new Image
            {
                Width = 32,
                Height = 24,
                Source = _flySprites[0]
            };
            
            airplane.ExplosionImage = new Image
            {
                Width = 64,
                Height = 64,
                Source = _explosionSprites[0],
                Visibility = Visibility.Hidden
            };
            
            _airplaneCanvas.Children.Add(airplane.AirplaneImage);
            _airplaneCanvas.Children.Add(airplane.ExplosionImage);
            
            // Add to active airplanes list
            _activeAirplanes.Add(airplane);
            
            // Set initial state
            airplane.CurrentState = AirplaneState.Flying;
            
            // Update position
            UpdateAirplanePosition(airplane);
        }
        
        private void SpawnDefenseWing(Point spawnLocation)
        {
            // Check if Defense Wing already exists and is active
            if (_activeDefenseWing != null && _activeDefenseWing.IsActive)
            {
                // Update patrol center instead of spawning new Defense Wing
                _activeDefenseWing.SetNewPatrolCenter(spawnLocation);
                return;
            }
            
            // Check if player has enough points to spawn new Defense Wing
            if (_playerScore < DEFENSE_WING_COST)
            {
                // Not enough points
                return;
            }
            
            // Deduct cost from score
            _playerScore -= DEFENSE_WING_COST;
            
            // Create new Defense Wing formation
            _activeDefenseWing = new DefenseWingFormation(spawnLocation);
            
            // Create UI elements for each aircraft in the formation
            foreach (var aircraft in _activeDefenseWing.Aircraft)
            {
                // Use RGB-inverted plane sprite for Defense Wing aircraft
                aircraft.AirplaneImage = new Image
                {
                    Width = 32,
                    Height = 24,
                    Source = _defenseWingSprites[0]
                };
                
                _airplaneCanvas.Children.Add(aircraft.AirplaneImage);
                
                // Set initial position
                aircraft.Position = new Point(
                    spawnLocation.X + aircraft.FormationPosition.X,
                    spawnLocation.Y + aircraft.FormationPosition.Y
                );
                
                UpdateDefenseWingPosition(aircraft);
            }
        }
        
        private BitmapImage CreateDefenseWingPlaceholderBitmap()
        {
            var drawingVisual = new DrawingVisual();
            using (DrawingContext drawingContext = drawingVisual.RenderOpen())
            {
                // Draw blue diamond shape for Defense Wing aircraft
                var points = new Point[]
                {
                    new Point(12, 2),  // Top
                    new Point(22, 9),  // Right
                    new Point(12, 16), // Bottom
                    new Point(2, 9)    // Left
                };
                
                var geometry = new PathGeometry();
                var figure = new PathFigure { StartPoint = points[0] };
                
                for (int i = 1; i < points.Length; i++)
                {
                    figure.Segments.Add(new LineSegment(points[i], true));
                }
                figure.IsClosed = true;
                geometry.Figures.Add(figure);
                
                drawingContext.DrawGeometry(
                    new SolidColorBrush(Color.FromRgb(0, 100, 200)), // Blue
                    new Pen(Brushes.White, 1), 
                    geometry
                );
            }
            
            var renderBitmap = new RenderTargetBitmap(24, 18, 96, 96, PixelFormats.Pbgra32);
            renderBitmap.Render(drawingVisual);
            
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderBitmap));
            
            var stream = new MemoryStream();
            encoder.Save(stream);
            stream.Seek(0, SeekOrigin.Begin);
            
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = stream;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            
            return bitmap;
        }
        
        private void UpdateDefenseWingPosition(DefenseWingInstance aircraft)
        {
            if (aircraft.AirplaneImage == null) return;
            
            // Update direction based on movement
            UpdateDefenseWingDirection(aircraft);
            
            // Apply smooth rotation based on movement direction
            ApplyDefenseWingRotation(aircraft);
            
            // Convert absolute screen coordinates to virtual screen relative coordinates
            var canvasX = aircraft.Position.X - SystemParameters.VirtualScreenLeft;
            var canvasY = aircraft.Position.Y - SystemParameters.VirtualScreenTop;
            
            Canvas.SetLeft(aircraft.AirplaneImage, canvasX);
            Canvas.SetTop(aircraft.AirplaneImage, canvasY);
        }
        
        private void UpdateDefenseWingDirection(DefenseWingInstance aircraft)
        {
            var deltaX = aircraft.Position.X - aircraft.PreviousPosition.X;
            var deltaY = aircraft.Position.Y - aircraft.PreviousPosition.Y;
            
            // Calculate movement magnitude
            var movement = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            
            if (movement > DIRECTION_THRESHOLD)
            {
                // Calculate angle in degrees (0° = right, 90° = down, 180° = left, 270° = up)
                var targetAngle = Math.Atan2(deltaY, deltaX) * (180.0 / Math.PI);
                
                // Set target angle for smooth rotation transition
                aircraft.TargetAngle = targetAngle;
            }
        }
        
        private void ApplyDefenseWingRotation(DefenseWingInstance aircraft)
        {
            if (aircraft.AirplaneImage == null) return;
            
            // Smooth rotation interpolation
            var angleDifference = aircraft.TargetAngle - aircraft.FlightAngle;
            
            // Handle angle wrapping (e.g., 350° to 10°)
            if (angleDifference > 180) angleDifference -= 360;
            if (angleDifference < -180) angleDifference += 360;
            
            // Apply smooth rotation transition (15% per frame for natural movement)
            aircraft.FlightAngle += angleDifference * 0.15;
            
            // Create transform group for rotation
            var transformGroup = new TransformGroup();
            
            // Apply rotation transform
            var rotateTransform = new RotateTransform(aircraft.FlightAngle);
            transformGroup.Children.Add(rotateTransform);
            
            // Set transform and origin
            aircraft.AirplaneImage.RenderTransform = transformGroup;
            aircraft.AirplaneImage.RenderTransformOrigin = new Point(0.5, 0.5);
        }
        
        private void UpdateAllAirplanes()
        {
            foreach (var airplane in _activeAirplanes.ToList())
            {
                UpdateSpeedTransitions(airplane);
                UpdateAirplaneBehavior(airplane);
                UpdateAirplanePosition(airplane);
            }
        }
        
        private void UpdateSpeedTransitions(AirplaneInstance airplane)
        {
            // Smoothly interpolate current speed toward target speed
            if (Math.Abs(airplane.CurrentSpeed - airplane.TargetSpeed) > 0.001)
            {
                var speedDifference = airplane.TargetSpeed - airplane.CurrentSpeed;
                airplane.CurrentSpeed += speedDifference * SPEED_TRANSITION_RATE;
                
                // Snap to target if very close
                if (Math.Abs(speedDifference) < 0.001)
                {
                    airplane.CurrentSpeed = airplane.TargetSpeed;
                }
            }
        }

        private void UpdateAirplaneBehavior(AirplaneInstance airplane)
        {
            // Store previous position for direction tracking
            airplane.PreviousPosition = airplane.Position;
            
            switch (airplane.CurrentState)
            {
                case AirplaneState.Flying:
                    ExecuteFlyingBehavior(airplane);
                    break;
                    
                case AirplaneState.Targeting:
                    ExecuteTargetingBehavior(airplane);
                    break;
                    
                case AirplaneState.Bombing:
                    ExecuteBombingBehavior(airplane);
                    break;
                    
                case AirplaneState.ClearingArea:
                    ExecuteClearingAreaBehavior(airplane);
                    break;
                    
                case AirplaneState.Exploding:
                    ExecuteExplodingBehavior(airplane);
                    break;
                    
                case AirplaneState.ShotDown:
                    ExecuteShotDownBehavior(airplane);
                    break;
                    
                case AirplaneState.HoldingPattern:
                    ExecuteHoldingPatternBehavior(airplane);
                    break;
                    
                case AirplaneState.Escaping:
                    ExecuteEscapingBehavior(airplane);
                    break;
            }
        }
        
        private void ExecuteFlyingBehavior(AirplaneInstance airplane)
        {
            airplane.TargetSpeed = FLYING_SPEED;
            
            // Check if airplane has reached halfway point toward target
            var centerX = SystemParameters.VirtualScreenWidth / 2;
            var distanceToCenter = Math.Abs(airplane.Position.X - centerX);
            
            if (distanceToCenter < 50) // Reached center area
            {
                // Transition to targeting
                airplane.CurrentState = AirplaneState.Targeting;
                airplane.LastStateChange = DateTime.Now;
                return;
            }
            
            // Move toward screen center
            var deltaX = centerX - airplane.Position.X;
            var deltaY = airplane.TargetLocation.Y - airplane.Position.Y;
            var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            
            if (distance > 5)
            {
                airplane.Position = new Point(
                    airplane.Position.X + (deltaX / distance) * airplane.CurrentSpeed * 50,
                    airplane.Position.Y + (deltaY / distance) * airplane.CurrentSpeed * 25
                );
            }
        }
        
        private void ExecuteTargetingBehavior(AirplaneInstance airplane)
        {
            airplane.TargetSpeed = TARGETING_SPEED;
            
            // Move directly toward target location
            var deltaX = airplane.TargetLocation.X - airplane.Position.X;
            var deltaY = airplane.TargetLocation.Y - airplane.Position.Y;
            var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            
            if (distance < 40) // Close enough to bomb
            {
                // Transition to bombing
                airplane.CurrentState = AirplaneState.Bombing;
                airplane.LastStateChange = DateTime.Now;
                return;
            }
            
            if (distance > 5)
            {
                airplane.Position = new Point(
                    airplane.Position.X + (deltaX / distance) * airplane.CurrentSpeed * 60,
                    airplane.Position.Y + (deltaY / distance) * airplane.CurrentSpeed * 60
                );
            }
        }
        
        private void ExecuteBombingBehavior(AirplaneInstance airplane)
        {
            airplane.TargetSpeed = BOMBING_SPEED;
            
            // Drop bomb (reduce ammunition)
            airplane.BombsRemaining--;
            airplane.TotalBombsDropped++;
            
            // Create delayed bomb at target location
            CreateDelayedBomb(airplane.TargetLocation);
            
            // Transition to clearing area state - continue flying past target
            airplane.CurrentState = AirplaneState.ClearingArea;
            airplane.LastStateChange = DateTime.Now;
        }
        
        private void ExecuteExplosion(AirplaneInstance airplane)
        {
            // Show explosion at target location
            if (airplane.ExplosionImage != null)
            {
                airplane.ExplosionImage.Visibility = Visibility.Visible;
                airplane.ExplosionAnimationPlaying = true;
                airplane.CurrentExplosionFrame = 0;
                
                // Position explosion at target
                var explosionCanvasX = airplane.TargetLocation.X - 32 - SystemParameters.VirtualScreenLeft;
                var explosionCanvasY = airplane.TargetLocation.Y - 32 - SystemParameters.VirtualScreenTop;
                Canvas.SetLeft(airplane.ExplosionImage, explosionCanvasX);
                Canvas.SetTop(airplane.ExplosionImage, explosionCanvasY);
                
                // Set initial explosion frame
                airplane.ExplosionImage.Source = _explosionSprites[0];
                airplane.ExplosionImage.Opacity = 1;
            }
        }
        
        private void ExecuteExplodingBehavior(AirplaneInstance airplane)
        {
            // Continue flying past target during explosion
            var timeSinceExplosion = DateTime.Now - airplane.LastStateChange;
            
            if (timeSinceExplosion.TotalSeconds >= 1.0) // Explosion duration
            {
                // Decide next state based on ammunition
                if (airplane.BombsRemaining > 0)
                {
                    // Enter holding pattern for next bombing run
                    airplane.CurrentState = AirplaneState.HoldingPattern;
                    airplane.LastStateChange = DateTime.Now;
                    
                    // Initialize holding pattern
                    InitializeHoldingPattern(airplane);
                }
                else
                {
                    // No bombs left - escape
                    airplane.CurrentState = AirplaneState.Escaping;
                    airplane.LastStateChange = DateTime.Now;
                }
            }
            
            // Continue flying away from target
            var deltaX = airplane.Position.X - airplane.TargetLocation.X;
            var deltaY = airplane.Position.Y - airplane.TargetLocation.Y;
            var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            
            if (distance > 5)
            {
                airplane.Position = new Point(
                    airplane.Position.X + (deltaX / distance) * airplane.CurrentSpeed * 40,
                    airplane.Position.Y + (deltaY / distance) * airplane.CurrentSpeed * 40
                );
            }
        }
        
        private void InitializeHoldingPattern(AirplaneInstance airplane)
        {
            // Calculate initial angle based on current position relative to target
            var deltaX = airplane.Position.X - airplane.TargetLocation.X;
            var deltaY = airplane.Position.Y - airplane.TargetLocation.Y;
            airplane.HoldingPatternAngle = Math.Atan2(deltaY, deltaX);
            airplane.OrbitStartAngle = airplane.HoldingPatternAngle;
            airplane.CompletedOrbits = 0;
        }
        
        private void ExecuteHoldingPatternBehavior(AirplaneInstance airplane)
        {
            // Circular motion around target
            airplane.HoldingPatternAngle += airplane.OrbitSpeed * airplane.OrbitDirection;
            
            // Calculate new position on circle
            airplane.Position = new Point(
                airplane.TargetLocation.X + airplane.HoldingPatternRadius * Math.Cos(airplane.HoldingPatternAngle),
                airplane.TargetLocation.Y + airplane.HoldingPatternRadius * Math.Sin(airplane.HoldingPatternAngle)
            );
            
            // Check if completed full orbit
            var angleDifference = Math.Abs(airplane.HoldingPatternAngle - airplane.OrbitStartAngle);
            if (angleDifference >= 2 * Math.PI)
            {
                airplane.CompletedOrbits++;
                airplane.OrbitStartAngle = airplane.HoldingPatternAngle;
                
                // After 1-2 orbits, return to targeting for next bombing run
                if (airplane.CompletedOrbits >= 1)
                {
                    airplane.CurrentState = AirplaneState.Targeting;
                    airplane.LastStateChange = DateTime.Now;
                    airplane.CompletedOrbits = 0;
                }
            }
        }
        
        private void ExecuteEscapingBehavior(AirplaneInstance airplane)
        {
            airplane.TargetSpeed = ESCAPE_SPEED;
            
            // Find nearest screen edge
            var screenWidth = SystemParameters.VirtualScreenWidth;
            var screenHeight = SystemParameters.VirtualScreenHeight;
            
            var distanceToLeft = airplane.Position.X;
            var distanceToRight = screenWidth - airplane.Position.X;
            var distanceToTop = airplane.Position.Y;
            var distanceToBottom = screenHeight - airplane.Position.Y;
            
            var minDistance = Math.Min(Math.Min(distanceToLeft, distanceToRight), 
                                     Math.Min(distanceToTop, distanceToBottom));
            
            Point escapeTarget;
            if (minDistance == distanceToLeft)
                escapeTarget = new Point(-50, airplane.Position.Y);
            else if (minDistance == distanceToRight)
                escapeTarget = new Point(screenWidth + 50, airplane.Position.Y);
            else if (minDistance == distanceToTop)
                escapeTarget = new Point(airplane.Position.X, -50);
            else
                escapeTarget = new Point(airplane.Position.X, screenHeight + 50);
            
            // Move toward escape point
            var deltaX = escapeTarget.X - airplane.Position.X;
            var deltaY = escapeTarget.Y - airplane.Position.Y;
            var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            
            if (distance > 5)
            {
                airplane.Position = new Point(
                    airplane.Position.X + (deltaX / distance) * airplane.CurrentSpeed * 70,
                    airplane.Position.Y + (deltaY / distance) * airplane.CurrentSpeed * 70
                );
            }
        }
        
        private void ExecuteShotDownBehavior(AirplaneInstance airplane)
        {
            // Stop airplane movement
            airplane.TargetSpeed = 0;
            
            // Check if explosion duration has elapsed (1 second)
            var timeSinceShot = DateTime.Now - airplane.LastStateChange;
            if (timeSinceShot.TotalSeconds >= 1.0)
            {
                // Mark airplane for removal - it will be cleaned up by CleanupFinishedAirplanes
                airplane.CurrentState = AirplaneState.Escaping; // Use escaping state to trigger cleanup
                
                // Move airplane off-screen immediately for cleanup
                airplane.Position = new Point(-1000, -1000);
            }
            
            // Hide airplane sprite during explosion
            if (airplane.AirplaneImage != null)
            {
                airplane.AirplaneImage.Visibility = Visibility.Hidden;
            }
        }

        private void UpdateAirplaneDirection(AirplaneInstance airplane)
        {
            var deltaX = airplane.Position.X - airplane.PreviousPosition.X;
            var deltaY = airplane.Position.Y - airplane.PreviousPosition.Y;
            
            // Calculate movement magnitude
            var movement = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            
            if (movement > DIRECTION_THRESHOLD)
            {
                // Calculate angle in degrees (0° = right, 90° = down, 180° = left, 270° = up)
                var targetAngle = Math.Atan2(deltaY, deltaX) * (180.0 / Math.PI);
                
                // Set target angle for smooth rotation transition
                airplane.TargetAngle = targetAngle;
                
                // Update legacy FacingRight for backward compatibility
                airplane.FacingRight = deltaX > 0;
            }
        }
        
        private void ApplySpriteRotation(AirplaneInstance airplane)
        {
            if (airplane.AirplaneImage == null) return;
            
            // Smooth rotation interpolation
            var angleDifference = airplane.TargetAngle - airplane.FlightAngle;
            
            // Handle angle wrapping (e.g., 350° to 10°)
            if (angleDifference > 180) angleDifference -= 360;
            if (angleDifference < -180) angleDifference += 360;
            
            // Apply smooth rotation transition (15% per frame for natural movement)
            airplane.FlightAngle += angleDifference * 0.15;
            
            // Create transform group for rotation
            var transformGroup = new TransformGroup();
            
            // Apply rotation transform
            var rotateTransform = new RotateTransform(airplane.FlightAngle);
            transformGroup.Children.Add(rotateTransform);
            
            // Set transform and origin
            airplane.AirplaneImage.RenderTransform = transformGroup;
            airplane.AirplaneImage.RenderTransformOrigin = new Point(0.5, 0.5);
        }

        private void UpdateAirplanePosition(AirplaneInstance airplane)
        {
            if (airplane.AirplaneImage == null) return;
            
            // Update direction based on movement
            UpdateAirplaneDirection(airplane);
            
            // Apply smooth rotation based on movement direction
            ApplySpriteRotation(airplane);
            
            // Convert absolute screen coordinates to virtual screen relative coordinates
            var canvasX = airplane.Position.X - SystemParameters.VirtualScreenLeft;
            var canvasY = airplane.Position.Y - SystemParameters.VirtualScreenTop;
            
            Canvas.SetLeft(airplane.AirplaneImage, canvasX);
            Canvas.SetTop(airplane.AirplaneImage, canvasY);
        }
        
        private void UpdateSpriteAnimations()
        {
            _frameCounter++;
            
            // Update flying animation (8 FPS) for all airplanes
            if (_frameCounter % 8 == 0)
            {
                foreach (var airplane in _activeAirplanes)
                {
                    if (airplane.AirplaneImage != null)
                    {
                        airplane.CurrentFlyFrame = (airplane.CurrentFlyFrame + 1) % _flySprites.Count;
                        airplane.AirplaneImage.Source = _flySprites[airplane.CurrentFlyFrame];
                    }
                }
                
                // Update Defense Wing animation (8 FPS) for all Defense Wing aircraft
                if (_activeDefenseWing != null && _activeDefenseWing.IsActive)
                {
                    foreach (var aircraft in _activeDefenseWing.Aircraft)
                    {
                        if (aircraft.AirplaneImage != null)
                        {
                            aircraft.CurrentFlyFrame = (aircraft.CurrentFlyFrame + 1) % _defenseWingSprites.Count;
                            aircraft.AirplaneImage.Source = _defenseWingSprites[aircraft.CurrentFlyFrame];
                        }
                    }
                }
            }
            
            // Update explosion animation (12 FPS) for active explosions
            if (_frameCounter % 5 == 0)
            {
                foreach (var airplane in _activeAirplanes)
                {
                    if (airplane.ExplosionAnimationPlaying && airplane.ExplosionImage != null)
                    {
                        airplane.CurrentExplosionFrame++;
                        
                        if (airplane.CurrentExplosionFrame < _explosionSprites.Count)
                        {
                            // Continue animation with next frame
                            airplane.ExplosionImage.Source = _explosionSprites[airplane.CurrentExplosionFrame];
                        }
                        else
                        {
                            // Animation complete - hide explosion and stop animation
                            airplane.ExplosionAnimationPlaying = false;
                            airplane.ExplosionImage.Visibility = Visibility.Hidden;
                            airplane.CurrentExplosionFrame = 0;
                        }
                    }
                }
            }
        }
        
        private void CleanupFinishedAirplanes()
        {
            var airplanesToRemove = new List<AirplaneInstance>();
            
            foreach (var airplane in _activeAirplanes)
            {
                // Remove airplanes that have escaped off-screen
                if (airplane.CurrentState == AirplaneState.Escaping)
                {
                    var screenWidth = SystemParameters.VirtualScreenWidth;
                    var screenHeight = SystemParameters.VirtualScreenHeight;
                    
                    if (airplane.Position.X < -100 || airplane.Position.X > screenWidth + 100 ||
                        airplane.Position.Y < -100 || airplane.Position.Y > screenHeight + 100)
                    {
                        airplanesToRemove.Add(airplane);
                    }
                }
            }
            
            // Remove finished airplanes
            foreach (var airplane in airplanesToRemove)
            {
                if (airplane.AirplaneImage != null)
                    _airplaneCanvas.Children.Remove(airplane.AirplaneImage);
                if (airplane.ExplosionImage != null)
                    _airplaneCanvas.Children.Remove(airplane.ExplosionImage);
                    
                _activeAirplanes.Remove(airplane);
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            
            // Make window click-through
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);
        }

        // Additional Win32 API for click-through
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        private void LoadSprites()
        {
            _flySprites = new List<BitmapImage>();
            _explosionSprites = new List<BitmapImage>();
            _defenseWingSprites = new List<BitmapImage>();
            
            // Load flying animation sprites
            for (int i = 1; i <= 8; i++)
            {
                var bitmap = LoadBitmapFromFile($"Graphics/plane_fly_{i:D2}.png");
                _flySprites.Add(bitmap);
            }
            
            // Load explosion animation sprites
            for (int i = 1; i <= 12; i++)
            {
                var bitmap = LoadBitmapFromFile($"Graphics/bomb_explode_{i:D2}.png");
                _explosionSprites.Add(bitmap);
            }
            
            // Create inverted Defense Wing sprites from normal plane sprites
            foreach (var originalSprite in _flySprites)
            {
                var invertedSprite = CreateInvertedSprite(originalSprite);
                _defenseWingSprites.Add(invertedSprite);
            }
        }
        
        private BitmapImage LoadBitmapFromFile(string relativePath)
        {
            // Try multiple possible locations for the graphics files
            var possiblePaths = new[]
            {
                relativePath, // Original relative path
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath), // App directory
                System.IO.Path.Combine(Directory.GetCurrentDirectory(), relativePath), // Current directory
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", relativePath) // Development directory
            };
            
            foreach (var path in possiblePaths)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(path, UriKind.Absolute);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        return bitmap;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load sprite from {path}: {ex.Message}");
                    continue;
                }
            }
            
            // If all paths fail, log the issue and create fallback
            Console.WriteLine($"Failed to load sprite: {relativePath} from any location");
            Console.WriteLine($"Current directory: {Directory.GetCurrentDirectory()}");
            Console.WriteLine($"App base directory: {AppDomain.CurrentDomain.BaseDirectory}");
            return CreateFallbackBitmap();
        }
        
        private BitmapImage CreateFallbackBitmap()
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            
            var drawingVisual = new DrawingVisual();
            using (DrawingContext drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.DrawRectangle(new SolidColorBrush(Color.FromRgb(100, 100, 100)), null, new Rect(0, 0, 32, 24));
            }
            
            var renderBitmap = new RenderTargetBitmap(32, 24, 96, 96, PixelFormats.Pbgra32);
            renderBitmap.Render(drawingVisual);
            
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderBitmap));
            
            var stream = new MemoryStream();
            encoder.Save(stream);
            stream.Seek(0, SeekOrigin.Begin);
            
            bitmap.StreamSource = stream;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            
            return bitmap;
        }
        
        private BitmapImage CreateInvertedSprite(BitmapImage originalSprite)
        {
            try
            {
                // Convert BitmapImage to WriteableBitmap for pixel manipulation
                var writeableBitmap = new WriteableBitmap(originalSprite);
                writeableBitmap.Lock();
                
                unsafe
                {
                    // Get pointer to pixel data
                    byte* pixels = (byte*)writeableBitmap.BackBuffer.ToPointer();
                    int stride = writeableBitmap.BackBufferStride;
                    int pixelHeight = writeableBitmap.PixelHeight;
                    int pixelWidth = writeableBitmap.PixelWidth;
                    
                    // Process each pixel
                    for (int y = 0; y < pixelHeight; y++)
                    {
                        for (int x = 0; x < pixelWidth; x++)
                        {
                            // Calculate pixel offset (BGRA format)
                            int offset = y * stride + x * 4;
                            
                            // Get current pixel values
                            byte blue = pixels[offset];
                            byte green = pixels[offset + 1];
                            byte red = pixels[offset + 2];
                            byte alpha = pixels[offset + 3];
                            
                            // Only invert non-transparent pixels
                            if (alpha > 0)
                            {
                                // Invert RGB channels (255 - value)
                                pixels[offset] = (byte)(255 - blue);       // Blue
                                pixels[offset + 1] = (byte)(255 - green);  // Green
                                pixels[offset + 2] = (byte)(255 - red);    // Red
                                // Keep alpha unchanged
                            }
                        }
                    }
                }
                
                writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, writeableBitmap.PixelWidth, writeableBitmap.PixelHeight));
                writeableBitmap.Unlock();
                
                // Convert back to BitmapImage
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(writeableBitmap));
                
                var stream = new MemoryStream();
                encoder.Save(stream);
                stream.Seek(0, SeekOrigin.Begin);
                
                var invertedBitmap = new BitmapImage();
                invertedBitmap.BeginInit();
                invertedBitmap.StreamSource = stream;
                invertedBitmap.CacheOption = BitmapCacheOption.OnLoad;
                invertedBitmap.EndInit();
                invertedBitmap.Freeze();
                
                return invertedBitmap;
            }
            catch (Exception ex)
            {
                // If inversion fails, create a fallback colored bitmap
                Console.WriteLine($"Failed to invert sprite: {ex.Message}");
                return CreateDefenseWingFallbackBitmap();
            }
        }
        
        private BitmapImage CreateDefenseWingFallbackBitmap()
        {
            var drawingVisual = new DrawingVisual();
            using (DrawingContext drawingContext = drawingVisual.RenderOpen())
            {
                // Draw inverted-looking airplane shape (cyan color as fallback)
                drawingContext.DrawRectangle(new SolidColorBrush(Color.FromRgb(0, 255, 255)), null, new Rect(0, 0, 32, 24));
            }
            
            var renderBitmap = new RenderTargetBitmap(32, 24, 96, 96, PixelFormats.Pbgra32);
            renderBitmap.Render(drawingVisual);
            
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderBitmap));
            
            var stream = new MemoryStream();
            encoder.Save(stream);
            stream.Seek(0, SeekOrigin.Begin);
            
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = stream;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            
            return bitmap;
        }

        protected override void OnClosed(EventArgs e)
        {
            // Clean up mouse hook
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
            }
            
            // Clean up system tray
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            
            base.OnClosed(e);
        }

        private void SetupSystemTray()
        {
            // Initialize settings path
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var airplanePath = System.IO.Path.Combine(appDataPath, "AirplaneCompanion");
            Directory.CreateDirectory(airplanePath);
            _settingsPath = System.IO.Path.Combine(airplanePath, "settings.json");
            
            // Create system tray icon
            _notifyIcon = new NotifyIcon
            {
                Icon = CreateAirplaneIcon(),
                Text = "Airplane Companion",
                Visible = true
            };
            
            // Create context menu
            CreateContextMenu();
            _notifyIcon.ContextMenuStrip = _contextMenu;
            
            // Handle left-click to show current status
            _notifyIcon.Click += (s, e) =>
            {
                if (((System.Windows.Forms.MouseEventArgs)e).Button == MouseButtons.Left)
                {
                    var statusText = $"Score: {_playerScore} points\nAircraft: {_activeAirplanes.Count} active\nTowers: {_activeTowers.Count}/{MAX_TOWERS}\nCooldown: {_settings.SpawnCooldownSeconds:F1}s";
                    _notifyIcon.ShowBalloonTip(3000, "Airplane Companion", statusText, ToolTipIcon.Info);
                }
            };
        }
        
        private void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    _settings = JsonSerializer.Deserialize<AirplaneSettings>(json) ?? new AirplaneSettings();
                }
                else
                {
                    _settings = new AirplaneSettings();
                    SaveSettings();
                }
                
                // Apply loaded settings
                ApplySettings();
            }
            catch
            {
                _settings = new AirplaneSettings();
            }
        }
        
        private void SaveSettings()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_settings, options);
                File.WriteAllText(_settingsPath, json);
            }
            catch
            {
                // Settings save failed - continue without saving
            }
        }
        
        private void ApplySettings()
        {
            // Apply speed multiplier and other settings
            // Settings will be checked during behavior updates
        }
        
        private System.Drawing.Icon CreateAirplaneIcon()
        {
            // Create a simple airplane icon programmatically
            var bitmap = new System.Drawing.Bitmap(16, 16);
            using (var g = System.Drawing.Graphics.FromImage(bitmap))
            {
                g.Clear(System.Drawing.Color.Transparent);
                
                // Draw simple airplane shape
                var brush = new System.Drawing.SolidBrush(System.Drawing.Color.DarkGreen);
                
                // Body
                g.FillEllipse(brush, 4, 6, 8, 4);
                // Wings
                g.FillRectangle(brush, 2, 7, 12, 2);
                // Nose
                g.FillPolygon(brush, new System.Drawing.Point[] {
                    new System.Drawing.Point(12, 7),
                    new System.Drawing.Point(15, 8),
                    new System.Drawing.Point(12, 9)
                });
                
                brush.Dispose();
            }
            
            return System.Drawing.Icon.FromHandle(bitmap.GetHicon());
        }
        
        private void CreateContextMenu()
        {
            _contextMenu = new ContextMenuStrip();
            
            // Status header
            var statusItem = new ToolStripLabel("✈️ Airplane Companion")
            {
                Font = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold)
            };
            _contextMenu.Items.Add(statusItem);
            _contextMenu.Items.Add(new ToolStripSeparator());
            
            // Exit
            var exitItem = new ToolStripMenuItem("❌ Exit");
            exitItem.Click += (s, e) => {
                _notifyIcon.Visible = false;
                System.Windows.Application.Current.Shutdown();
            };
            _contextMenu.Items.Add(exitItem);
        }

        // Enhanced bombing system methods
        private void CreateDelayedBomb(Point dropLocation)
        {
            var bomb = new Bomb
            {
                DropLocation = dropLocation,
                DropTime = DateTime.Now
            };
            
            // Create explosion image for this bomb
            bomb.ExplosionImage = new Image
            {
                Width = 64,
                Height = 64,
                Source = _explosionSprites[0],
                Visibility = Visibility.Hidden
            };
            
            _airplaneCanvas.Children.Add(bomb.ExplosionImage);
            _activeBombs.Add(bomb);
        }
        
        private void ExecuteClearingAreaBehavior(AirplaneInstance airplane)
        {
            airplane.TargetSpeed = BOMBING_SPEED;
            
            // Continue flying past target in same direction
            var deltaX = airplane.Position.X - airplane.PreviousPosition.X;
            var deltaY = airplane.Position.Y - airplane.PreviousPosition.Y;
            var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            
            if (distance > 1)
            {
                // Continue in same direction
                airplane.Position = new Point(
                    airplane.Position.X + (deltaX / distance) * airplane.CurrentSpeed * 50,
                    airplane.Position.Y + (deltaY / distance) * airplane.CurrentSpeed * 50
                );
            }
            else
            {
                // Fallback: move away from target
                var awayDeltaX = airplane.Position.X - airplane.TargetLocation.X;
                var awayDeltaY = airplane.Position.Y - airplane.TargetLocation.Y;
                var awayDistance = Math.Sqrt(awayDeltaX * awayDeltaX + awayDeltaY * awayDeltaY);
                
                if (awayDistance > 5)
                {
                    airplane.Position = new Point(
                        airplane.Position.X + (awayDeltaX / awayDistance) * airplane.CurrentSpeed * 50,
                        airplane.Position.Y + (awayDeltaY / awayDistance) * airplane.CurrentSpeed * 50
                    );
                }
            }
            
            // Check if enough time has passed for bomb to explode (2 seconds)
            var timeSinceDropped = DateTime.Now - airplane.LastStateChange;
            if (timeSinceDropped.TotalSeconds >= 2.5) // Give extra 0.5s after bomb explosion
            {
                // Decide next state based on ammunition
                if (airplane.BombsRemaining > 0)
                {
                    // Enter holding pattern for next bombing run
                    airplane.CurrentState = AirplaneState.HoldingPattern;
                    airplane.LastStateChange = DateTime.Now;
                    
                    // Initialize holding pattern
                    InitializeHoldingPattern(airplane);
                }
                else
                {
                    // No bombs left - escape
                    airplane.CurrentState = AirplaneState.Escaping;
                    airplane.LastStateChange = DateTime.Now;
                }
            }
        }
        
        private void UpdateActiveBombs()
        {
            foreach (var bomb in _activeBombs.ToList())
            {
                if (!bomb.HasExploded)
                {
                    var timeSinceDrop = DateTime.Now - bomb.DropTime;
                    
                    if (timeSinceDrop >= bomb.DescentDuration)
                    {
                        // Bomb explodes - trigger explosion animation
                        TriggerBombExplosion(bomb);
                        bomb.HasExploded = true;
                    }
                }
                else if (bomb.ExplosionAnimationPlaying && bomb.ExplosionImage != null)
                {
                    // Handle explosion animation for bombs
                    if (_frameCounter % 5 == 0) // 12 FPS
                    {
                        bomb.CurrentExplosionFrame++;
                        
                        if (bomb.CurrentExplosionFrame < _explosionSprites.Count)
                        {
                            // Continue animation with next frame
                            bomb.ExplosionImage.Source = _explosionSprites[bomb.CurrentExplosionFrame];
                        }
                        else
                        {
                            // Animation complete - stop animation
                            bomb.ExplosionAnimationPlaying = false;
                            bomb.ExplosionImage.Visibility = Visibility.Hidden;
                        }
                    }
                }
            }
        }
        
        private void TriggerBombExplosion(Bomb bomb)
        {
            if (bomb.ExplosionImage != null)
            {
                bomb.ExplosionImage.Visibility = Visibility.Visible;
                bomb.ExplosionAnimationPlaying = true;
                bomb.CurrentExplosionFrame = 0;
                
                // Position explosion at drop location
                var explosionCanvasX = bomb.DropLocation.X - 32 - SystemParameters.VirtualScreenLeft;
                var explosionCanvasY = bomb.DropLocation.Y - 32 - SystemParameters.VirtualScreenTop;
                Canvas.SetLeft(bomb.ExplosionImage, explosionCanvasX);
                Canvas.SetTop(bomb.ExplosionImage, explosionCanvasY);
                
                // Set initial explosion frame
                bomb.ExplosionImage.Source = _explosionSprites[0];
                bomb.ExplosionImage.Opacity = 1;
            }
        }
        
        private void CleanupFinishedBombs()
        {
            var bombsToRemove = new List<Bomb>();
            
            foreach (var bomb in _activeBombs)
            {
                // Remove bombs that have finished exploding
                if (bomb.HasExploded && !bomb.ExplosionAnimationPlaying)
                {
                    bombsToRemove.Add(bomb);
                }
            }
            
            // Remove finished bombs
            foreach (var bomb in bombsToRemove)
            {
                if (bomb.ExplosionImage != null)
                    _airplaneCanvas.Children.Remove(bomb.ExplosionImage);
                    
                _activeBombs.Remove(bomb);
            }
        }

        // Tower defense system methods
        private void PlaceTower(Point position)
        {
            // Enforce tower limit
            if (_activeTowers.Count >= MAX_TOWERS)
            {
                // Remove oldest tower
                var oldestTower = _activeTowers[0];
                if (oldestTower.TowerImage != null)
                    _airplaneCanvas.Children.Remove(oldestTower.TowerImage);
                _activeTowers.RemoveAt(0);
            }
            
            // Create new tower
            var tower = new AntiAirTower(position);
            
            // Create placeholder tower image (16x16 white square)
            tower.TowerImage = new Image
            {
                Width = 16,
                Height = 16,
                Source = CreateTowerPlaceholderBitmap(),
                Opacity = 0.8
            };
            
            // Position tower
            var canvasX = position.X - 8 - SystemParameters.VirtualScreenLeft;
            var canvasY = position.Y - 8 - SystemParameters.VirtualScreenTop;
            Canvas.SetLeft(tower.TowerImage, canvasX);
            Canvas.SetTop(tower.TowerImage, canvasY);
            
            _airplaneCanvas.Children.Add(tower.TowerImage);
            _activeTowers.Add(tower);
        }
        
        private BitmapImage CreateTowerPlaceholderBitmap()
        {
            var drawingVisual = new DrawingVisual();
            using (DrawingContext drawingContext = drawingVisual.RenderOpen())
            {
                // Draw white square with black border
                drawingContext.DrawRectangle(
                    Brushes.White, 
                    new Pen(Brushes.Black, 1), 
                    new Rect(0, 0, 16, 16)
                );
            }
            
            var renderBitmap = new RenderTargetBitmap(16, 16, 96, 96, PixelFormats.Pbgra32);
            renderBitmap.Render(drawingVisual);
            
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderBitmap));
            
            var stream = new MemoryStream();
            encoder.Save(stream);
            stream.Seek(0, SeekOrigin.Begin);
            
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = stream;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            
            return bitmap;
        }
        
        private void UpdateTowers()
        {
            foreach (var tower in _activeTowers)
            {
                // Check for targets in range
                AirplaneInstance? bestTarget = null;
                double closestDistance = double.MaxValue;
                
                foreach (var airplane in _activeAirplanes)
                {
                    // Only target airplanes that are not shot down or escaping
                    if (airplane.CurrentState == AirplaneState.ShotDown || 
                        airplane.CurrentState == AirplaneState.Escaping)
                        continue;
                        
                    var distance = CalculateDistance(tower.Position, airplane.Position);
                    if (distance <= tower.Range && distance < closestDistance)
                    {
                        bestTarget = airplane;
                        closestDistance = distance;
                    }
                }
                
                // Handle target switching with delay
                if (bestTarget != tower.CurrentTarget)
                {
                    var timeSinceLastTargetChange = DateTime.Now - tower.LastTargetTime;
                    if (timeSinceLastTargetChange.TotalSeconds >= 1.0 || tower.CurrentTarget == null)
                    {
                        tower.CurrentTarget = bestTarget;
                        tower.LastTargetTime = DateTime.Now;
                    }
                }
                
                // Fire at current target
                if (tower.CurrentTarget != null)
                {
                    var timeSinceLastShot = DateTime.Now - tower.LastShotTime;
                    var fireInterval = 1.0 / tower.FireRate; // Convert shots per second to interval
                    
                    if (timeSinceLastShot.TotalSeconds >= fireInterval)
                    {
                        FireFlakProjectile(tower, tower.CurrentTarget);
                        tower.LastShotTime = DateTime.Now;
                    }
                }
            }
        }
        
        private void FireFlakProjectile(AntiAirTower tower, AirplaneInstance target)
        {
            var projectile = new FlakProjectile(tower.Position, target);
            
            // Create placeholder projectile image (8x8 white circle)
            projectile.ProjectileImage = new Image
            {
                Width = 8,
                Height = 8,
                Source = CreateProjectilePlaceholderBitmap()
            };
            
            _airplaneCanvas.Children.Add(projectile.ProjectileImage);
            _activeProjectiles.Add(projectile);
        }
        
        private BitmapImage CreateProjectilePlaceholderBitmap()
        {
            var drawingVisual = new DrawingVisual();
            using (DrawingContext drawingContext = drawingVisual.RenderOpen())
            {
                // Draw white circle with yellow border for visibility
                drawingContext.DrawEllipse(
                    Brushes.White, 
                    new Pen(Brushes.Yellow, 1), 
                    new Point(4, 4), 3, 3
                );
            }
            
            var renderBitmap = new RenderTargetBitmap(8, 8, 96, 96, PixelFormats.Pbgra32);
            renderBitmap.Render(drawingVisual);
            
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderBitmap));
            
            var stream = new MemoryStream();
            encoder.Save(stream);
            stream.Seek(0, SeekOrigin.Begin);
            
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = stream;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            
            return bitmap;
        }
        
        private void UpdateProjectiles()
        {
            foreach (var projectile in _activeProjectiles.ToList())
            {
                if (projectile.HasHitTarget) continue;
                
                // Move projectile towards target with 100% accuracy
                var targetDistance = CalculateDistance(projectile.Position, projectile.TargetAirplane.Position);
                
                if (targetDistance <= 20) // Hit distance
                {
                    // Hit airplane - transition to shot down state
                    projectile.HasHitTarget = true;
                    projectile.TargetAirplane.CurrentState = AirplaneState.ShotDown;
                    projectile.TargetAirplane.LastStateChange = DateTime.Now;
                    
                    // Calculate and add score for destroyed airplane
                    var scoreEarned = CalculatePlaneScore(projectile.TargetAirplane);
                    _playerScore += scoreEarned;
                    
                    // Start airplane explosion animation
                    if (projectile.TargetAirplane.ExplosionImage != null)
                    {
                        projectile.TargetAirplane.ExplosionImage.Visibility = Visibility.Visible;
                        projectile.TargetAirplane.ExplosionAnimationPlaying = true;
                        projectile.TargetAirplane.CurrentExplosionFrame = 0;
                        
                        // Position explosion at airplane location
                        var explosionCanvasX = projectile.TargetAirplane.Position.X - 32 - SystemParameters.VirtualScreenLeft;
                        var explosionCanvasY = projectile.TargetAirplane.Position.Y - 32 - SystemParameters.VirtualScreenTop;
                        Canvas.SetLeft(projectile.TargetAirplane.ExplosionImage, explosionCanvasX);
                        Canvas.SetTop(projectile.TargetAirplane.ExplosionImage, explosionCanvasY);
                        
                        projectile.TargetAirplane.ExplosionImage.Source = _explosionSprites[0];
                    }
                }
                else
                {
                    // Update projectile position (hitscan style - instant hit with visual trail)
                    projectile.Position = new Point(
                        projectile.Position.X + projectile.Velocity.X,
                        projectile.Position.Y + projectile.Velocity.Y
                    );
                    
                    // Update visual position
                    if (projectile.ProjectileImage != null)
                    {
                        var canvasX = projectile.Position.X - 4 - SystemParameters.VirtualScreenLeft;
                        var canvasY = projectile.Position.Y - 4 - SystemParameters.VirtualScreenTop;
                        Canvas.SetLeft(projectile.ProjectileImage, canvasX);
                        Canvas.SetTop(projectile.ProjectileImage, canvasY);
                    }
                }
            }
        }
        
        private void CleanupFinishedProjectiles()
        {
            var projectilesToRemove = new List<FlakProjectile>();
            
            foreach (var projectile in _activeProjectiles)
            {
                // Remove projectiles that have hit targets or traveled too far
                var distanceTraveled = CalculateDistance(projectile.StartPosition, projectile.Position);
                
                if (projectile.HasHitTarget || distanceTraveled > 800)
                {
                    projectilesToRemove.Add(projectile);
                }
            }
            
            // Remove finished projectiles
            foreach (var projectile in projectilesToRemove)
            {
                if (projectile.ProjectileImage != null)
                    _airplaneCanvas.Children.Remove(projectile.ProjectileImage);
                    
                _activeProjectiles.Remove(projectile);
            }
        }
        
        private double CalculateDistance(Point point1, Point point2)
        {
            var deltaX = point1.X - point2.X;
            var deltaY = point1.Y - point2.Y;
            return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        }
        
        // Scoring system methods
        private int CalculatePlaneScore(AirplaneInstance airplane)
        {
            // Base score: 10 points for destroying the plane
            int baseScore = 10;
            
            // Bonus: 10 points per undropped bomb remaining
            // This rewards early interception of fully-loaded planes
            int bombBonus = airplane.BombsRemaining * 10;
            
            // Total score range: 10-40 points
            // 0 bombs remaining = 10 points
            // 1 bomb remaining = 20 points
            // 2 bombs remaining = 30 points  
            // 3 bombs remaining = 40 points (full plane)
            return baseScore + bombBonus;
        }
        
        // Defense Wing system methods
        private void UpdateDefenseWing()
        {
            if (_activeDefenseWing == null || !_activeDefenseWing.IsActive) return;
            
            // Update fuel for all aircraft
            var deltaTime = 16.0 / 1000.0; // ~60 FPS timer interval in seconds
            foreach (var aircraft in _activeDefenseWing.Aircraft)
            {
                if (!aircraft.IsRefueling)
                {
                    aircraft.FuelRemaining -= deltaTime;
                }
            }
            
            // Check if formation needs to refuel
            if (_activeDefenseWing.AllAircraftNeedRefuel())
            {
                // Start refuel process - aircraft exit to nearest screen edge
                foreach (var aircraft in _activeDefenseWing.Aircraft)
                {
                    aircraft.CurrentState = DefenseWingState.Refueling;
                    aircraft.IsRefueling = true;
                }
                return;
            }
            
            // Update each aircraft in the formation
            foreach (var aircraft in _activeDefenseWing.Aircraft)
            {
                UpdateDefenseWingAircraft(aircraft);
                UpdateDefenseWingPosition(aircraft);
            }
            
            // Update formation patrol logic
            UpdateDefenseWingPatrol();
        }
        
        private void UpdateDefenseWingAircraft(DefenseWingInstance aircraft)
        {
            aircraft.PreviousPosition = aircraft.Position;
            
            switch (aircraft.CurrentState)
            {
                case DefenseWingState.Patrolling:
                    ExecuteDefensePatrolBehavior(aircraft);
                    break;
                    
                case DefenseWingState.Engaging:
                    ExecuteDefenseEngageBehavior(aircraft);
                    break;
                    
                case DefenseWingState.Firing:
                    ExecuteDefenseFiringBehavior(aircraft);
                    break;
                    
                case DefenseWingState.Refueling:
                    ExecuteDefenseRefuelBehavior(aircraft);
                    break;
            }
        }
        
        private void ExecuteDefensePatrolBehavior(DefenseWingInstance aircraft)
        {
            aircraft.TargetSpeed = aircraft.CurrentSpeed;
            
            // Look for enemy targets
            var nearestEnemy = FindNearestEnemyTarget(aircraft.Position);
            if (nearestEnemy != null)
            {
                aircraft.CurrentTarget = nearestEnemy;
                aircraft.CurrentState = DefenseWingState.Engaging;
                aircraft.LastStateChange = DateTime.Now;
                return;
            }
            
            // Stay in formation while patrolling - use the formation target method
            var targetPosition = _activeDefenseWing.GetAircraftFormationTarget(aircraft);
            
            // Move towards formation position with reduced speed multiplier
            var deltaX = targetPosition.X - aircraft.Position.X;
            var deltaY = targetPosition.Y - aircraft.Position.Y;
            var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            
            if (distance > 5)
            {
                aircraft.Position = new Point(
                    aircraft.Position.X + (deltaX / distance) * aircraft.CurrentSpeed * 30,
                    aircraft.Position.Y + (deltaY / distance) * aircraft.CurrentSpeed * 30
                );
            }
        }
        
        private void ExecuteDefenseEngageBehavior(DefenseWingInstance aircraft)
        {
            if (aircraft.CurrentTarget == null || 
                aircraft.CurrentTarget.CurrentState == AirplaneState.ShotDown ||
                aircraft.CurrentTarget.CurrentState == AirplaneState.Escaping)
            {
                // Target lost - return to patrol
                aircraft.CurrentTarget = null;
                aircraft.CurrentState = DefenseWingState.Patrolling;
                aircraft.LastStateChange = DateTime.Now;
                return;
            }
            
            // Move towards target
            var deltaX = aircraft.CurrentTarget.Position.X - aircraft.Position.X;
            var deltaY = aircraft.CurrentTarget.Position.Y - aircraft.Position.Y;
            var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            
            // Check if in firing range
            if (distance <= 512) // 512px range
            {
                aircraft.CurrentState = DefenseWingState.Firing;
                aircraft.LastStateChange = DateTime.Now;
                return;
            }
            
            // Move closer to target with reduced speed multiplier
            if (distance > 5)
            {
                aircraft.Position = new Point(
                    aircraft.Position.X + (deltaX / distance) * aircraft.CurrentSpeed * 35,
                    aircraft.Position.Y + (deltaY / distance) * aircraft.CurrentSpeed * 35
                );
            }
        }
        
        private void ExecuteDefenseFiringBehavior(DefenseWingInstance aircraft)
        {
            aircraft.TargetSpeed = aircraft.FiringSpeed; // 10% slower when firing
            
            if (aircraft.CurrentTarget == null ||
                aircraft.CurrentTarget.CurrentState == AirplaneState.ShotDown ||
                aircraft.CurrentTarget.CurrentState == AirplaneState.Escaping)
            {
                // Target lost - return to patrol
                aircraft.CurrentTarget = null;
                aircraft.CurrentState = DefenseWingState.Patrolling;
                aircraft.LastStateChange = DateTime.Now;
                return;
            }
            
            // Check firing cooldown (unlimited ammo but rate limited)
            var timeSinceLastShot = DateTime.Now - aircraft.LastShotTime;
            if (timeSinceLastShot.TotalSeconds >= 0.5) // 2 shots per second
            {
                // Fire missile
                var missile = new DefenseWingProjectile(aircraft.Position, aircraft.CurrentTarget);
                
                // Create placeholder missile image (red diamond)
                missile.ProjectileImage = new Image
                {
                    Width = 6,
                    Height = 6,
                    Source = CreateMissilePlaceholderBitmap()
                };
                
                _airplaneCanvas.Children.Add(missile.ProjectileImage);
                _activeDefenseProjectiles.Add(missile);
                
                aircraft.LastShotTime = DateTime.Now;
            }
            
            // Check if target is out of range
            var deltaX = aircraft.CurrentTarget.Position.X - aircraft.Position.X;
            var deltaY = aircraft.CurrentTarget.Position.Y - aircraft.Position.Y;
            var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            
            if (distance > 1024) // Out of range
            {
                aircraft.CurrentState = DefenseWingState.Engaging;
                aircraft.LastStateChange = DateTime.Now;
                return;
            }
            
            // Continue firing behavior - slow movement towards target
            if (distance > 5)
            {
                aircraft.Position = new Point(
                    aircraft.Position.X + (deltaX / distance) * aircraft.TargetSpeed * 40,
                    aircraft.Position.Y + (deltaY / distance) * aircraft.TargetSpeed * 40
                );
            }
        }
        
        private void ExecuteDefenseRefuelBehavior(DefenseWingInstance aircraft)
        {
            // Move to nearest screen edge
            var screenWidth = SystemParameters.VirtualScreenWidth;
            var screenHeight = SystemParameters.VirtualScreenHeight;
            
            var distanceToLeft = aircraft.Position.X;
            var distanceToRight = screenWidth - aircraft.Position.X;
            var distanceToTop = aircraft.Position.Y;
            var distanceToBottom = screenHeight - aircraft.Position.Y;
            
            var minDistance = Math.Min(Math.Min(distanceToLeft, distanceToRight),
                                     Math.Min(distanceToTop, distanceToBottom));
            
            Point exitTarget;
            if (minDistance == distanceToLeft)
                exitTarget = new Point(-50, aircraft.Position.Y);
            else if (minDistance == distanceToRight)
                exitTarget = new Point(screenWidth + 50, aircraft.Position.Y);
            else if (minDistance == distanceToTop)
                exitTarget = new Point(aircraft.Position.X, -50);
            else
                exitTarget = new Point(aircraft.Position.X, screenHeight + 50);
            
            // Move toward exit point
            var deltaX = exitTarget.X - aircraft.Position.X;
            var deltaY = exitTarget.Y - aircraft.Position.Y;
            var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            
            if (distance > 5)
            {
                aircraft.Position = new Point(
                    aircraft.Position.X + (deltaX / distance) * aircraft.TargetSpeed * 70,
                    aircraft.Position.Y + (deltaY / distance) * aircraft.TargetSpeed * 70
                );
            }
        }
        
        private void UpdateDefenseWingPatrol()
        {
            if (_activeDefenseWing == null) return;
            
            // Update center transition if needed
            _activeDefenseWing.UpdateCenterTransition();
            
            // Patrol pattern - move formation center in a slow circle around the user-defined center
            _activeDefenseWing.PatrolAngle += 0.01; // Slow rotation
            
            // Use the current center position (which may be transitioning)
            var patrolCenterX = _activeDefenseWing.CenterPosition.X;
            var patrolCenterY = _activeDefenseWing.CenterPosition.Y;
            
            var newFormationCenter = new Point(
                patrolCenterX + _activeDefenseWing.PatrolRadius * Math.Cos(_activeDefenseWing.PatrolAngle),
                patrolCenterY + _activeDefenseWing.PatrolRadius * Math.Sin(_activeDefenseWing.PatrolAngle)
            );
            
            _activeDefenseWing.UpdateFormationCenter(newFormationCenter);
        }
        
        private AirplaneInstance? FindNearestEnemyTarget(Point fromPosition)
        {
            AirplaneInstance? nearest = null;
            double shortestDistance = double.MaxValue;
            
            foreach (var airplane in _activeAirplanes)
            {
                // Only target active enemy aircraft
                if (airplane.CurrentState == AirplaneState.ShotDown ||
                    airplane.CurrentState == AirplaneState.Escaping)
                    continue;
                    
                var distance = CalculateDistance(fromPosition, airplane.Position);
                if (distance < shortestDistance)
                {
                    shortestDistance = distance;
                    nearest = airplane;
                }
            }
            
            return nearest;
        }
        
        private BitmapImage CreateMissilePlaceholderBitmap()
        {
            var drawingVisual = new DrawingVisual();
            using (DrawingContext drawingContext = drawingVisual.RenderOpen())
            {
                // Draw red diamond for missile
                var points = new Point[]
                {
                    new Point(3, 0),  // Top
                    new Point(6, 3),  // Right
                    new Point(3, 6),  // Bottom
                    new Point(0, 3)   // Left
                };
                
                var geometry = new PathGeometry();
                var figure = new PathFigure { StartPoint = points[0] };
                
                for (int i = 1; i < points.Length; i++)
                {
                    figure.Segments.Add(new LineSegment(points[i], true));
                }
                figure.IsClosed = true;
                geometry.Figures.Add(figure);
                
                drawingContext.DrawGeometry(
                    new SolidColorBrush(Color.FromRgb(255, 0, 0)), // Red
                    new Pen(Brushes.Yellow, 0.5), 
                    geometry
                );
            }
            
            var renderBitmap = new RenderTargetBitmap(6, 6, 96, 96, PixelFormats.Pbgra32);
            renderBitmap.Render(drawingVisual);
            
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderBitmap));
            
            var stream = new MemoryStream();
            encoder.Save(stream);
            stream.Seek(0, SeekOrigin.Begin);
            
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = stream;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            
            return bitmap;
        }
        
        private void UpdateDefenseProjectiles()
        {
            foreach (var missile in _activeDefenseProjectiles.ToList())
            {
                if (missile.HasHitTarget) continue;
                
                // Check if target is still valid
                if (missile.TargetAirplane.CurrentState == AirplaneState.ShotDown ||
                    missile.TargetAirplane.CurrentState == AirplaneState.Escaping)
                {
                    missile.HasHitTarget = true;
                    continue;
                }
                
                // Update homing behavior
                missile.UpdateHomingVelocity();
                
                // Move missile
                missile.Position = new Point(
                    missile.Position.X + missile.Velocity.X,
                    missile.Position.Y + missile.Velocity.Y
                );
                
                // Check for hit
                var distance = CalculateDistance(missile.Position, missile.TargetAirplane.Position);
                if (distance <= 15) // Hit distance
                {
                    // Hit target
                    missile.HasHitTarget = true;
                    missile.TargetAirplane.CurrentState = AirplaneState.ShotDown;
                    missile.TargetAirplane.LastStateChange = DateTime.Now;
                    
                    // Add score
                    var scoreEarned = CalculatePlaneScore(missile.TargetAirplane);
                    _playerScore += scoreEarned;
                    
                    // Start explosion
                    if (missile.TargetAirplane.ExplosionImage != null)
                    {
                        missile.TargetAirplane.ExplosionImage.Visibility = Visibility.Visible;
                        missile.TargetAirplane.ExplosionAnimationPlaying = true;
                        missile.TargetAirplane.CurrentExplosionFrame = 0;
                        
                        var explosionCanvasX = missile.TargetAirplane.Position.X - 32 - SystemParameters.VirtualScreenLeft;
                        var explosionCanvasY = missile.TargetAirplane.Position.Y - 32 - SystemParameters.VirtualScreenTop;
                        Canvas.SetLeft(missile.TargetAirplane.ExplosionImage, explosionCanvasX);
                        Canvas.SetTop(missile.TargetAirplane.ExplosionImage, explosionCanvasY);
                        
                        missile.TargetAirplane.ExplosionImage.Source = _explosionSprites[0];
                    }
                }
                else
                {
                    // Update visual position
                    if (missile.ProjectileImage != null)
                    {
                        var canvasX = missile.Position.X - 3 - SystemParameters.VirtualScreenLeft;
                        var canvasY = missile.Position.Y - 3 - SystemParameters.VirtualScreenTop;
                        Canvas.SetLeft(missile.ProjectileImage, canvasX);
                        Canvas.SetTop(missile.ProjectileImage, canvasY);
                    }
                }
            }
        }
        
        private void CleanupFinishedDefenseWing()
        {
            // Clean up finished missiles
            var missilesToRemove = new List<DefenseWingProjectile>();
            
            foreach (var missile in _activeDefenseProjectiles)
            {
                var distanceTraveled = CalculateDistance(missile.StartPosition, missile.Position);
                
                if (missile.HasHitTarget || distanceTraveled > missile.Range)
                {
                    missilesToRemove.Add(missile);
                }
            }
            
            foreach (var missile in missilesToRemove)
            {
                if (missile.ProjectileImage != null)
                    _airplaneCanvas.Children.Remove(missile.ProjectileImage);
                _activeDefenseProjectiles.Remove(missile);
            }
            
            // Clean up defense wing if all aircraft are refueling and off-screen
            if (_activeDefenseWing != null && _activeDefenseWing.AllAircraftNeedRefuel())
            {
                var allOffScreen = true;
                var screenWidth = SystemParameters.VirtualScreenWidth;
                var screenHeight = SystemParameters.VirtualScreenHeight;
                
                foreach (var aircraft in _activeDefenseWing.Aircraft)
                {
                    if (aircraft.Position.X > -100 && aircraft.Position.X < screenWidth + 100 &&
                        aircraft.Position.Y > -100 && aircraft.Position.Y < screenHeight + 100)
                    {
                        allOffScreen = false;
                        break;
                    }
                }
                
                if (allOffScreen)
                {
                    // Remove all defense wing aircraft
                    foreach (var aircraft in _activeDefenseWing.Aircraft)
                    {
                        if (aircraft.AirplaneImage != null)
                            _airplaneCanvas.Children.Remove(aircraft.AirplaneImage);
                    }
                    
                    _activeDefenseWing.IsActive = false;
                    _activeDefenseWing = null;
                }
            }
        }
    }

    // App.xaml.cs equivalent
    public partial class App : System.Windows.Application
    {
        [STAThread]
        public static void Main()
        {
            var app = new App();
            app.Run(new MainWindow());
        }
    }
}
