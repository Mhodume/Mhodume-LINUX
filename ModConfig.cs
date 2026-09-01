using System.ComponentModel;
using System.Text.Json.Serialization;
using Avalonia.Media;

namespace Mhodume;

/// <summary>
/// Root configuration written to crosshair.json and read by the in-game Lua
/// module. One section per feature — new mod-menu features get a new section
/// here and a matching block in main.lua.
/// </summary>
public class ModConfig : ObservableObject
{
    private CrosshairConfig _crosshair = new();
    private HudConfig _hud = new();
    private TrajectoryConfig _trajectory = new();
    private FreecamConfig _freecam = new();
    private SpeedConfig _speed = new();
    private TrainingConfig _training = new();
    private TweaksConfig _tweaks = new();
    private CheckpointsConfig _checkpoints = new();

    [JsonPropertyName("crosshair")]
    public CrosshairConfig Crosshair
    {
        get => _crosshair;
        set { Detach(_crosshair); _crosshair = value ?? new CrosshairConfig(); Attach(_crosshair); OnPropertyChanged(); }
    }

    [JsonPropertyName("hud")]
    public HudConfig Hud
    {
        get => _hud;
        set { Detach(_hud); _hud = value ?? new HudConfig(); Attach(_hud); OnPropertyChanged(); }
    }

    [JsonPropertyName("trajectory")]
    public TrajectoryConfig Trajectory
    {
        get => _trajectory;
        set { Detach(_trajectory); _trajectory = value ?? new TrajectoryConfig(); Attach(_trajectory); OnPropertyChanged(); }
    }

    [JsonPropertyName("freecam")]
    public FreecamConfig Freecam
    {
        get => _freecam;
        set { Detach(_freecam); _freecam = value ?? new FreecamConfig(); Attach(_freecam); OnPropertyChanged(); }
    }

    [JsonPropertyName("speed")]
    public SpeedConfig Speed
    {
        get => _speed;
        set { Detach(_speed); _speed = value ?? new SpeedConfig(); Attach(_speed); OnPropertyChanged(); }
    }

    [JsonPropertyName("training")]
    public TrainingConfig Training
    {
        get => _training;
        set { Detach(_training); _training = value ?? new TrainingConfig(); Attach(_training); OnPropertyChanged(); }
    }

    [JsonPropertyName("checkpoints")]
    public CheckpointsConfig Checkpoints
    {
        get => _checkpoints;
        set { Detach(_checkpoints); _checkpoints = value ?? new CheckpointsConfig(); Attach(_checkpoints); OnPropertyChanged(); }
    }

    [JsonPropertyName("tweaks")]
    public TweaksConfig Tweaks
    {
        get => _tweaks;
        set { Detach(_tweaks); _tweaks = value ?? new TweaksConfig(); Attach(_tweaks); OnPropertyChanged(); }
    }

    public ModConfig()
    {
        Attach(_tweaks);
        Attach(_checkpoints);
        Attach(_training);
        Attach(_speed);
        Attach(_crosshair);
        Attach(_hud);
        Attach(_trajectory);
        Attach(_freecam);
    }

    /// <summary>Raised whenever anything inside any section changes.</summary>
    public event EventHandler? AnyChanged;

    private void Attach(INotifyPropertyChanged section) => section.PropertyChanged += Section_Changed;
    private void Detach(INotifyPropertyChanged? section)
    {
        if (section is not null) section.PropertyChanged -= Section_Changed;
    }

    private void Section_Changed(object? sender, PropertyChangedEventArgs e)
        => AnyChanged?.Invoke(this, EventArgs.Empty);

    public ModConfig Clone() => new()
    {
        Crosshair = Crosshair.Clone(),
        Hud = Hud.Clone(),
        Trajectory = Trajectory.Clone(),
        Freecam = Freecam.Clone(),
        Speed = Speed.Clone(),
        Training = Training.Clone(),
        Tweaks = Tweaks.Clone(),
        Checkpoints = Checkpoints.Clone(),
    };
}

