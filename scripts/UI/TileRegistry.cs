using System.Collections.Generic;
using Godot;
using AinSoph.Player;
using AinSoph.World;

namespace AinSoph.UI
{
    /// <summary>
    /// Maps biomes and game concepts to Kenney 1-bit tileset indices.
    /// Colored tiles live at res://assets/sprites/map/colored/tile_XXXX.png (8×8 source, displayed at 32×32).
    ///
    /// The pack has no ground tiles — every glyph sits on the same dark
    /// background. The renderer swaps that background for a per-biome ground
    /// colour (see GroundShader), so biomes read as terrain, not as glyphs.
    /// </summary>
    public static class TileRegistry
    {
        // ── Ground ──────────────────────────────────────────────────────────
        public const int BlankTile = 17;   // only fully-background tile in the pack

        public static Color GroundColorFor(BiomeType biome) => biome switch
        {
            BiomeType.Wilderness => new Color(0.36f, 0.33f, 0.22f),
            BiomeType.Desert     => new Color(0.70f, 0.58f, 0.36f),
            BiomeType.River      => new Color(0.24f, 0.42f, 0.22f),
            BiomeType.Sea        => new Color(0.16f, 0.22f, 0.45f),
            BiomeType.Forest     => new Color(0.15f, 0.28f, 0.15f),
            BiomeType.Grove      => new Color(0.30f, 0.42f, 0.20f),
            BiomeType.Mountain   => new Color(0.36f, 0.36f, 0.39f),
            BiomeType.Valley     => new Color(0.30f, 0.48f, 0.24f),
            _                    => new Color(0.36f, 0.33f, 0.22f),
        };

        /// <summary>Glyph drawn on a tile for its surface, or BlankTile for bare ground.</summary>
        public static int SurfaceTile(TileSurface surface, int variant) => surface switch
        {
            TileSurface.Water     => (variant & 1) == 0 ? 126 : 127,
            TileSurface.TreeCedar => 84,
            TileSurface.TreeOlive => 85,
            TileSurface.TreeFig   => 86,
            TileSurface.TreePalm  => 87,
            TileSurface.Rock      => variant % 3 == 0 ? 53 : BlankTile,
            TileSurface.Stone     => variant % 4 == 0 ? 52 : BlankTile,
            TileSurface.Grass     => variant % 5 == 0 ? 69 : BlankTile,
            _                     => BlankTile,
        };

        // ── Landmarks ───────────────────────────────────────────────────────
        public const int CaveTile   = 34;   // stone archway
        public const int AltarTile  = 120;  // standing cross
        public const int MannaTile  = 56;
        public const int BodyTile   = 121;  // headstone

        // ── Entities ────────────────────────────────────────────────────────
        public const int PlayerTile = 7;
        public static readonly int[] NpcTiles    = { 4, 5, 6, 8, 14, 15 };
        public static readonly int[] AnimalTiles = { 20, 21, 23, 25, 26 };

        // ── Primitive skill icons (HUD + primitive menu) ────────────────────
        public static int SkillIcon(Skills.SkillType skill) => skill switch
        {
            Skills.SkillType.Move => 99,   // arrow
            Skills.SkillType.See  => 13,   // eye
            Skills.SkillType.Hear => 116,  // bell
            Skills.SkillType.Talk => 63,   // speech bubble
            Skills.SkillType.Reap => 43,   // scythe
            Skills.SkillType.Pray => 89,   // halo
            _                     => BlankTile,
        };

        // ── Entity icons (floating state above NPC/player) ──────────────────
        public const int StateIdle     = BlankTile;
        public const int StateMoving   = 99;
        public const int StateEating   = MannaTile;
        public const int StateSleeping = 130;
        public const int StateCreating = 131;
        public const int StateTalking  = 63;
        public const int StatePraying  = 89;

        // ── Fog of war overlays ─────────────────────────────────────────────
        public static readonly Color FogColor   = new Color(0.04f, 0.04f, 0.05f, 1f);
        public static readonly Color EdgeColor  = new Color(0.45f, 0.45f, 0.50f, 1f);
        public static readonly Color ClearColor = new Color(1, 1, 1, 1f);

        // ── Helpers ─────────────────────────────────────────────────────────
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

        /// <summary>Hash that is stable across runs (string.GetHashCode is not).</summary>
        public static int StableHash(string s)
        {
            unchecked
            {
                int h = 17;
                foreach (var c in s) h = h * 31 + c;
                return h & 0x7fffffff;
            }
        }

        // ── Ground shader ───────────────────────────────────────────────────
        // Replaces the pack's dark background with the biome ground colour.

        private const string GroundShaderCode = @"
shader_type canvas_item;
uniform vec4 ground = vec4(0.36, 0.33, 0.22, 1.0);
varying vec4 tint;
void vertex() { tint = COLOR; } // node modulate (fog)
void fragment() {
    vec4 c = texture(TEXTURE, UV);
    if (distance(c.rgb, vec3(34.0, 35.0, 35.0) / 255.0) < 0.02) c = ground;
    COLOR = c * tint;
}";

        private static Shader? _groundShader;
        private static readonly Dictionary<BiomeType, ShaderMaterial> _groundMaterials = new();

        private const string CutoutShaderCode = @"
shader_type canvas_item;
void fragment() {
    if (distance(texture(TEXTURE, UV).rgb, vec3(34.0, 35.0, 35.0) / 255.0) < 0.02) discard;
}";

        private static ShaderMaterial? _cutout;

        /// <summary>Material that makes the pack's background transparent — for entities and icons.</summary>
        public static ShaderMaterial CutoutMaterial =>
            _cutout ??= new ShaderMaterial { Shader = new Shader { Code = CutoutShaderCode } };

        // One material for every ground tile. The sprite's modulate carries the
        // tile's own (blended) ground colour in RGB and its fog light in alpha,
        // so biome borders can feather tile by tile without a material per tile.
        private const string BlendedGroundShaderCode = @"
shader_type canvas_item;
varying vec4 tint;
void vertex() { tint = COLOR; }
void fragment() {
    vec4 c = texture(TEXTURE, UV);
    if (distance(c.rgb, vec3(34.0, 35.0, 35.0) / 255.0) < 0.02)
        COLOR = vec4(tint.rgb, 1.0);          // ground: blended colour, already lit
    else
        COLOR = vec4(c.rgb * tint.a, c.a);    // glyph: its own colour, lit
}";

        private static ShaderMaterial? _blendedGround;

        public static ShaderMaterial BlendedGroundMaterial =>
            _blendedGround ??= new ShaderMaterial { Shader = new Shader { Code = BlendedGroundShaderCode } };

        public static ShaderMaterial GroundMaterialFor(BiomeType biome)
        {
            if (_groundMaterials.TryGetValue(biome, out var mat)) return mat;

            _groundShader ??= new Shader { Code = GroundShaderCode };
            mat = new ShaderMaterial { Shader = _groundShader };
            mat.SetShaderParameter("ground", GroundColorFor(biome));
            _groundMaterials[biome] = mat;
            return mat;
        }
    }
}
