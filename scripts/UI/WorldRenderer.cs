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
        public Vector2I   AltarTile   { get; set; } // tile within the altar cell (0–7)
        public WorldItemRegistry? Items { get; set; }

        // Camera follows the player
        private Camera2D _camera;

        // Tile pools: ground glyph + landmark/item overlay, keyed by world tile
        private readonly Dictionary<Vector2I, Sprite2D> _pool    = new();
        private readonly Dictionary<Vector2I, Sprite2D> _overlay = new();
        private readonly Dictionary<int, Texture2D>     _texCache = new();

        private Dictionary<string, FogOfWar.TileVisibility> _fog = new();

        /// <summary>Whether a tile can be seen from the last refresh (not under full fog).</summary>
        public bool TileVisible(int tileX, int tileY)
        {
            var c = TileToCell(new Vector2I(tileX, tileY));
            return _fog.TryGetValue($"{c.X},{c.Y}", out var v) && v != FogOfWar.TileVisibility.Fog;
        }

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
            _fog = fogLookup;

            // Items by tile, for the overlay pass
            var itemTiles = new Dictionary<Vector2I, int>();
            if (Items != null)
            {
                foreach (var item in Items.All)
                {
                    var pos  = new Vector2I(item.TileX, item.TileY);
                    var tile = item.Type == "body" ? TileRegistry.BodyTile : TileRegistry.MannaTile;
                    if (!itemTiles.ContainsKey(pos) || tile == TileRegistry.BodyTile)
                        itemTiles[pos] = tile;
                }
            }

            // Mark all pooled sprites as unused
            foreach (var s in _pool.Values)    s.Visible = false;
            foreach (var s in _overlay.Values) s.Visible = false;

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
                    DrawCell(cell, vis, isAltar, itemTiles);
                }
            }

            // Move camera to player world position
            if (_camera != null)
                _camera.GlobalPosition = TileToWorld(playerTile) + new Vector2(TileSize / 2f, TileSize / 2f);
        }

        // ── Private helpers ──────────────────────────────────────────────────

        private void DrawCell(WorldCell cell, FogOfWar.TileVisibility vis, bool isAltar,
                              Dictionary<Vector2I, int> itemTiles)
        {
            var material = TileRegistry.GroundMaterialFor(cell.Biome);
            var modulate = FogModulate(vis);

            for (int tx = 0; tx < CellTiles; tx++)
            {
                for (int ty = 0; ty < CellTiles; ty++)
                {
                    var tilePos = new Vector2I(
                        cell.GridX * CellTiles + tx,
                        cell.GridY * CellTiles + ty
                    );
                    var tile = cell.GetTile(tx, ty);

                    // Ground: the tile's surface glyph on the biome's ground colour
                    int variant = Math.Abs(tilePos.X * 31 + tilePos.Y * 17);
                    var sprite  = GetOrCreate(_pool, tilePos, z: 0);
                    sprite.Texture  = LoadTile(TileRegistry.SurfaceTile(tile.Surface, variant));
                    sprite.Material = material;
                    sprite.Modulate = modulate;
                    sprite.Visible  = true;

                    // Overlay: altar > cave > items. Hidden under full fog.
                    int overlay = -1;
                    if (isAltar && tx == AltarTile.X && ty == AltarTile.Y)
                        overlay = TileRegistry.AltarTile;
                    else if (tile.HasCave)
                        overlay = TileRegistry.CaveTile;
                    else if (vis != FogOfWar.TileVisibility.Fog && itemTiles.TryGetValue(tilePos, out var it))
                        overlay = it;

                    if (overlay >= 0)
                    {
                        var o = GetOrCreate(_overlay, tilePos, z: 1);
                        o.Texture  = LoadTile(overlay);
                        o.Material = material;
                        o.Modulate = modulate;
                        o.Visible  = true;
                    }
                }
            }
        }

        private static Color FogModulate(FogOfWar.TileVisibility vis) => vis switch
        {
            FogOfWar.TileVisibility.Clear => TileRegistry.ClearColor,
            FogOfWar.TileVisibility.Edge  => TileRegistry.EdgeColor,
            _                             => TileRegistry.FogColor,
        };

        private Sprite2D GetOrCreate(Dictionary<Vector2I, Sprite2D> pool, Vector2I tilePos, int z)
        {
            if (pool.TryGetValue(tilePos, out var existing))
                return existing;

            var sprite = new Sprite2D();
            sprite.Centered = false;
            sprite.Position = TileToWorld(tilePos);
            sprite.Scale    = Vector2.One * (TileSize / 8f); // source tiles are 8px
            sprite.ZIndex   = z;
            AddChild(sprite);
            pool[tilePos] = sprite;
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
