using Godot;
using AinSoph.Audio;

namespace AinSoph.UI;

/// <summary>
/// First-time guidance. One hint at a time; each moves on when the player has
/// done the thing it describes. Progress is kept in settings, so a returning
/// player is not taught twice. Turn off in the Esc menu.
/// </summary>
public partial class Hints : CanvasLayer
{
    private PanelContainer _panel = null!;
    private Label _label = null!;
    private int _step;
    private double _timeOnStep;
    private Vector2I _startTile;
    private DateTime _startAte;
    private bool _sawDialogue;

    private record Hint(string Text, Func<Hints, bool> Done);

    private static readonly Hint[] Steps =
    {
        new("The world does not pause. Hunger and sleep run on real hours — even while you are away.",
            h => h._timeOnStep > 9),
        new("Walk with WASD, the arrow keys, or by left-clicking the ground.",
            h => h.Moved() >= 4),
        new("Left-click someone to see the six primitives. Choose TALK to speak with them.",
            h => h._sawDialogue),
        new("Eat once every 24 real hours. Right-click manna — the small red shapes — and choose REAP.",
            h => GameRoot.Player is { } p && p.Survival.LastAteUtc > h._startAte),
        new("Sleep 8 real hours a day. Only a cave — the stone arch — is safe. Step inside, then press SLEEP.",
            h => GameRoot.Player?.Survival.IsInCave == true || h._timeOnStep > 30),
        new("Somewhere lies one altar, unmarked. Stand beside it and PRAY — the Council answers in parable.",
            h => h._timeOnStep > 15),
    };

    public override void _Ready()
    {
        Layer = 15;
        _step = GameSettings.HintsSeen;
        Build();
        BeginStep();
    }

    public override void _Process(double delta)
    {
        var scene = GameRoot.Scene;
        bool active = GameSettings.ShowHints && _step < Steps.Length && GameRoot.Player != null &&
                      !string.IsNullOrEmpty(GameRoot.Player.Name) && scene != null && !scene.InputLocked;
        _panel.Visible = active && !(scene!.MenuOpen);
        if (!active) return;

        if (scene.DialogueOpen) _sawDialogue = true;
        _timeOnStep += delta;
        if (Steps[_step].Done(this))
        {
            _step++;
            GameSettings.HintsSeen = _step;
            GameSettings.Save();
            if (_step < Steps.Length) { Sound.Play("click"); BeginStep(); }
        }
    }

    private void BeginStep()
    {
        if (_step >= Steps.Length) return;
        _timeOnStep = 0;
        _startTile  = GameRoot.Scene?.PlayerTile ?? Vector2I.Zero;
        _startAte   = GameRoot.Player?.Survival.LastAteUtc ?? DateTime.UtcNow;
        _label.Text = $"{Steps[_step].Text}\n({_step + 1} of {Steps.Length} · hints can be turned off in the Esc menu)";
    }

    private int Moved()
    {
        var now = GameRoot.Scene?.PlayerTile ?? _startTile;
        return Math.Abs(now.X - _startTile.X) + Math.Abs(now.Y - _startTile.Y);
    }

    private void Build()
    {
        var vp = GetViewport().GetVisibleRect().Size;
        _panel = new PanelContainer { Position = new Vector2(vp.X / 2 - 330, 56), CustomMinimumSize = new Vector2(660, 0) };
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.05f, 0.06f, 0.88f), BorderColor = new Color(0.55f, 0.47f, 0.28f),
            ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 8, ContentMarginBottom = 8,
        };
        style.SetBorderWidthAll(1);
        _panel.AddThemeStyleboxOverride("panel", style);
        AddChild(_panel);

        _label = new Label { HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _label.AddThemeFontSizeOverride("font_size", 14);
        _label.AddThemeColorOverride("font_color", new Color(0.92f, 0.88f, 0.74f));
        _panel.AddChild(_label);
    }
}
