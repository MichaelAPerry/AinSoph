using System;
using System.Collections.Generic;
using AinSoph.World;
using Godot;

namespace AinSoph.UI
{
    /// <summary>
    /// The map (MAP or M): the land this life has seen, cell by cell, in the colour of
    /// its ground — with you, the altar once found (or a ring where the messenger said
    /// it lies), caves you have passed, your people and anything that hunts. Unseen land
    /// stays dark. The world keeps running while it is open.
    /// </summary>
    public partial class MapScreen : CanvasLayer
    {
        public record Marker(int TileX, int TileY, Color Colour, float Size, bool Ring = false);

        private readonly Func<IReadOnlyCollection<(int X, int Y)>> _explored;
        private readonly Func<WorldGrid?> _grid;
        private readonly Func<List<Marker>> _markers;
        private readonly Func<(int X, int Y)> _centre;
        private readonly Action _close;
        private MapView _view = null!;

        private const int Radius = 12;     // cells shown each way from you
        private const float Cell = 20f;    // pixels per cell

        public MapScreen(Func<IReadOnlyCollection<(int X, int Y)>> explored, Func<WorldGrid?> grid,
                         Func<List<Marker>> markers, Func<(int X, int Y)> centre, Action close)
        {
            _explored = explored; _grid = grid; _markers = markers; _centre = centre; _close = close;
        }

        public override void _Ready()
        {
            Layer = 27;
            var vp = GetViewport().GetVisibleRect().Size;
            AddChild(new ColorRect { Color = new Color(0, 0, 0, 0.78f), Size = vp });

            var size = (Radius * 2 + 1) * Cell;
            _view = new MapView(this) { Position = new Vector2((vp.X - size) / 2, 70), Size = new Vector2(size, size) };
            AddChild(_view);

            var title = new Label { Text = "WHAT YOU HAVE SEEN", Position = new Vector2(0, 30), Size = new Vector2(vp.X, 24),
                                    HorizontalAlignment = HorizontalAlignment.Center };
            title.AddThemeFontSizeOverride("font_size", 16);
            title.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.45f));
            AddChild(title);

            var legend = new RichTextLabel
            {
                BbcodeEnabled = true, FitContent = true, ScrollActive = false,
                Position = new Vector2(0, 70 + size + 10), Size = new Vector2(vp.X, 22),
                Text = "[center][color=#ffd966]●[/color] you    [color=#bfccff]●[/color] the altar ([color=#bfccff]○[/color] where the messenger said it lies)    " +
                       "[color=#e6e6e6]∩[/color] a cave    [color=#73e680]●[/color] your people    [color=#d9d1b3]●[/color] others    " +
                       "[color=#f24d40]●[/color] hunters    —    M or Esc to close[/center]",
            };
            legend.AddThemeFontSizeOverride("normal_font_size", 11);
            legend.AddThemeColorOverride("default_color", new Color(0.7f, 0.68f, 0.6f));
            AddChild(legend);
        }

        public override void _Process(double delta) => _view?.QueueRedraw(); // the world keeps moving

        public override void _Input(InputEvent ev)
        {
            if (ev is InputEventKey { Pressed: true, Echo: false } key && key.Keycode is Key.Escape or Key.M)
            {
                GetViewport().SetInputAsHandled();
                _close();
            }
        }

        private partial class MapView : Control
        {
            private readonly MapScreen _map;
            public MapView(MapScreen map) { _map = map; MouseFilter = MouseFilterEnum.Ignore; }

            public override void _Draw()
            {
                var grid = _map._grid();
                if (grid == null) return;
                var (px, py) = _map._centre();
                int pcx = px < 0 ? (px - 7) / 8 : px / 8, pcy = py < 0 ? (py - 7) / 8 : py / 8;
                var explored = _map._explored();
                var seen = new HashSet<(int, int)>(explored);

                DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.05f, 0.05f, 0.06f));
                for (int dx = -Radius; dx <= Radius; dx++)
                for (int dy = -Radius; dy <= Radius; dy++)
                {
                    int cx = pcx + dx, cy = pcy + dy;
                    if (!seen.Contains((cx, cy))) continue;
                    var cell = grid.GetOrGenerate(cx, cy);
                    var origin = new Vector2((dx + Radius) * Cell, (dy + Radius) * Cell);
                    DrawRect(new Rect2(origin, new Vector2(Cell - 1, Cell - 1)), TileRegistry.GroundColorFor(cell.Biome));
                    if (cell.HasCave)
                        DrawArc(origin + new Vector2(Cell / 2, Cell * 0.7f), Cell * 0.22f, Mathf.Pi, Mathf.Tau, 8,
                                new Color(0.9f, 0.9f, 0.9f), 1.5f);
                }

                foreach (var m in _map._markers())
                {
                    var pos = new Vector2((m.TileX / 8f - pcx + Radius) * Cell, (m.TileY / 8f - pcy + Radius) * Cell);
                    if (pos.X < 0 || pos.Y < 0 || pos.X > Size.X || pos.Y > Size.Y) continue;
                    if (m.Ring) DrawArc(pos, m.Size, 0, Mathf.Tau, 24, m.Colour, 2f);
                    else        DrawCircle(pos, m.Size, m.Colour);
                }
            }
        }
    }
}
