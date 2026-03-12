using AinSoph.Council;
using AinSoph.NPC;
using AinSoph.Player;
using AinSoph.World;
using Godot;

namespace AinSoph.Skills;

public enum InteractionTarget { Item, Npc, Animal, Altar, Tile }

public class InteractionRequest
{
    public string            ActorId      { get; set; } = string.Empty;
    public string            Primitive    { get; set; } = string.Empty; // PrimitiveSkills constant
    public InteractionTarget TargetType   { get; set; }
    public string            TargetId     { get; set; } = string.Empty;
    public string            TargetName   { get; set; } = string.Empty;
    public string?           PrayText     { get; set; } // only used for Pray at altar
}

public class InteractionResult
{
    public bool   Success     { get; set; }
    public string WorldText   { get; set; } = string.Empty; // shown to the player
    public bool   IsDialogue  { get; set; }
    public bool   IsCouncil   { get; set; }
    public CouncilVerdict? Verdict { get; set; }
}

/// <summary>
/// Resolves any primitive applied to any target.
/// The engine calls this when the player selects a primitive from the interaction menu.
///
/// Nonsensical combinations produce environmental and oblique responses.
/// The world notices. Nothing useful occurs.
/// </summary>
public class InteractionResolver
{
    private readonly WorldGrid      _grid;
    private readonly Altar?         _altar;
    private readonly CouncilSubmissionParser _parser;

    private static readonly string[] ObliqueResponses = new[]
    {
        "The air does not answer.",
        "Nothing stirs.",
        "The world holds its shape.",
        "You reach. The distance remains.",
        "Something watches. Nothing moves.",
        "The ground remembers nothing of this.",
        "Silence, which is its own kind of answer.",
        "The light does not change.",
        "Whatever you meant, the world heard something else.",
    };

    public InteractionResolver(WorldGrid grid, Altar? altar,
        CouncilSubmissionParser parser)
    {
        _grid   = grid;
        _altar  = altar;
        _parser = parser;
    }

    public async Task<InteractionResult> ResolveAsync(InteractionRequest req,
        CancellationToken ct = default)
    {
        return req.Primitive switch
        {
            PrimitiveSkills.Move  => ResolveMove(req),
            PrimitiveSkills.See   => ResolveSee(req),
            PrimitiveSkills.Hear  => ResolveHear(req),
            PrimitiveSkills.Talk  => ResolveTalk(req),
            PrimitiveSkills.Kill  => ResolveKill(req),
            PrimitiveSkills.Pray  => await ResolvePrayAsync(req, ct),
            _                     => Oblique()
        };
    }

    // -------------------------------------------------------------------------
    // Move
    // -------------------------------------------------------------------------

    private InteractionResult ResolveMove(InteractionRequest req)
    {
        if (req.TargetType == InteractionTarget.Tile)
            return new InteractionResult { Success = true,
                WorldText = string.Empty }; // movement handled by engine

        return Oblique();
    }

    // -------------------------------------------------------------------------
    // See
    // -------------------------------------------------------------------------

    private InteractionResult ResolveSee(InteractionRequest req)
    {
        // See used on an item or entity — describe what is visible
        return new InteractionResult
        {
            Success   = true,
            WorldText = $"You look at the {req.TargetName}."
        };
    }

    // -------------------------------------------------------------------------
    // Hear
    // -------------------------------------------------------------------------

    private InteractionResult ResolveHear(InteractionRequest req)
    {
        return req.TargetType switch
        {
            InteractionTarget.Animal => new InteractionResult { Success = true,
                WorldText = $"The {req.TargetName} makes a sound." },
            InteractionTarget.Npc   => new InteractionResult { Success = true,
                WorldText = $"You listen." },
            _                       => Oblique()
        };
    }

    // -------------------------------------------------------------------------
    // Talk
    // -------------------------------------------------------------------------

    private InteractionResult ResolveTalk(InteractionRequest req)
    {
        if (req.TargetType == InteractionTarget.Npc)
            return new InteractionResult { Success = true, IsDialogue = true,
                WorldText = string.Empty }; // dialogue handled by NpcBrain

        if (req.TargetType == InteractionTarget.Item)
        {
            // Manna and items don't talk — but the world notices
            return new InteractionResult
            {
                Success   = false,
                WorldText = PickOblique()
            };
        }

        if (req.TargetType == InteractionTarget.Altar)
            return new InteractionResult
            {
                Success   = false,
                WorldText = "The altar does not speak. It waits."
            };

        return Oblique();
    }

    // -------------------------------------------------------------------------
    // Kill
    // -------------------------------------------------------------------------

    private InteractionResult ResolveKill(InteractionRequest req)
    {
        if (req.TargetType == InteractionTarget.Npc ||
            req.TargetType == InteractionTarget.Animal)
            return new InteractionResult { Success = true,
                WorldText = string.Empty }; // kill roll handled by KillResolver

        if (req.TargetType == InteractionTarget.Item)
            return new InteractionResult
            {
                Success   = false,
                WorldText = PickOblique()
            };

        return Oblique();
    }

    // -------------------------------------------------------------------------
    // Pray
    // -------------------------------------------------------------------------

    private async Task<InteractionResult> ResolvePrayAsync(InteractionRequest req,
        CancellationToken ct)
    {
        // Pray anywhere — but only at the altar does it reach the Council
        if (req.TargetType != InteractionTarget.Altar || _altar is null)
        {
            return new InteractionResult
            {
                Success   = false,
                WorldText = "You pray. The world does not move."
            };
        }

        if (string.IsNullOrWhiteSpace(req.PrayText))
            return new InteractionResult
            {
                Success   = false,
                WorldText = "You stand at the altar. Nothing is offered."
            };

        // Parse the free-form prayer into a Council submission
        var submission = _parser.Parse(req.PrayText);
        if (submission is null)
        {
            // Doesn't map to a valid submission — Council rejects in parable
            // The format is never revealed
            return new InteractionResult
            {
                Success    = false,
                IsCouncil  = true,
                WorldText  = "You pray. The Council hears something shapeless. " +
                             "Nothing enters the world."
            };
        }

        return new InteractionResult
        {
            Success   = true,
            IsCouncil = true,
            WorldText = string.Empty // Council verdict delivered separately
        };
    }

    // -------------------------------------------------------------------------
    // Oblique
    // -------------------------------------------------------------------------

    private static InteractionResult Oblique() =>
        new() { Success = false, WorldText = PickOblique() };

    private static string PickOblique() =>
        ObliqueResponses[new Random().Next(ObliqueResponses.Length)];
}
