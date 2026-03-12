using System.Collections.Generic;
using Godot;
using AinSoph.Player;

namespace AinSoph.UI
{
    /// <summary>
    /// Maps biomes and game concepts to Kenney tileset indices.
    /// Colored tiles live at res://assets/sprites/map/colored/tile_XXXX.png (8×8 source, displayed at 32×32).
    /// </summary>
    public static class TileRegistry
    {
        // ── Biome ground tiles ──────────────────────────────────────────────
        public static readonly int[] WildernessTiles  = { 20, 24, 26, 84 };
        public static readonly int[] DesertTiles       = { 112, 113, 114, 115 };
        public static readonly int[] RiverTiles        = { 110, 111, 126, 127 };
        public static readonly int[] SeaTiles          = { 139, 140, 155, 156, 157 };
        public static readonly int[] ForestTiles       = { 84, 85, 86, 129 };
        public static readonly int[] GroveTiles        = { 24, 26, 129, 84 };
        public static readonly int[] MountainTiles     = { 0, 1, 2, 32, 33, 34 };
        public static readonly int[] ValleyTiles       = { 144, 145, 147, 148 };

        // ── Landmarks ───────────────────────────────────────────────────────
        public const int CaveTile   = 17;
        public const int AltarTile  = 94;
        public const int MannaTile  = 95;

        // ── Entity icons (floating state above NPC/player) ──────────────────
        public const int StateIdle     = 20;
        public const int StateMoving   = 3;
        public const int StateEating   = 7;
        public const int StateSleeping = 17;
        public const int StateCreating = 92;
        public const int StateTalking  = 107;
        public const int StatePraying  = 94;

        // ── Fog of war overlays ─────────────────────────────────────────────
        public static readonly Color FogColor   = new Color(0, 0, 0, 1f);
        public static readonly Color EdgeColor  = new Color(0, 0, 0, 0.55f);
        public static readonly Color ClearColor = new Color(1, 1, 1, 1f);

        // ── Helpers ─────────────────────────────────────────────────────────
        public static int[] GroundTilesFor(World.BiomeType biome) => biome switch
        {
            World.BiomeType.Wilderness => WildernessTiles,
            World.BiomeType.Desert     => DesertTiles,
            World.BiomeType.River      => RiverTiles,
            World.BiomeType.Sea        => SeaTiles,
            World.BiomeType.Forest     => ForestTiles,
            World.BiomeType.Grove      => GroveTiles,
            World.BiomeType.Mountain   => MountainTiles,
            World.BiomeType.Valley     => ValleyTiles,
            _                          => WildernessTiles
        };

        public static string TilePath(int index) =>
            $"res://assets/sprites/map/colored/tile_{index:D4}.png";

        public static int StateIconFor(NPC.NpcState state) => state switch
        {
            NPC.NpcState.Idle     => StateIdle,
            NPC.NpcState.Moving   => StateMoving,
            NPC.NpcState.Eating   => StateEating,
            NPC.NpcState.Sleeping => StateSleeping,
            NPC.NpcState.Creating => StateCreating,
            NPC.NpcState.Talking  => StateTalking,
            NPC.NpcState.Praying  => StatePraying,
            _                     => StateIdle
        };
    }
}