/// <summary>
/// Toggles for the game's own HUD widgets. Each maps to a property on the
/// game's save object, applied edge-triggered by the Lua module so the game's
/// own options menu keeps working.
/// </summary>
public class HudConfig : ObservableObject
{
    private bool _manage;
    private bool _showSpeedometer = true;
    private bool _showTimer = true;
    private bool _showCheckpointTime = true;

    /// <summary>When false, the mod leaves the game's HUD settings alone.</summary>
    [JsonPropertyName("manage")]
    public bool Manage { get => _manage; set => Set(ref _manage, value); }

    [JsonPropertyName("showSpeedometer")]
    public bool ShowSpeedometer { get => _showSpeedometer; set => Set(ref _showSpeedometer, value); }

    [JsonPropertyName("showTimer")]
    public bool ShowTimer { get => _showTimer; set => Set(ref _showTimer, value); }

    [JsonPropertyName("showCheckpointTime")]
    public bool ShowCheckpointTime { get => _showCheckpointTime; set => Set(ref _showCheckpointTime, value); }

    public HudConfig Clone() => new()
    {
        Manage = Manage,
        ShowSpeedometer = ShowSpeedometer,
        ShowTimer = ShowTimer,
        ShowCheckpointTime = ShowCheckpointTime,
    };
}

/// <summary>
/// Draws a recorded run's path in the world as a line.
///
/// The points themselves live in a separate trajectory.json — they are far
/// bigger than the settings and must not be re-parsed by the game every time a
/// slider moves.
/// </summary>
public class TrajectoryConfig : ObservableObject
{
    private bool _enabled;
    private double _thickness = 4;
    private double _opacity = 90;
    private bool _gradient = true;
    private double[] _color = { 0.0, 1.0, 0.3, 1.0 };
    private bool _hideGhost;
    private double _maxDistance = 400;
    private bool _accuracy;
    private double _accWidth = 3;
    private double _accOffsetX;
    private double _accOffsetY = 92;
    private double _accScale = 1;
    private bool _keys;
    private double _keysOffsetX;
    private double _keysOffsetY = 130;
    private double _keysScale = 1;
    private double[] _keysColor = { 0.85, 0.16, 0.16, 1.0 };
    private int _watchRequest;
    private double _watchFrom;
    private double _watchBehind;
    private double _watchAbove;
    private string _map = "";
    private string _sourcePath = "";
    private string _label = "";

