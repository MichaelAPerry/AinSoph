using Godot;
using System;
using System.Collections.Generic;
using AinSoph.Player;
using AinSoph.World;

namespace AinSoph.UI
{
    /// <summary>
    /// Renders the visible world as a 32×32 tile grid.
    /// Pulls live data from WorldGrid; applies fog-of-war tinting.
    /// Tiles are pooled Sprite2D children — never instantiated per-frame.
    ///
    /// Layout: each cell is 8×8 tiles × 32px = 256×256px.
    /// Viewport shows a window of cells centred on the player.
    /// </summary>
    public partial class WorldRenderer : Node2D
    {
        [Export] public int TileSize    = 32;
        [Export] public int CellTiles   = 8;       // tiles per cell side
        [Export] public int ViewRadius  = 4;       // cells to render around player

        // Injected by GameRoot
        public WorldGrid  Grid        { get; set; }
        public WorldClock Clock       { get; set; }
        public string     AltarCellId { get; set; } // e.g. "3,-2"

        // Camera follows the player
        private Camera2D _camera;

        // Tile pool: keyed by grid position string for fast lookup
        private readonly Dictionary<Vector2I, Sprite2D> _pool = new();
        private readonly Dictionary<int, Texture2D>     _texCache = new();

        public override void _Ready()
        {
            _camera = GetNode<Camera2D>("../Camera2D");
        }

        /// <summary>Called by GameRoot whenever the player moves or the clock ticks.</summary>
        public void Refresh(Vector2I playerTile)
        {
            if (Grid == null) return;

            var playerCell = TileToCell(playerTile);
            bool isNight   = Clock != null && WorldClock.IsNight();

            // Calculate fog for all cells in view range
            var fogMap = FogOfWar.Calculate(playerCell.X, playerCell.Y, isNight, Grid);
            var fogLookup = new Dictionary<string, FogOfWar.TileVisibility>();
            foreach (var f in fogMap)
                fogLookup[$"{f.GridX},{f.GridY}"] = f.Visibility;

            // Mark all pooled sprites as unused
            foreach (var s in _pool.Values) s.Visible = false;

            // Render cells in view
            for (int cx = playerCell.X - ViewRadius; cx <= playerCell.X + ViewRadius; cx++)
            {
                for (int cy = playerCell.Y - ViewRadius; cy <= playerCell.Y + ViewRadius; cy++)
                {
                    var cell = Grid.GetOrGenerate(cx, cy);
                    var vis  = fogLookup.TryGetValue($"{cx},{cy}", out var v)
                               ? v
                               : FogOfWar.TileVisibility.Fog;

                    bool isAltar = AltarCellId == cell.CellId;
                    DrawCell(cell, vis, isAltar);
                }
            }

            // Move camera to player world position
            if (_camera != null)
                _camera.GlobalPosition = TileToWorld(playerTile);
        }

        // ── Private helpers ──────────────────────────────────────────────────

        private void DrawCell(WorldCell cell, FogOfWar.TileVisibility vis, bool isAltar)
        {
            int[] groundTiles = TileRegistry.GroundTilesFor(cell.Biome);

            for (int tx = 0; tx < CellTiles; tx++)
            {
                for (int ty = 0; ty < CellTiles; ty++)
                {
                    var tilePos = new Vector2I(
                        cell.GridX * CellTiles + tx,
                        cell.GridY * CellTiles + ty
                    );

                    // Pick tile variant deterministically
                    int variant  = Math.Abs(tilePos.X * 31 + tilePos.Y * 17) % groundTiles.Length;
                    int tileIdx  = groundTiles[variant];

                    // Cave tile override (centre of cell)
                    if (cell.HasCave && tx == 3 && ty == 3)
                        tileIdx = TileRegistry.CaveTile;

                    // Altar tile override
                    if (isAltar && tx == 4 && ty == 4)
                        tileIdx = TileRegistry.AltarTile;

                    var sprite = GetOrCreateTile(tilePos);
                    sprite.Texture  = LoadTile(tileIdx);
                    sprite.Position = TileToWorld(tilePos);
                    sprite.Scale    = Vector2.One * (TileSize / 8f); // source tiles are 8px
                    sprite.Modulate = FogModulate(vis);
                    sprite.Visible  = true;
                }
            }
        }

        private static Color FogModulate(FogOfWar.TileVisibility vis) => vis switch
        {
            FogOfWar.TileVisibility.Clear => TileRegistry.ClearColor,
            FogOfWar.TileVisibility.Edge  => TileRegistry.EdgeColor,
            _                             => TileRegistry.FogColor,
        };

        private Sprite2D GetOrCreateTile(Vector2I tilePos)
        {
            if (_pool.TryGetValue(tilePos, out var existing))
                return existing;

            var sprite = new Sprite2D();
            sprite.Centered = false;
            AddChild(sprite);
            _pool[tilePos] = sprite;
            return sprite;
        }

        private Texture2D LoadTile(int index)
        {
            if (_texCache.TryGetValue(index, out var cached))
                return cached;

            var tex = GD.Load<Texture2D>(TileRegistry.TilePath(index));
            _texCache[index] = tex;
            return tex;
        }

        private Vector2 TileToWorld(Vector2I tile) =>
            new Vector2(tile.X * TileSize, tile.Y * TileSize);

        private Vector2I TileToCell(Vector2I tile) =>
            new Vector2I(
                tile.X < 0 ? (tile.X - CellTiles + 1) / CellTiles : tile.X / CellTiles,
                tile.Y < 0 ? (tile.Y - CellTiles + 1) / CellTiles : tile.Y / CellTiles
            );
    }
}
