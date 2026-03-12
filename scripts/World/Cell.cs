namespace AinSoph.World;

/// <summary>
/// A sovereign cell in the grid.
/// Cells are peer equals — no hierarchy.
/// A cell is 256x256 units (~meters). Crossing on foot: ~38 real minutes.
/// </summary>
public class Cell
{
    public Vector2I Coordinates { get; set; } // grid position
    public string BiomeId { get; set; } = string.Empty;
    public bool Generated { get; set; }

    // Contents — populated on generation
    public List<string> ItemIds { get; set; } = new();
    public List<string> NpcIds  { get; set; } = new();

    public const int SizeUnits = 256; // 1 unit ≈ 1 meter
}

// Godot doesn't expose Vector2I outside the engine namespace in pure C# data classes,
// so we define a lightweight equivalent for use in data/serialization contexts.
public readonly struct Vector2I(int x, int y)
{
    public int X { get; } = x;
    public int Y { get; } = y;

    public override string ToString() => $"({X}, {Y})";
}