    [JsonPropertyName("enabled")]
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }

    [JsonPropertyName("thickness")]
    public double Thickness { get => _thickness; set => Set(ref _thickness, Math.Round(value)); }

    /// <summary>Line opacity in percent; converted to 0..1 on the way out.</summary>
    [JsonIgnore]
    public double Opacity { get => _opacity; set => Set(ref _opacity, Math.Round(value)); }

    [JsonPropertyName("alpha")]
    public double Alpha
    {
        get => Math.Round(_opacity / 100.0, 3);
        set { _opacity = Math.Clamp(value * 100, 0, 100); OnPropertyChanged(nameof(Opacity)); }
    }

    /// <summary>Colour the line by speed instead of using a single colour.</summary>
    [JsonPropertyName("gradient")]
    public bool Gradient { get => _gradient; set => Set(ref _gradient, value); }

    [JsonPropertyName("color")]
    public double[] Color
    {
        get => _color;
        set
        {
            _color = value is { Length: >= 4 } ? value : new[] { 0.0, 1.0, 0.3, 1.0 };
            OnPropertyChanged();
            OnPropertyChanged(nameof(LineColor));
        }
    }

    [JsonIgnore]
    public Color LineColor
    {
        get => Avalonia.Media.Color.FromRgb(
            (byte)Math.Round(Math.Clamp(_color[0], 0, 1) * 255),
            (byte)Math.Round(Math.Clamp(_color[1], 0, 1) * 255),
            (byte)Math.Round(Math.Clamp(_color[2], 0, 1) * 255));
        set
        {
            _color = new[] { value.R / 255.0, value.G / 255.0, value.B / 255.0, 1.0 };
            OnPropertyChanged();
            OnPropertyChanged(nameof(Color));
        }
    }

    /// <summary>Hide the ghost character while the trajectory is shown.</summary>
    [JsonPropertyName("hideGhost")]
    public bool HideGhost { get => _hideGhost; set => Set(ref _hideGhost, value); }

    /// <summary>Draw range in metres; beyond this the line is skipped, for framerate.</summary>
    [JsonIgnore]
    public double MaxDistance { get => _maxDistance; set => Set(ref _maxDistance, Math.Round(value)); }

    /// <summary>Same value in centimetres, which is what Unreal works in.</summary>
    [JsonPropertyName("maxDistance")]
    public double MaxDistanceCm
    {
        get => _maxDistance * 100;
        set { _maxDistance = Math.Round(value / 100); OnPropertyChanged(nameof(MaxDistance)); }
    }

    /// <summary>Show how closely you are following the loaded ghost.</summary>
    [JsonPropertyName("accuracy")]
    public bool Accuracy { get => _accuracy; set => Set(ref _accuracy, value); }

    /// <summary>
    /// How far off the line counts as nothing, in metres. A gap to thread and a
    /// rooftop to cross do not deserve the same tolerance, so it is a setting.
    /// </summary>
    [JsonIgnore]
    public double AccWidth
    {
        get => _accWidth;
        set { Set(ref _accWidth, Math.Round(value, 1)); OnPropertyChanged(nameof(AccWidthCm)); }
    }

    [JsonPropertyName("accWidth")]
    public double AccWidthCm
    {
        get => _accWidth * 100;
        set { _accWidth = Math.Round(value / 100, 1); OnPropertyChanged(nameof(AccWidth)); }
    }

    [JsonPropertyName("accOffsetX")]
    public double AccOffsetX { get => _accOffsetX; set => Set(ref _accOffsetX, Math.Round(value)); }

    [JsonPropertyName("accOffsetY")]
    public double AccOffsetY { get => _accOffsetY; set => Set(ref _accOffsetY, Math.Round(value)); }

    [JsonPropertyName("accScale")]
    public double AccScale { get => _accScale; set => Set(ref _accScale, Math.Round(value, 2)); }

    /// <summary>
    /// Show which keys the loaded ghost was holding where you are standing.
    /// Only runs loaded since this feature existed carry them; older
    /// trajectory files have no fifth field and the pad stays hidden.
    /// </summary>
    [JsonPropertyName("keys")]
    public bool Keys { get => _keys; set => Set(ref _keys, value); }

    [JsonPropertyName("keysOffsetX")]
    public double KeysOffsetX { get => _keysOffsetX; set => Set(ref _keysOffsetX, Math.Round(value)); }

    [JsonPropertyName("keysOffsetY")]
    public double KeysOffsetY { get => _keysOffsetY; set => Set(ref _keysOffsetY, Math.Round(value)); }

    [JsonPropertyName("keysScale")]
    public double KeysScale { get => _keysScale; set => Set(ref _keysScale, Math.Round(value, 2)); }

    [JsonPropertyName("keysColor")]
    public double[] KeysColor { get => _keysColor; set => Set(ref _keysColor, value); }

    /// <summary>
    /// Asks the game to spectate the loaded run. A counter rather than a flag,
    /// so asking for the same moment twice is two viewings: the mod acts on it
    /// changing, and a flag already set says nothing new.
    /// </summary>
    [JsonPropertyName("watchRequest")]
    public int WatchRequest { get => _watchRequest; set => Set(ref _watchRequest, value); }

    /// <summary>Seconds into the run to start from; below zero means stop.</summary>
    [JsonPropertyName("watchFrom")]
    public double WatchFrom { get => _watchFrom; set => Set(ref _watchFrom, Math.Round(value, 2)); }

    /// <summary>
    /// How far back from the runner the camera sits, in centimetres. Zero puts
    /// you behind their eyes; anything else puts them in front of you, which is
    /// the difference between riding the run and watching it.
    /// </summary>
    [JsonPropertyName("watchBehind")]
    public double WatchBehind { get => _watchBehind; set => Set(ref _watchBehind, Math.Round(value)); }

    [JsonPropertyName("watchAbove")]
    public double WatchAbove { get => _watchAbove; set => Set(ref _watchAbove, Math.Round(value)); }

    /// <summary>Map the loaded ghost belongs to; the mod only draws on a match.</summary>
    [JsonPropertyName("map")]
    public string Map { get => _map; set => Set(ref _map, value); }

    /// <summary>Ghost file this trajectory came from (app-side only).</summary>
    [JsonPropertyName("sourcePath")]
    public string SourcePath { get => _sourcePath; set => Set(ref _sourcePath, value); }

    /// <summary>Human-readable description of the loaded run (app-side only).</summary>
    [JsonPropertyName("label")]
    public string Label { get => _label; set => Set(ref _label, value); }

    public TrajectoryConfig Clone() => new()
    {
        Enabled = Enabled, Thickness = Thickness, Opacity = Opacity,
        Gradient = Gradient, Color = (double[])Color.Clone(), HideGhost = HideGhost,
        Accuracy = Accuracy, AccWidth = AccWidth, AccScale = AccScale,
        AccOffsetX = AccOffsetX, AccOffsetY = AccOffsetY,
        Keys = Keys, KeysOffsetX = KeysOffsetX, KeysOffsetY = KeysOffsetY,
        KeysScale = KeysScale, KeysColor = (double[])KeysColor.Clone(),
        WatchRequest = WatchRequest, WatchFrom = WatchFrom,
        WatchBehind = WatchBehind, WatchAbove = WatchAbove,
        MaxDistance = MaxDistance, Map = Map, SourcePath = SourcePath, Label = Label,
    };
}

