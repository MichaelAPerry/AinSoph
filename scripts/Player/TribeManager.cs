using AinSoph.NPC;
using AinSoph.World;
using Godot;

namespace AinSoph.Player;

/// <summary>
/// Manages the player's tribe — spouse creation, progeny birth, lineage.
///
/// Spouse: one ever. Standard NPC, decan from the 72, never leaves the sphere.
/// Progeny: 1–2 per real week after spouse exists. Standard NPCs, wander freely.
/// Lineage: append-only. Each progeny carries the player id as origin.
/// </summary>
public class TribeManager
{
    private readonly PlayerCharacter _player;
    private readonly List<NpcBrain>  _worldNpcs; // live NPC list, shared with world
    private DateTime _lastProgenyBirthUtc;

    private static readonly TimeSpan ProgenyInterval = TimeSpan.FromDays(7);

    public event Action<NpcBrain>?   OnSpouseCreated;
    public event Action<NpcBrain>?   OnProgenyBorn;

    public TribeManager(PlayerCharacter player, List<NpcBrain> worldNpcs, DateTime nowUtc)
    {
        _player              = player;
        _worldNpcs           = worldNpcs;
        _lastProgenyBirthUtc = nowUtc;
    }

    // -------------------------------------------------------------------------
    // Spouse creation — called once when the player uses the rib
    // -------------------------------------------------------------------------

    /// <summary>
    /// Create the spouse NPC. Player provides name and description.
    /// Decan drawn randomly from the 72.
    /// Spouse is a standard NPC — cannot leave the sphere.
    /// </summary>
    public NpcBrain? CreateSpouse(string name, string description,
        AinSoph.LLM.LlmRunner llm, DateTime nowUtc)
    {
        if (!_player.HasRib || _player.HasSpouse)
        {
            GD.PrintErr("TribeManager: cannot create spouse — rib not earned or spouse exists");
            return null;
        }

        var decan  = DecanRegistry.DrawRandom();
        var npcId  = $"spouse:{_player.Id}";

        var spouse = new NpcBrain(npcId, decan, llm, nowUtc);

        // Roll birth impairment
        var spouseData = new AinSoph.Data.NpcSaveData();
        AinSoph.NPC.BirthImpairment.Roll(spouseData);
        spouse.BrokenMove = spouseData.BrokenMove;
        spouse.BrokenSee  = spouseData.BrokenSee;
        spouse.BrokenHear = spouseData.BrokenHear;
        spouse.BrokenTalk = spouseData.BrokenTalk;

        // Seed the spouse's memory with the player's description of them
        spouse.Memory.Write(MemorySlot.Thought,
            $"I am {name}. {description}");
        spouse.Memory.Write(MemorySlot.Feeling,
            $"I belong to this sphere. I do not leave.");

        _player.ClaimRib(npcId);
        _worldNpcs.Add(spouse);

        OnSpouseCreated?.Invoke(spouse);
        GD.Print($"TribeManager: spouse created — {name} [{decan.Name}]");
        return spouse;
    }

    // -------------------------------------------------------------------------
    // Progeny birth — called from the weekly tick
    // -------------------------------------------------------------------------

    /// <summary>
    /// Tick the tribe. Called on the hourly world tick.
    /// Births 1–2 progeny per week if the spouse exists.
    /// </summary>
    public void Tick(AinSoph.LLM.LlmRunner llm, DateTime nowUtc)
    {
        if (!_player.HasSpouse) return;
        if ((nowUtc - _lastProgenyBirthUtc) < ProgenyInterval) return;

        _lastProgenyBirthUtc = nowUtc;

        var count = new Random().Next(1, 3); // 1 or 2
        for (var i = 0; i < count; i++)
            BirthProgeny(llm, nowUtc);
    }

    private void BirthProgeny(AinSoph.LLM.LlmRunner llm, DateTime nowUtc)
    {
        var decan = DecanRegistry.DrawRandom();
        var npcId = $"progeny:{_player.Id}:{Guid.NewGuid():N}";

        var progeny = new NpcBrain(npcId, decan, llm, nowUtc);

        // Roll birth impairment
        var progenyData = new AinSoph.Data.NpcSaveData();
        AinSoph.NPC.BirthImpairment.Roll(progenyData);
        progeny.BrokenMove = progenyData.BrokenMove;
        progeny.BrokenSee  = progenyData.BrokenSee;
        progeny.BrokenHear = progenyData.BrokenHear;
        progeny.BrokenTalk = progenyData.BrokenTalk;

        // Lineage — append-only origin record
        // Stored in action memory as the closest equivalent until a lineage field is added
        progeny.Memory.Write(MemorySlot.Action,
            $"Born of {_player.Id} on {nowUtc:yyyy-MM-dd}.");

        _player.AddProgeny(npcId);
        _worldNpcs.Add(progeny);

        OnProgenyBorn?.Invoke(progeny);
        GD.Print($"TribeManager: progeny born — [{decan.Name}] child of {_player.Name}");
    }

    // -------------------------------------------------------------------------
    // Intermarriage — called by RouteManager when migrants arrive
    // -------------------------------------------------------------------------

    /// <summary>
    /// Record an intermarriage between a local progeny and a migrant progeny.
    /// Appends both lineages to the descendant.
    /// </summary>
    public static void RecordIntermarriage(NpcBrain local, NpcBrain migrant,
        string localPlayerId, string migrantOrigin)
    {
        // Append both lineages to action memory
        var current = local.Memory.Action;
        var entry   = $" | intermarried:{migrantOrigin} on {DateTime.UtcNow:yyyy-MM-dd}";

        if ((current + entry).Length <= NpcMemory.MaxSlotLength)
            local.Memory.Write(MemorySlot.Action, current + entry);

        GD.Print($"TribeManager: intermarriage recorded — " +
                 $"{localPlayerId} lineage + {migrantOrigin}");
    }
}
