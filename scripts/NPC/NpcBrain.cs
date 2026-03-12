using System.Text.Json;
using AinSoph.LLM;
using AinSoph.World;
using Godot;

namespace AinSoph.NPC;

/// <summary>
/// Drives one NPC's autonomous behavior.
/// Every real hour, the NPC thinks — LLM receives decan + memory + situation,
/// returns a decision, and the brain applies it.
///
/// Sleeping NPCs skip the think tick and wake on the hour.
/// Memory is written only when the NPC decides to write it.
/// The brain does not force survival behavior — decan determines how the NPC responds.
/// </summary>
public class NpcBrain
{
    public string        NpcId     { get; }
    public DecanSeed     Decan     { get; }
    public NpcMemory     Memory    { get; } = new();
    public NpcState      State     { get; private set; } = NpcState.Idle;
    public SurvivalTracker Survival { get; }

    // Birth impairment — rolled once, never changes
    public bool BrokenMove  { get; set; }
    public bool BrokenSee   { get; set; }
    public bool BrokenHear  { get; set; }
    public bool BrokenTalk  { get; set; }

    // Foreigner — set on arrival via route, permanent
    public bool IsForeigner { get; set; }

    /// <summary>
    /// Roll birth impairment for a newly created NPC.
    /// Rates from SKILLS.md — real-world natural occurrence.
    /// Move: ~2.5/1000, See: ~0.4/1000, Hear: ~1/1000, Talk: ~0.1/1000.
    /// </summary>
    public static (bool move, bool see, bool hear, bool talk) RollBirthImpairment(Random rng)
    {
        return (
            move: rng.NextDouble() < 0.0025,
            see:  rng.NextDouble() < 0.0004,
            hear: rng.NextDouble() < 0.001,
            talk: rng.NextDouble() < 0.0001
        );
    }

    private string _cellId = string.Empty;
    public string CellId() => _cellId;
    public void SetCellId(string cellId) => _cellId = cellId;

    private readonly LlmRunner _llm;
    private DateTime _lastThinkUtc;
    private DateTime _sleepStartUtc;

    private static readonly TimeSpan ThinkInterval = TimeSpan.FromHours(1);

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Events the engine listens to
    public event Action<NpcBrain, NpcDecision>?       OnDecision;
    public event Action<NpcBrain, string>?             OnSpeech;
    public event Action<NpcBrain, SurvivalTickResult>? OnSurvivalEvent;
    public event Action<NpcBrain>?                     OnDeath;

    public NpcBrain(string npcId, DecanSeed decan, LlmRunner llm, DateTime nowUtc)
    {
        NpcId    = npcId;
        Decan    = decan;
        _llm     = llm;
        Survival = new SurvivalTracker(nowUtc);
        _lastThinkUtc = nowUtc;
    }

    // -------------------------------------------------------------------------
    // Tick — called by the engine on a regular interval (e.g. every real minute)
    // -------------------------------------------------------------------------

    public async Task TickAsync(SituationContext situation, CancellationToken ct = default)
    {
        var now = WorldClock.NowUtc;

        // Survival always ticks
        var survivalResult = Survival.Tick(now);
        if (survivalResult.HungerWarning || survivalResult.SleepWarning)
            OnSurvivalEvent?.Invoke(this, survivalResult);

        if (survivalResult.IsDead)
        {
            State = NpcState.Idle;
            OnDeath?.Invoke(this);
            return;
        }

        // Sleeping NPCs wake on the hour, otherwise skip
        if (State == NpcState.Sleeping)
        {
            var continuousSleep = (now - _sleepStartUtc).TotalHours;
            if (continuousSleep >= SurvivalTracker.SleepRequired)
            {
                Survival.EndSleep(now);
                State = NpcState.Idle;
                // Fall through to think
            }
            else
            {
                return; // Still sleeping — no think tick
            }
        }

        // Think once per hour
        if (now - _lastThinkUtc < ThinkInterval)
            return;

        _lastThinkUtc = now;
        await ThinkAsync(situation, now, ct);
    }

    // -------------------------------------------------------------------------
    // Think — calls the LLM, parses the decision, applies state
    // -------------------------------------------------------------------------

