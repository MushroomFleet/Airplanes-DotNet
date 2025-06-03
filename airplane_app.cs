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
        Spawning, Flying, Targeting, Bombing, Exploding, HoldingPattern, Escaping 
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
            OrbitSpeed = 0.02 + random.NextDouble() * 0.02; // Varied orbit speeds
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
        
        // Speed constants
        private const double FLYING_SPEED = 0.03;
        private const double TARGETING_SPEED = 0.025;
        private const double BOMBING_SPEED = 0.02;
        private const double ESCAPE_SPEED = 0.04;
        
        // Smooth transition variables
        private const double SPEED_TRANSITION_RATE = 0.08; // How fast speed changes
        
        // Aircraft management
        private List<AirplaneInstance> _activeAirplanes = new List<AirplaneInstance>();
        private Random _flightRandom = new Random();
        
        // Sprite animation system
        private List<BitmapImage> _flySprites;
        private List<BitmapImage> _explosionSprites;
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
            
            // Handle click detection for spawning airplanes
            if (wParam == (IntPtr)WM_LBUTTONDOWN || wParam == (IntPtr)WM_RBUTTONDOWN)
            {
                _lastClickPos = _currentMousePos;
                
                // Check spawn cooldown
                var timeSinceLastSpawn = DateTime.Now - _lastSpawnTime;
                if (timeSinceLastSpawn.TotalSeconds >= _settings.SpawnCooldownSeconds)
                {
                    SpawnAirplane(_lastClickPos);
                    _lastSpawnTime = DateTime.Now;
                }
            }
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            UpdateAllAirplanes();
            UpdateSpriteAnimations();
            CleanupFinishedAirplanes();
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
                    
                case AirplaneState.Exploding:
                    ExecuteExplodingBehavior(airplane);
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
            
            // Start explosion at target location
            ExecuteExplosion(airplane);
            
            // Transition to exploding state
            airplane.CurrentState = AirplaneState.Exploding;
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
        }
        
        private BitmapImage LoadBitmapFromFile(string relativePath)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(relativePath, UriKind.Relative);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception ex)
            {
                // If loading fails, create a fallback placeholder
                Console.WriteLine($"Failed to load sprite: {relativePath} - {ex.Message}");
                return CreateFallbackBitmap();
            }
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
                    var statusText = $"Status: {_activeAirplanes.Count} aircraft active\nCooldown: {_settings.SpawnCooldownSeconds:F1}s\nSpeed: {_settings.SpeedMultiplier:F1}x";
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
