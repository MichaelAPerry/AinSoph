using Godot;
using AinSoph.Audio;

namespace AinSoph.UI;

/// <summary>
/// The Esc menu: settings, controls, quit. The world does not pause — survival
/// runs on real hours — so this is a menu, not a pause screen, and it says so.
/// </summary>
public partial class GameMenu : CanvasLayer
{
    private Control _root = null!;
    private Label   _controls = null!;

    public bool IsOpen => _root.Visible;

    private static readonly Color Gold  = new(1f, 0.85f, 0.45f);
    private static readonly Color Text  = new(0.85f, 0.84f, 0.78f);
    private static readonly Color Muted = new(0.55f, 0.53f, 0.47f);

    public override void _Ready()
    {
        Layer = 40;
        ProcessMode = ProcessModeEnum.Always;
        Build();
        _root.Visible = false;
    }

    public override void _UnhandledInput(InputEvent ev)
    {
        if (ev is not InputEventKey { Pressed: true, Echo: false } key || key.Keycode != Key.Escape) return;
        if (!IsOpen && GameRoot.Scene is { } scene && (scene.DialogueOpen || scene.InputLocked)) return;
        Toggle();
        GetViewport().SetInputAsHandled();
    }

    public void Toggle()
    {
        _root.Visible = !_root.Visible;
        if (GameRoot.Scene != null) GameRoot.Scene.MenuOpen = _root.Visible;
        Sound.Play("click");
        if (!_root.Visible) GameSettings.Save();
    }

    private void Build()
    {
        var vp = GetViewport().GetVisibleRect().Size;
        _root = new Control { Size = vp, MouseFilter = Control.MouseFilterEnum.Stop };
        AddChild(_root);
        _root.AddChild(new ColorRect { Color = new Color(0, 0, 0, 0.82f), Size = vp });

        var panel = new VBoxContainer { Position = new Vector2(vp.X / 2 - 230, 70), Size = new Vector2(460, 0) };
        panel.AddThemeConstantOverride("separation", 10);
        _root.AddChild(panel);

        panel.AddChild(MakeLabel("AIN SOPH", 30, Gold));
        panel.AddChild(MakeLabel($"version {GameSettings.Version}", 11, Muted));
        panel.AddChild(MakeLabel("The world does not pause. Hunger and sleep run on real hours, even while this menu is open — and while you are away.", 13, Text, wrap: true));

        panel.AddChild(MakeButton("RESUME", Toggle));

        var full = new CheckButton { Text = "Fullscreen", ButtonPressed = GameSettings.Fullscreen };
        full.AddThemeColorOverride("font_color", Text);
        full.Toggled += on => { GameSettings.Fullscreen = on; GameSettings.Apply(); };
        panel.AddChild(full);

        panel.AddChild(MakeSlider("Music",    GameSettings.MusicVolume,    v => GameSettings.MusicVolume = v));
        panel.AddChild(MakeSlider("Ambience", GameSettings.AmbienceVolume, v => GameSettings.AmbienceVolume = v));
        panel.AddChild(MakeSlider("Effects",  GameSettings.EffectsVolume,  v => GameSettings.EffectsVolume = v));

        var hints = new CheckButton { Text = "Show hints", ButtonPressed = GameSettings.ShowHints };
        hints.AddThemeColorOverride("font_color", Text);
        hints.Toggled += on => GameSettings.ShowHints = on;
        panel.AddChild(hints);

        _controls = MakeLabel(
            "Walk — WASD, arrow keys, or left-click the ground\n" +
            "Left-click a being — the six primitives (Move, See, Hear, Talk, Reap, Pray)\n" +
            "Right-click a tile — act on it, or on what lies there (manna, bodies)\n" +
            "Reap — eat what is edible beside you, or strike a being beside you\n" +
            "Move on a thing beside you — pick it up.  PACK or I — eat, give or drop what you carry\n" +
            "Pray at the hidden altar — ask for a skill, a thing or a law, or leave the choice to the gods\n" +
            "SLEEP — sleep or wake; only a cave (or a gift of shelter) is safe\n" +
            "RIB — after a week of play, give form to your spouse\n" +
            "Esc — this menu", 12, Muted, wrap: true);
        _controls.Visible = false;
        panel.AddChild(MakeButton("CONTROLS", () => _controls.Visible = !_controls.Visible));
        panel.AddChild(_controls);

        panel.AddChild(MakeButton("QUIT TO DESKTOP", () =>
        {
            GameSettings.Save();
            GetTree().Root.PropagateNotification((int)NotificationWMCloseRequest);
            GetTree().Quit();
        }));
    }

    private static Label MakeLabel(string text, int size, Color color, bool wrap = false)
    {
        var l = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
        if (wrap) { l.AutowrapMode = TextServer.AutowrapMode.WordSmart; l.CustomMinimumSize = new Vector2(460, 0); }
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color);
        return l;
    }

    private static Button MakeButton(string text, System.Action onPress)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(460, 34) };
        b.AddThemeFontSizeOverride("font_size", 13);
        b.AddThemeColorOverride("font_color", Text);
        var style = new StyleBoxFlat { BgColor = new Color(0.10f, 0.10f, 0.09f), BorderColor = new Color(0.35f, 0.31f, 0.22f) };
        style.SetBorderWidthAll(1);
        var hover = (StyleBoxFlat)style.Duplicate();
        hover.BgColor = new Color(0.18f, 0.16f, 0.11f);
        b.AddThemeStyleboxOverride("normal", style);
        b.AddThemeStyleboxOverride("hover", hover);
        b.AddThemeStyleboxOverride("pressed", hover);
        b.Pressed += () => { Sound.Play("click"); onPress(); };
        return b;
    }

    private static Control MakeSlider(string name, float value, System.Action<float> set)
    {
        var row = new HBoxContainer();
        var label = new Label { Text = name, CustomMinimumSize = new Vector2(110, 0) };
        label.AddThemeColorOverride("font_color", Text);
        row.AddChild(label);
        var slider = new HSlider { MinValue = 0, MaxValue = 1, Step = 0.05, Value = value, CustomMinimumSize = new Vector2(340, 24) };
        slider.ValueChanged += v => { set((float)v); GameSettings.Apply(); };
        row.AddChild(slider);
        return row;
    }
}
