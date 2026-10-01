using Godot;
using System;
using System.Collections.Generic;
using AinSoph.NPC;

namespace AinSoph.UI
{
    /// <summary>
    /// Builds a head-and-shoulders character portrait by layering Kenney modular PNG parts.
    /// Each character's appearance is deterministically derived from their DecanSeed id.
    /// The composite is rendered into a SubViewportContainer for display on DialogueScreen.
    ///
    /// The parts are ~170px illustrations (not pixel art). The bust is laid out in
    /// source pixels — 240 wide, ~330 tall, origin at top-centre — then scaled.
    ///
    /// Layer order (back to front):
    ///   Shirt → Neck → Head → Eyebrows → Eyes → Nose → Mouth → Hair
    /// </summary>
    public partial class CharacterPortraitBuilder : Node2D
    {
        // Bust is ~240×330 source px; this fits it in the dialogue portrait box
        public const float PortraitScale = 0.8f;
        public const float PortraitWidth  = 240f * PortraitScale;
        public const float PortraitHeight = 330f * PortraitScale;

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

            root.Scale = Vector2.One * PortraitScale;

            // ── Body ──
            string shirtFile = GetShirtFile(shirtColor, (Math.Abs(seed) % 8) + 1);
            AddLayer(root, $"{charBase}/Shirts/{shirtFolder}/{shirtFile}",            0, 170);
            AddLayer(root, $"{charBase}/Skin/{tintFolder}/{tintPrefix}_neck.png",     0, 150);
            AddLayer(root, $"{charBase}/Skin/{tintFolder}/{tintPrefix}_head.png",     0, 8);

            // ── Face ──
            string brow = $"{charBase}/Face/Eyebrows/{hairColor}Brow{browStyle}.png";
            string eye  = $"{charBase}/Face/Eyes/eye{eyeColor}_large.png";
            AddLayer(root, brow, -30, 62);
            AddLayer(root, brow,  30, 62, flipH: true);
            AddLayer(root, eye,  -30, 82);
            AddLayer(root, eye,   30, 82);
            AddLayer(root, $"{charBase}/Face/Nose/{tintFolder}/{tintPrefix}Nose{noseStyle}.png", 0, 104);
            AddLayer(root, $"{charBase}/Face/Mouth/mouth_{mouthStyle}.png",           0, 132);

            // ── Hair ──
            AddLayer(root, $"{charBase}/Hair/{hairFolder}/{NormalizeHairFileName(hairColor, hairSuffix, hairStyle)}", 0, 0);

            return root;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>Add a part horizontally centred on x, with its top edge at y (source px).</summary>
        private static void AddLayer(Node2D root, string path, float x, float y, bool flipH = false)
        {
            // Load texture; if missing, skip silently
            if (!ResourceLoader.Exists(path)) return;

            var tex    = GD.Load<Texture2D>(path);
            var sprite = new Sprite2D();
            sprite.Centered      = false;
            sprite.Texture       = tex;
            sprite.FlipH         = flipH;
            sprite.TextureFilter = TextureFilterEnum.Linear; // illustrations, not pixel art
            sprite.Position      = new Vector2(x - tex.GetWidth() / 2f, y);
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