/// <summary>
/// Flies the character through the level. Implemented as flying movement with
/// collision off rather than a detached camera, so the game's own controls and
/// key bindings apply unchanged.
/// </summary>
public class FreecamConfig : ObservableObject
{
    private bool _enabled;
    private string _key = "F9";
    private double _speed = 2000;
    private bool _disableCollision = true;
    private bool _returnOnExit = true;
    private bool _followLook = true;
    private double _lookFactor = 100;

    [JsonPropertyName("enabled")]
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }

    /// <summary>Key name as the Lua side knows it, e.g. "F9" or "HOME".</summary>
    [JsonPropertyName("key")]
    public string Key { get => _key; set => Set(ref _key, value); }

    [JsonPropertyName("speed")]
    public double Speed { get => _speed; set => Set(ref _speed, Math.Round(value)); }

    [JsonPropertyName("disableCollision")]
    public bool DisableCollision { get => _disableCollision; set => Set(ref _disableCollision, value); }

    /// <summary>
    /// Put the player back at their take-off point when leaving freecam.
    ///
    /// Without this, flying ahead and landing would skip part of the course,
    /// on a game that has online leaderboards. With it, the freecam is purely
    /// an observation tool: the run timer keeps running while you fly, so
    /// there is nothing to gain.
    /// </summary>
    [JsonPropertyName("returnOnExit")]
    public bool ReturnOnExit { get => _returnOnExit; set => Set(ref _returnOnExit, value); }

    /// <summary>
    /// Rise and fall with where you aim, rather than on dedicated keys.
    ///
    /// Keys were tried first and abandoned: UE4SS exposes no held-key state,
    /// this game publishes no jump/slide flags, binding Space fought the
    /// game's own jump, and Ctrl cannot be bound at all. Aim is continuous and
    /// always available.
    /// </summary>
    [JsonPropertyName("followLook")]
    public bool FollowLook { get => _followLook; set => Set(ref _followLook, value); }

    /// <summary>Strength of that effect, as a percentage.</summary>
    [JsonIgnore]
    public double LookFactor { get => _lookFactor; set => Set(ref _lookFactor, Math.Round(value)); }

    [JsonPropertyName("lookFactor")]
    public double LookFactorScalar
    {
        get => Math.Round(_lookFactor / 100.0, 2);
        set { _lookFactor = Math.Clamp(value * 100, 0, 200); OnPropertyChanged(nameof(LookFactor)); }
    }

    public FreecamConfig Clone() => new()
    {
        Enabled = Enabled, Key = Key, Speed = Speed, DisableCollision = DisableCollision,
        FollowLook = FollowLook, LookFactor = LookFactor,
        ReturnOnExit = ReturnOnExit,
    };
}

