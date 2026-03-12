using Godot;
using System;
using System.Collections.Generic;
using AinSoph.NPC;

namespace AinSoph.UI
{
    /// <summary>
    /// Builds a full-body character portrait by layering Kenney modular PNG parts.
    /// Each character's appearance is deterministically derived from their DecanSeed id.
    /// The composite is rendered into a SubViewportContainer for display on DialogueScreen.
    ///
    /// Layer order (back to front):
    ///   Skin body parts → Pants → Shirt → Shoes → Face (head/neck) → Hair → Eyebrows → Eyes → Nose → Mouth
    /// </summary>
    public partial class CharacterPortraitBuilder : Node2D
    {
        // Portrait dimensions (displayed at 4× the Kenney source size)
        public const int Scale = 4;

        // Skin tints available (Tint 1–8)
        private static readonly int SkinTintCount = 8;

        // Hair colors available
        private static readonly string[] HairColors =
            { "black", "blonde", "brown1", "brown2", "grey", "red", "tan", "white" };

        // Shirt/pants/shoe color names in the asset folders
        private static readonly string[] ShirtColors =
            { "blue", "green", "grey", "navy", "pine", "red", "white", "yellow" };
        private static readonly string[] PantsColors =
            { "Blue 1", "Blue 2", "Brown", "Green", "Grey", "Light Blue", "Navy", "Pine", "Red", "Tan", "White", "Yellow" };
        private static readonly string[] ShoeColors =
            { "Black", "Blue", "Brown 1", "Brown 2", "Grey", "Red", "Tan" };
        private static readonly string[] EyeColors =
            { "Black", "Blue", "Brown", "Green", "Pine" };

        private List<Sprite2D> _layers = new();

        /// <summary>
        /// Builds and returns a Node2D containing all portrait layers.
        /// Caller adds this to their scene tree and positions it.
        /// </summary>
        public static Node2D Build(int decanId, int seed, bool isPlayer = false)
        {
            var root = new Node2D();
            var rng  = new System.Random(seed ^ (decanId * 7919));

            int skinTint = (Math.Abs(seed) % SkinTintCount) + 1;
            string tintFolder = $"Tint {skinTint}";
            string tintPrefix = $"tint{skinTint}";

            string hairColor  = HairColors[Math.Abs(seed >> 3)  % HairColors.Length];
            bool   isWoman    = (seed & 1) == 1;
            string hairSuffix = isWoman ? "Woman" : "Man";
            int    hairStyle  = (Math.Abs(seed >> 6)  % (isWoman ? 6 : 8)) + 1;

            string shirtColor = ShirtColors[Math.Abs(seed >> 9)  % ShirtColors.Length];
            string pantsColor = PantsColors[Math.Abs(seed >> 12) % PantsColors.Length];
            string shoeColor  = ShoeColors[Math.Abs(seed >> 15)  % ShoeColors.Length];
            string eyeColor   = EyeColors[Math.Abs(seed >> 18)   % EyeColors.Length];
            int    noseStyle  = (Math.Abs(seed >> 21) % 3) + 1;
            string[] mouths   = { "glad", "happy", "oh", "sad", "straight", "teethLower", "teethUpper" };
            string mouthStyle = mouths[Math.Abs(seed >> 24) % mouths.Length];
            int    browStyle  = (Math.Abs(seed >> 27) % 3) + 1;

            string charBase   = "res://assets/sprites/characters/PNG";
            string shirtFolder = shirtColor.Substring(0, 1).ToUpper() + shirtColor.Substring(1);
            string hairFolder  = hairColor.Substring(0, 1).ToUpper() + hairColor.Substring(1);
            if (hairColor == "brown1") hairFolder = "Brown 1";
            if (hairColor == "brown2") hairFolder = "Brown 2";

            // ── Skin body ──
            AddLayer(root, $"{charBase}/Skin/{tintFolder}/{tintPrefix}_leg.png",   new Vector2(16, 80), Scale);
            AddLayer(root, $"{charBase}/Skin/{tintFolder}/{tintPrefix}_arm.png",   new Vector2(0, 40),  Scale);
            AddLayer(root, $"{charBase}/Skin/{tintFolder}/{tintPrefix}_hand.png",  new Vector2(0, 70),  Scale);
            AddLayer(root, $"{charBase}/Skin/{tintFolder}/{tintPrefix}_neck.png",  new Vector2(16, 30), Scale);

            // ── Pants ──
            AddLayer(root, $"{charBase}/Pants/{pantsColor}/{GetPantsFile(pantsColor, 1)}", new Vector2(8, 75), Scale);

            // ── Shirt ──
            string shirtFile = GetShirtFile(shirtColor, (Math.Abs(seed) % 8) + 1);
            AddLayer(root, $"{charBase}/Shirts/{shirtFolder}/{shirtFile}", new Vector2(4, 35), Scale);

            // ── Shoes ──
            AddLayer(root, $"{charBase}/Shoes/{shoeColor}/{shoeColor.Replace(" ", "")}Shoe1.png", new Vector2(8, 100), Scale);

            // ── Head ──
            AddLayer(root, $"{charBase}/Skin/{tintFolder}/{tintPrefix}_head.png", new Vector2(8, 4), Scale);

            // ── Hair ──
            string hairFile = $"{hairColor}{hairSuffix}{hairStyle}.png";
            // Normalize file name casing to match asset names
            hairFile = NormalizeHairFileName(hairColor, hairSuffix, hairStyle);
            AddLayer(root, $"{charBase}/Hair/{hairFolder}/{hairFile}", new Vector2(4, 0), Scale);

            // ── Face parts ──
            string browColor = hairColor.Replace("1", "").Replace("2", "");
            AddLayer(root, $"{charBase}/Face/Eyebrows/{browColor}Brow{browStyle}.png",  new Vector2(10, 12), Scale);
            AddLayer(root, $"{charBase}/Face/Eyes/eye{eyeColor}_large.png",              new Vector2(10, 16), Scale);
            AddLayer(root, $"{charBase}/Face/Nose/{tintFolder}/{tintPrefix}Nose{noseStyle}.png", new Vector2(14, 19), Scale);
            AddLayer(root, $"{charBase}/Face/Mouth/mouth_{mouthStyle}.png",              new Vector2(12, 23), Scale);

            return root;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static void AddLayer(Node2D root, string path, Vector2 offset, int scale)
        {
            var sprite = new Sprite2D();
            sprite.Centered = false;
            sprite.Position = offset * scale;
            sprite.Scale    = Vector2.One * scale;

            // Load texture; if missing, skip silently
            if (ResourceLoader.Exists(path))
                sprite.Texture = GD.Load<Texture2D>(path);

            root.AddChild(sprite);
        }

        private static string GetPantsFile(string color, int style)
        {
            // Pants have varying file name prefixes per color folder
            string c = color.Replace(" ", "").Replace("1", "1").Replace("2", "2");
            string prefix = color switch
            {
                "Yellow" => "pantsYellow",
                "Light Blue" => "pantsLightBlue",
                _ => "pants" + color.Replace(" ", "")
            };
            return $"{prefix}{style}.png";
        }

        private static string GetShirtFile(string color, int style)
        {
            string prefix = color switch
            {
                "yellow" => "shirtYellow",
                "white"  => "whiteShirt",
                _        => color + "Shirt"
            };
            return $"{prefix}{style}.png";
        }

        private static string NormalizeHairFileName(string color, string suffix, int style)
        {
            // Asset files use camelCase: e.g. blackMan1.png, brown1Man3.png
            return $"{color}{suffix}{style}.png";
        }
    }
}
