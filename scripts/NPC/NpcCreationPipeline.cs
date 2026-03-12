using AinSoph.Council;
using Godot;

namespace AinSoph.NPC;

/// <summary>
/// Handles the full NPC creation loop:
/// 1. NPC decides to create (state = Creating, creation_intent set)
/// 2. NPC transitions to Praying to submit to the Council
/// 3. Council votes — 2/3 passes
/// 4. On pass: content enters the world
/// 5. On fail: NPC moves on. The decan determines how they carry it.
///
/// The player can observe from any distance.
/// The player can interrupt and initiate dialogue at any point.
/// </summary>
public class NpcCreationPipeline
{
    private readonly TribuneCouncil _council;

    // Events the engine listens to for display
    public event Action<NpcBrain, string>?          OnCreationBegun;
    public event Action<NpcBrain, CouncilVerdict>?  OnCouncilVerdict;
    public event Action<NpcBrain, CouncilVerdict>?  OnCreationApproved;
    public event Action<NpcBrain, CouncilVerdict>?  OnCreationRejected;

    public NpcCreationPipeline(TribuneCouncil council)
    {
        _council = council;
    }

    /// <summary>
    /// Called when an NPC's brain transitions into Creating state.
    /// Builds the submission from the NPC's stated intent and runs it through the Council.
    /// </summary>
    public async Task RunAsync(NpcBrain npc, NpcDecision decision,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(decision.CreationIntent))
        {
            GD.PrintErr($"NpcCreationPipeline [{npc.NpcId}]: creation state with no intent");
            return;
        }

        var intent = decision.CreationIntent;
        var type   = string.IsNullOrWhiteSpace(decision.CreationType)
            ? "item"
            : decision.CreationType;

        GD.Print($"NpcCreationPipeline [{npc.NpcId}]: beginning creation — {type}: {intent}");
        OnCreationBegun?.Invoke(npc, intent);

        // Build a council submission from the NPC's stated intent
        var submission = new CouncilSubmission
        {
            Type        = type,
            Name        = intent, // The NPC named it in their intent
            Description = intent,
            CreatedBy   = npc.NpcId,
            BaseSkills  = new List<string>(),
            Cost        = new SubmissionCost()
        };

        // NPC transitions to Praying to reach the Council
        GD.Print($"NpcCreationPipeline [{npc.NpcId}]: praying to the Council");

        CouncilVerdict verdict;
        try
        {
            verdict = await _council.SubmitAsync(submission, ct);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"NpcCreationPipeline [{npc.NpcId}]: Council error: {ex.Message}");
            return;
        }

        OnCouncilVerdict?.Invoke(npc, verdict);

        if (verdict.Approved)
        {
            GD.Print($"NpcCreationPipeline [{npc.NpcId}]: APPROVED — {intent}");
            OnCreationApproved?.Invoke(npc, verdict);
        }
        else
        {
            GD.Print($"NpcCreationPipeline [{npc.NpcId}]: REJECTED — {intent}. NPC moves on.");
            OnCreationRejected?.Invoke(npc, verdict);
            // The NPC's decan determines how they carry the rejection.
            // The engine writes the homiilies to the NPC's visible status.
            // The NPC's next think tick will decide how to proceed.
        }
    }
}