/// <summary>
/// Current speed drawn next to the crosshair, in the game's own units.
/// Separate from the game's own speedometer, which the HUD section controls.
/// </summary>
public class SpeedConfig : ObservableObject
{
    private int _topSpeed = 2600;
    private bool _colorBySpeed;
    private bool _crisp = true;
    private bool _enabled;
    private double _offsetY = 34;
    private double _offsetX;
    private double _scale = 1.2;
    private double[] _color = { 1.0, 1.0, 1.0, 1.0 };
    private double _decimals;
    private bool _includeFall;
    private string _suffix = "";

    [JsonPropertyName("enabled")]
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }

    [JsonPropertyName("offsetY")]
    public double OffsetY { get => _offsetY; set => Set(ref _offsetY, Math.Round(value)); }

    [JsonPropertyName("offsetX")]
    public double OffsetX { get => _offsetX; set => Set(ref _offsetX, Math.Round(value)); }

    [JsonPropertyName("scale")]
    public double Scale { get => _scale; set => Set(ref _scale, Math.Round(value, 1)); }

    [JsonPropertyName("decimals")]
    public double Decimals { get => _decimals; set => Set(ref _decimals, Math.Round(value)); }

    /// <summary>Counting falling speed makes the figure jump during drops.</summary>
    [JsonPropertyName("includeFall")]
    public bool IncludeFall { get => _includeFall; set => Set(ref _includeFall, value); }

    /// <summary>
    /// Speed treated as the top of the scale, in game units. 2600 is what
    /// speed runners chase, so that is where the gradient turns bright red.
    /// </summary>
    [JsonPropertyName("topSpeed")]
    public int TopSpeed { get => _topSpeed; set => Set(ref _topSpeed, value); }

    [JsonPropertyName("colorBySpeed")]
    public bool ColorBySpeed { get => _colorBySpeed; set => Set(ref _colorBySpeed, value); }

    /// <summary>
    /// Draw the readout with lines rather than the engine font. The font goes
    /// soft when scaled and its strokes stay hairline; lines stay sharp and
    /// thicken with the size.
    /// </summary>
    /// <summary>
    /// Kept so an existing config round-trips, but no longer offered and no
    /// longer read by the mod. Unticked, it drew the readout with the engine
    /// font, and that call leaves this game unable to draw anything at all for
    /// the rest of the session.
    /// </summary>
    [JsonPropertyName("crisp")]
    public bool Crisp { get => _crisp; set => Set(ref _crisp, true); }

    [JsonPropertyName("suffix")]
    public string Suffix { get => _suffix; set => Set(ref _suffix, value ?? ""); }

    [JsonPropertyName("color")]
    public double[] Color
    {
        get => _color;
        set
        {
            _color = value is { Length: >= 4 } ? value : new[] { 1.0, 1.0, 1.0, 1.0 };
            OnPropertyChanged();
            OnPropertyChanged(nameof(TextColor));
        }
    }

    [JsonIgnore]
    public Color TextColor
    {
        get => Avalonia.Media.Color.FromRgb(
            (byte)Math.Round(Math.Clamp(_color[0], 0, 1) * 255),
            (byte)Math.Round(Math.Clamp(_color[1], 0, 1) * 255),
            (byte)Math.Round(Math.Clamp(_color[2], 0, 1) * 255));
        set
        {
            _color = new[] { value.R / 255.0, value.G / 255.0, value.B / 255.0, 1.0 };
            OnPropertyChanged();
            OnPropertyChanged(nameof(Color));
        }
    }

    public SpeedConfig Clone() => new()
    {
        Enabled = Enabled, OffsetY = OffsetY, OffsetX = OffsetX, Scale = Scale,
        Color = (double[])Color.Clone(), Decimals = Decimals,
        IncludeFall = IncludeFall, Suffix = Suffix,
        TopSpeed = TopSpeed, ColorBySpeed = ColorBySpeed, Crisp = Crisp,
    };
}

