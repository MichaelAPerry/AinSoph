namespace AinSoph.Skills;

public enum SkillType
{
    Primitive,   // Born with it. Cannot be created.
    Composite,   // Built from two or more existing skills.
    Substitute,  // Replaces a broken or absent primitive.
    Extension    // Amplifies an existing primitive beyond its base.
}

/// <summary>
/// A skill is anything a character can do.
/// Primitives exist at world start. Everything else passes through the Triune Council.
/// </summary>
public class Skill
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public SkillType Type { get; set; }
    public List<string> BaseSkillIds { get; set; } = new();
    public string CreatedBy { get; set; } = string.Empty; // entity id, empty for primitives
    public SkillCost Cost { get; set; } = new();
}

public class SkillCost
{
    public float? TimeHours { get; set; }
    public List<string> ItemsConsumed { get; set; } = new(); // item ids
    public string Notes { get; set; } = string.Empty;
}

/// <summary>The six primitive skills every PC and NPC is born with.</summary>
public static class PrimitiveSkills
{
    public const string Move  = "primitive.move";
    public const string See   = "primitive.see";
    public const string Hear  = "primitive.hear";
    public const string Talk  = "primitive.talk";
    public const string Kill  = "primitive.kill";
    public const string Pray  = "primitive.pray";

    public static IReadOnlyList<string> All { get; } = new[]
    {
        Move, See, Hear, Talk, Kill, Pray
    };
}
