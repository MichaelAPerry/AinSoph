namespace AinSoph.Skills;

/// <summary>Taxonomy of a skill — how it was created and what it builds on.</summary>
public enum SkillKind
{
    Primitive,   // Born with it. Cannot be created.
    Composite,   // Built from two or more existing skills.
    Substitute,  // Replaces a broken or absent primitive.
    Extension    // Amplifies an existing primitive beyond its base.
}

/// <summary>The six primitives every PC and NPC is born with.</summary>
public enum SkillType
{
    Move,
    See,
    Hear,
    Talk,
    Reap,   // Covers killing and consuming. See SKILLS.md.
    Pray
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
    public SkillKind Kind { get; set; }
    public List<string> BaseSkillIds { get; set; } = new();
    public string CreatedBy { get; set; } = string.Empty;
    public SkillCost Cost { get; set; } = new();
}

public class SkillCost
{
    public float? TimeHours { get; set; }
    public List<string> ItemsConsumed { get; set; } = new();
    public string Notes { get; set; } = string.Empty;
}

/// <summary>String ids for the six primitive skills — used in save data and prompts.</summary>
public static class PrimitiveSkills
{
    public const string Move  = "primitive.move";
    public const string See   = "primitive.see";
    public const string Hear  = "primitive.hear";
    public const string Talk  = "primitive.talk";
    public const string Reap  = "primitive.reap";
    public const string Pray  = "primitive.pray";

    public static IReadOnlyList<string> All { get; } = new[]
    {
        Move, See, Hear, Talk, Reap, Pray
    };

    public static string FromSkillType(SkillType s) => s switch
    {
        SkillType.Move => Move,
        SkillType.See  => See,
        SkillType.Hear => Hear,
        SkillType.Talk => Talk,
        SkillType.Reap => Reap,
        SkillType.Pray => Pray,
        _              => s.ToString().ToLower()
    };
}