/// <summary>
/// The on-screen indicator shown while training mode is on, and afterwards
/// while the lap remains uncountable.
/// </summary>
public class TrainingConfig : ObservableObject
{
    private bool _showBadge = true;
    private string _text = "TRAINING";
    private string _taintedText = "LAP NOT COUNTABLE";
    private double _opacity = 50;
    private double _scale = 1.3;
    private double _margin = 24;

    [JsonPropertyName("showBadge")]
    public bool ShowBadge { get => _showBadge; set => Set(ref _showBadge, value); }

    [JsonPropertyName("text")]
    public string Text { get => _text; set => Set(ref _text, value ?? "TRAINING"); }

    /// <summary>Shown once training mode is off but the lap is still spent.</summary>
    [JsonPropertyName("taintedText")]
    public string TaintedText
    {
        get => _taintedText;
        set => Set(ref _taintedText, value ?? "LAP NOT COUNTABLE");
    }

    [JsonIgnore]
    public double Opacity { get => _opacity; set => Set(ref _opacity, Math.Round(value)); }

    [JsonPropertyName("alpha")]
    public double Alpha
    {
        get => Math.Round(_opacity / 100.0, 3);
        set { _opacity = Math.Clamp(value * 100, 0, 100); OnPropertyChanged(nameof(Opacity)); }
    }

    [JsonPropertyName("scale")]
    public double Scale { get => _scale; set => Set(ref _scale, Math.Round(value, 1)); }

    [JsonPropertyName("margin")]
    public double Margin { get => _margin; set => Set(ref _margin, Math.Round(value)); }

    public TrainingConfig Clone() => new()
    {
        ShowBadge = ShowBadge, Text = Text, TaintedText = TaintedText,
        Opacity = Opacity, Scale = Scale, Margin = Margin,
    };
}

/// <summary>
/// Small behaviour changes for practice. Nothing here gives an advantage, so
/// none of it requires training mode.
/// </summary>
public class TweaksConfig : ObservableObject
{
    private bool _stayOnLevel;

    /// <summary>
    /// Come back to the level you were practising instead of advancing.
    /// The mod cannot cancel the change, so it loads your level back.
    /// </summary>
    [JsonPropertyName("stayOnLevel")]
    public bool StayOnLevel { get => _stayOnLevel; set => Set(ref _stayOnLevel, value); }

    // Jump to a level: set GotoLevel, then bump GotoRequest. The mod acts on the
    // counter changing, and recognises the arrival as its own, so "stay on the
    // level" does not fight it.
    private string _gotoLevel = "";
    private int _gotoRequest;

    [JsonPropertyName("gotoLevel")]
    public string GotoLevel { get => _gotoLevel; set => Set(ref _gotoLevel, value ?? ""); }

    [JsonPropertyName("gotoRequest")]
    public int GotoRequest { get => _gotoRequest; set => Set(ref _gotoRequest, value); }

    public TweaksConfig Clone() => new()
    {
        StayOnLevel = StayOnLevel, GotoLevel = GotoLevel, GotoRequest = GotoRequest,
    };
}


/// <summary>
/// Split times along a map. Passive — it measures and changes nothing — so
/// unlike the trail and freecam this needs no training mode.
/// </summary>
public class CheckpointsConfig : ObservableObject
{
    // On by default: the mod binds the placement key at load only when
    // checkpoints are enabled, so starting off meant no key until a restart.
    private bool _enabled = true;
    private int _radius = 250;
    private int _height = 600;
    private string _key = "INS";
    private bool _showClock = true;
    private int _offsetY = 62;
    private double _scale = 1.0;
    private double _holdSeconds = 3.0;
    private double[] _color = { 1.0, 1.0, 1.0, 1.0 };

