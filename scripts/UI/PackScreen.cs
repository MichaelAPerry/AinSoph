using System;
using System.Collections.Generic;
using System.Linq;
using AinSoph.Data;
using AinSoph.Skills;
using Godot;

namespace AinSoph.UI
{
    /// <summary>
    /// What you carry: eat it, give it to whoever is beside you, or set it down.
    /// Opened with the PACK button or I; Esc or I closes it. The world keeps running.
    /// </summary>
    public partial class PackScreen : CanvasLayer
    {
        private readonly Func<List<CarriedItem>> _items;
        private readonly Func<string?> _beside;
        private readonly Action<CarriedItem> _eat, _give, _drop;
        private readonly Action _close;
        private VBoxContainer _rows = null!;
        private Label _title = null!, _hint = null!;

        private static readonly Color Gold  = new(1f, 0.85f, 0.45f);
        private static readonly Color Text  = new(0.85f, 0.83f, 0.76f);
        private static readonly Color Muted = new(0.55f, 0.53f, 0.46f);

        public PackScreen(Func<List<CarriedItem>> items, Func<string?> beside,
                          Action<CarriedItem> eat, Action<CarriedItem> give, Action<CarriedItem> drop, Action close)
        {
            _items = items; _beside = beside; _eat = eat; _give = give; _drop = drop; _close = close;
        }

        public override void _Ready()
        {
            Layer = 28;
            var vp = GetViewport().GetVisibleRect().Size;
            AddChild(new ColorRect { Color = new Color(0, 0, 0, 0.55f), Size = vp });

            var panel = new PanelContainer { Size = new Vector2(560, 0), Position = new Vector2(vp.X / 2 - 280, 110) };
            var style = new StyleBoxFlat { BgColor = new Color(0.07f, 0.07f, 0.06f, 0.97f), BorderColor = new Color(0.55f, 0.45f, 0.2f) };
            style.SetBorderWidthAll(1);
            style.SetContentMarginAll(16);
            panel.AddThemeStyleboxOverride("panel", style);
            AddChild(panel);

            var box = new VBoxContainer();
            box.AddThemeConstantOverride("separation", 8);
            panel.AddChild(box);

            _title = Label("", 16, Gold);
            box.AddChild(_title);
            _rows = new VBoxContainer();
            _rows.AddThemeConstantOverride("separation", 6);
            box.AddChild(_rows);
            _hint = Label("", 12, Muted);
            _hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            box.AddChild(_hint);

            var close = new Button { Text = "CLOSE  (I / Esc)" };
            close.Pressed += _close;
            box.AddChild(close);
            Refresh();
        }

        public void Refresh()
        {
            if (_rows == null) return;
            foreach (Node child in _rows.GetChildren()) child.QueueFree();
            var items = _items();
            var beside = _beside();
            _title.Text = $"WHAT YOU CARRY  ({items.Count}/{Player.PlayerCharacter.MaxCarried})";
            _hint.Text = (items.Count == 0 ? "You carry nothing. Use Move on a thing beside you to pick it up. " : "") +
                         (beside != null ? $"{beside} is beside you." : "No one is beside you to give to.") +
                         " Food spoils here as it would on the ground. The world does not pause while this is open.";

            foreach (var c in items)
            {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 8);

                var effect = Gifts.Classify($"{c.Item.Name} {c.Item.Description}");
                var what = c.Item.Edible ? SpoilsIn(c)
                         : c.Item.Type is "crafted" or "relic" or "provision" && effect != GiftEffect.Lore ? Gifts.Describe(effect)
                         : c.Item.Description;
                var name = Label(c.Item.Name, 14, Text);
                name.CustomMinimumSize = new Vector2(150, 0);
                row.AddChild(name);
                var detail = Label(what, 11, Muted);
                detail.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                detail.ClipText = true;
                row.AddChild(detail);

                if (c.Item.Edible) row.AddChild(Button("EAT", () => _eat(c)));
                var give = Button("GIVE", () => _give(c));
                give.Disabled = beside == null;
                row.AddChild(give);
                row.AddChild(Button("DROP", () => _drop(c)));
                _rows.AddChild(row);
            }
        }

        private static string SpoilsIn(CarriedItem c)
        {
            if (c.Item.LifespanHours is not { } life) return "food";
            var left = life - (c.Item.AgeHours ?? 0) - (DateTime.UtcNow - c.PickedUpUtc).TotalHours;
            return left < 1 ? "food — spoils within the hour" : $"food — spoils in {left:0} hours";
        }

        public override void _Input(InputEvent ev)
        {
            if (ev is InputEventKey { Pressed: true, Echo: false } key && key.Keycode is Key.Escape or Key.I)
            {
                GetViewport().SetInputAsHandled();
                _close();
            }
        }

        private static Label Label(string text, int size, Color colour)
        {
            var l = new Label { Text = text };
            l.AddThemeFontSizeOverride("font_size", size);
            l.AddThemeColorOverride("font_color", colour);
            return l;
        }

        private static Button Button(string text, Action onPress)
        {
            var b = new Button { Text = text, CustomMinimumSize = new Vector2(58, 26) };
            b.AddThemeFontSizeOverride("font_size", 10);
            b.Pressed += () => { AinSoph.Audio.Sound.Play("click"); onPress(); };
            return b;
        }
    }
}