    private async Task ThinkAsync(SituationContext situation, DateTime now,
        CancellationToken ct)
    {
        var systemPrompt = NpcPromptBuilder.BuildSystemPrompt(Decan, BrokenMove, BrokenSee, BrokenHear, BrokenTalk, IsForeigner);
        var userMessage  = NpcPromptBuilder.BuildUserMessage(Memory, situation);

        string raw;
        try
        {
            raw = await _llm.InferAsync(systemPrompt, userMessage,
                maxTokens: 512, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"NpcBrain [{NpcId}]: LLM error: {ex.Message}");
            return;
        }

        var decision = ParseDecision(raw);
        if (decision is null)
        {
            GD.PrintErr($"NpcBrain [{NpcId}]: could not parse decision from: {raw}");
            return;
        }

        // Foreigner enforcement — engine layer, not just prompt layer
        // If a crafted memory slot tricks the LLM into returning a forbidden state, override it.
        if (IsForeigner)
        {
            if (decision.ParsedState == NpcState.Creating || decision.ParsedState == NpcState.Praying)
            {
                GD.Print($"NpcBrain [{NpcId}]: foreigner attempted {decision.ParsedState} — overridden to Idle");
                decision.ParsedState = NpcState.Idle;
                decision.CreationType = string.Empty;
                decision.CreationIntent = string.Empty;
            }
        }

        ApplyDecision(decision, now);
        OnDecision?.Invoke(this, decision);

        if (!string.IsNullOrWhiteSpace(decision.Speech))
            OnSpeech?.Invoke(this, decision.Speech);
    }

    // -------------------------------------------------------------------------
    // Apply — transition state based on the decision
    // -------------------------------------------------------------------------

    private void ApplyDecision(NpcDecision decision, DateTime now)
    {
        State = decision.ParsedState;

        switch (State)
        {
            case NpcState.Sleeping:
                _sleepStartUtc = now;
                Survival.BeginSleep(now, Survival.IsInCave);
                break;

            case NpcState.Eating:
                if (!string.IsNullOrEmpty(decision.EatItemId))
                    Survival.RecordEat(now);
                break;
        }

        // Write memory if the NPC decided to update it
        if (decision.MemoryUpdates is { } mu)
        {
            if (mu.Will    is not null) Memory.Write(MemorySlot.Will,    mu.Will);
            if (mu.Thought is not null) Memory.Write(MemorySlot.Thought, mu.Thought);
            if (mu.Feeling is not null) Memory.Write(MemorySlot.Feeling, mu.Feeling);
            if (mu.Action  is not null) Memory.Write(MemorySlot.Action,  mu.Action);
        }
    }

    // -------------------------------------------------------------------------
    // Dialogue — player interrupts the NPC mid-task
    // -------------------------------------------------------------------------

    /// <summary>
    /// Player initiates dialogue. NPC responds in character about what they are doing and why.
    /// Does not interrupt creation — the NPC responds while continuing.
    /// </summary>
    public async Task<string> RespondToDialogueAsync(string playerMessage,
        SituationContext situation, CancellationToken ct = default)
    {
        var systemPrompt = NpcPromptBuilder.BuildSystemPrompt(Decan, BrokenMove, BrokenSee, BrokenHear, BrokenTalk, IsForeigner);

        var context = NpcPromptBuilder.BuildUserMessage(Memory, situation);
        var currentActivity = State switch
        {
            NpcState.Creating => $"You are currently in the middle of creating something: {_pendingCreationIntent}. " +
                                  "You continue your work as you speak.",
            NpcState.Praying  => "You are praying to the Triune Council.",
            _                 => $"You are currently {State.ToString().ToLower()}."
        };

        var fullUserMessage =
            $"{context}\n\nYour current activity: {currentActivity}\n\n" +
            $"Someone speaks to you: \"{playerMessage}\"\n\n" +
            $"Respond in character. Speak as yourself. Return plain text — no JSON.";

        return await _llm.InferAsync(systemPrompt, fullUserMessage,
            maxTokens: 256, cancellationToken: ct);
    }

    private string _pendingCreationIntent = string.Empty;

    // -------------------------------------------------------------------------
    // Parse
    // -------------------------------------------------------------------------

    private static NpcDecision? ParseDecision(string raw)
    {
        var cleaned = raw.Trim();
        if (cleaned.StartsWith("```")) cleaned = cleaned.Split('\n', 2)[1];
        if (cleaned.EndsWith("```"))   cleaned = cleaned[..^3];

        try
        {
            var d = JsonSerializer.Deserialize<NpcDecision>(cleaned.Trim(), _jsonOpts);
            if (d is not null && d.ParsedState == NpcState.Creating)
                return d;
            return d;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