    private int _trainSection;
    private bool _holdOnReturn = true;
    private int _goRequest;
    private int _goSection;
    private bool _panel = true;
    private bool _panelHeader = true;
    private int _panelX = 24;
    private int _panelY = 120;
    private double _panelScale = 0.85;
    private double[] _panelColor = { 0.85, 0.88, 0.95, 1.0 };
    private bool _show = true;
    private int _markerSize = 120;
    private int _markerHeight = 40;
    private double[] _markerColor = { 1.0, 0.85, 0.0, 1.0 };
    private double[] _nextColor = { 0.1, 1.0, 0.35, 1.0 };
    private double[] _doneColor = { 0.45, 0.45, 0.5, 1.0 };
    private int _markerThickness = 2;
    private int _markerDistance = 6000;

    [JsonPropertyName("enabled")]
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }

    /// <summary>How close you have to pass. Generous: you cross these at speed.</summary>
    [JsonPropertyName("radius")]
    public int Radius { get => _radius; set => Set(ref _radius, value); }

    /// <summary>
    /// Vertical reach. A checkpoint is a standing column, not a ball: you drop
    /// one on the ground and cross it mid-jump, which equal reach would miss.
    /// </summary>
    [JsonPropertyName("height")]
    public int Height { get => _height; set => Set(ref _height, value); }

    /// <summary>
    /// Key that drops a checkpoint. Must be one the mod already binds — it
    /// shares one set of binds rather than claiming keys of its own, which is
    /// what broke drawing when F10 collided with the game's console.
    /// </summary>
    [JsonPropertyName("key")]
    public string Key { get => _key; set => Set(ref _key, value ?? "INS"); }

    [JsonPropertyName("showClock")]
    public bool ShowClock { get => _showClock; set => Set(ref _showClock, value); }

    [JsonPropertyName("offsetY")]
    public int OffsetY { get => _offsetY; set => Set(ref _offsetY, value); }

    [JsonPropertyName("scale")]
    public double Scale { get => _scale; set => Set(ref _scale, value); }

    /// <summary>Seconds a split stays on screen before the clock returns.</summary>
    [JsonPropertyName("holdSeconds")]
    public double HoldSeconds { get => _holdSeconds; set => Set(ref _holdSeconds, value); }

    [JsonPropertyName("color")]
    public double[] Color { get => _color; set => Set(ref _color, value ?? new[] { 1.0, 1.0, 1.0, 1.0 }); }

    [JsonIgnore]
    public Color TextColor
    {
        get => Avalonia.Media.Color.FromRgb(
            (byte)Math.Round(Math.Clamp(_color[0], 0, 1) * 255),
            (byte)Math.Round(Math.Clamp(_color[1], 0, 1) * 255),
            (byte)Math.Round(Math.Clamp(_color[2], 0, 1) * 255));
        set
        {
            _color = new[] { value.R / 255.0, value.G / 255.0, value.B / 255.0, 1.0 };
            OnPropertyChanged();
            OnPropertyChanged(nameof(Color));
        }
    }

    /// <summary>
    /// Section being drilled, or 0. While set, finishing that section puts you
    /// back at its start with the speed and direction you had on your best run
    /// of it — practising the approach as well as the stretch.
    /// </summary>
    [JsonPropertyName("trainSection")]
    public int TrainSection { get => _trainSection; set => Set(ref _trainSection, value); }

    /// <summary>
    /// Hold you in place after being put back, until you move the view.
    /// Arriving at speed the instant you finish leaves no moment to think.
    /// </summary>
    [JsonPropertyName("holdOnReturn")]
    public bool HoldOnReturn { get => _holdOnReturn; set => Set(ref _holdOnReturn, value); }

    /// <summary>
    /// Asks the mod to put you on a section, once. A counter rather than a
    /// flag, so asking twice for the same section is two trips — the mod acts
    /// when the number changes and never on whatever it was at startup.
    /// </summary>
    [JsonPropertyName("goRequest")]
    public int GoRequest { get => _goRequest; set => Set(ref _goRequest, value); }

    [JsonPropertyName("goSection")]
    public int GoSection { get => _goSection; set => Set(ref _goSection, value); }

    /// <summary>The list of checkpoints, with your best and last on each.</summary>
    [JsonPropertyName("panel")]
    public bool Panel { get => _panel; set => Set(ref _panel, value); }

    /// <summary>Names the three time columns above the list.</summary>
    [JsonPropertyName("panelHeader")]
    public bool PanelHeader { get => _panelHeader; set => Set(ref _panelHeader, value); }

    [JsonPropertyName("panelX")]
    public int PanelX { get => _panelX; set => Set(ref _panelX, value); }

    [JsonPropertyName("panelY")]
    public int PanelY { get => _panelY; set => Set(ref _panelY, value); }

    [JsonPropertyName("panelScale")]
    public double PanelScale { get => _panelScale; set => Set(ref _panelScale, value); }

    [JsonPropertyName("panelColor")]
    public double[] PanelColor { get => _panelColor; set => Set(ref _panelColor, value ?? new[] { 0.85, 0.88, 0.95, 1.0 }); }

    [JsonPropertyName("show")]
    public bool Show { get => _show; set => Set(ref _show, value); }

    /// <summary>Half-width on the ground, centimetres.</summary>
    [JsonPropertyName("markerSize")]
    public int MarkerSize { get => _markerSize; set => Set(ref _markerSize, value); }

    /// <summary>Half-height. Low reads as a gate rather than a box in the way.</summary>
    [JsonPropertyName("markerHeight")]
    public int MarkerHeight { get => _markerHeight; set => Set(ref _markerHeight, value); }

    [JsonPropertyName("markerColor")]
    public double[] MarkerColor { get => _markerColor; set => Set(ref _markerColor, value ?? new[] { 1.0, 0.85, 0.0, 1.0 }); }

    [JsonPropertyName("nextColor")]
    public double[] NextColor { get => _nextColor; set => Set(ref _nextColor, value ?? new[] { 0.1, 1.0, 0.35, 1.0 }); }

    [JsonPropertyName("doneColor")]
    public double[] DoneColor { get => _doneColor; set => Set(ref _doneColor, value ?? new[] { 0.45, 0.45, 0.5, 1.0 }); }

    [JsonPropertyName("markerThickness")]
    public int MarkerThickness { get => _markerThickness; set => Set(ref _markerThickness, value); }

    /// <summary>Beyond this, a checkpoint is not drawn at all.</summary>
    [JsonPropertyName("markerDistance")]
    public int MarkerDistance { get => _markerDistance; set => Set(ref _markerDistance, value); }

    [JsonIgnore]
    public Color MarkerBrush
    {
        get => Avalonia.Media.Color.FromRgb(
            (byte)Math.Round(Math.Clamp(_markerColor[0], 0, 1) * 255),
            (byte)Math.Round(Math.Clamp(_markerColor[1], 0, 1) * 255),
            (byte)Math.Round(Math.Clamp(_markerColor[2], 0, 1) * 255));
        set
        {
            _markerColor = new[] { value.R / 255.0, value.G / 255.0, value.B / 255.0, 1.0 };
            OnPropertyChanged();
            OnPropertyChanged(nameof(MarkerColor));
        }
    }

    public CheckpointsConfig Clone() => new()
    {
        Enabled = Enabled, Radius = Radius, Height = Height, Key = Key,
        ShowClock = ShowClock,
        OffsetY = OffsetY, Scale = Scale, HoldSeconds = HoldSeconds,
        Color = (double[])Color.Clone(),
        TrainSection = TrainSection, HoldOnReturn = HoldOnReturn,
        GoRequest = GoRequest, GoSection = GoSection,
        Panel = Panel, PanelHeader = PanelHeader, PanelX = PanelX, PanelY = PanelY,
        PanelScale = PanelScale, PanelColor = (double[])PanelColor.Clone(),
        Show = Show, MarkerSize = MarkerSize, MarkerHeight = MarkerHeight,
        MarkerColor = (double[])MarkerColor.Clone(),
        NextColor = (double[])NextColor.Clone(),
        DoneColor = (double[])DoneColor.Clone(),
        MarkerThickness = MarkerThickness, MarkerDistance = MarkerDistance,
    };
}
