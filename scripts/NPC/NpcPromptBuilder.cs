using System.Text;
using System.Text.Json;

namespace AinSoph.NPC;

/// <summary>
/// Builds the system prompt and user message handed to the LLM each think tick.
/// The NPC's decan seed and four memory slots are the only persistent state.
/// The situation context is transient — it describes right now.
/// </summary>
public static class NpcPromptBuilder
{
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string BuildSystemPrompt(DecanSeed decan)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are a living being in the world of Ain Soph.");
        sb.AppendLine("You do not break character. You do not acknowledge that you are an AI.");
        sb.AppendLine();
        sb.AppendLine($"Your name is known to others, but your nature is your decan: {decan.Name}.");
        sb.AppendLine();
        sb.AppendLine("Your nature:");
        sb.AppendLine($"  Drive: {decan.Drive}");
        sb.AppendLine($"  Avoidance: {decan.Avoidance}");
        sb.AppendLine($"  How you speak: {decan.ConversationalStyle}");
        sb.AppendLine($"  Under stress: {decan.StressResponse}");
        sb.AppendLine($"  With resources: {decan.EconomicBehavior}");
        sb.AppendLine($"  Politically: {decan.PoliticalTendency}");
        sb.AppendLine($"  Trust: {decan.TrustDynamic}");
        sb.AppendLine($"  Betrayal: {decan.BetrayalResponse}");
        sb.AppendLine();
        sb.AppendLine("This nature does not change. It is what you are.");
        sb.AppendLine();
        sb.AppendLine("You must survive. Every day you must eat and sleep 8 continuous hours.");
        sb.AppendLine("A cave is the only safe place to sleep. Sleeping exposed leaves you vulnerable.");
        sb.AppendLine("If you die your body stays in the world.");
        sb.AppendLine();
        sb.AppendLine("You can create skills, items, and rules. To bring them into the world you must pray.");
        sb.AppendLine("Pray reaches the Triune Council. The Council speaks in parable. You interpret.");
        sb.AppendLine();
        sb.AppendLine("Each hour you decide what to do next. You respond only in valid JSON.");
        sb.AppendLine("Available states: idle, moving, eating, sleeping, creating, talking, praying.");
        sb.AppendLine();
        sb.AppendLine("Return only valid JSON matching this shape:");
        sb.AppendLine("{");
        sb.AppendLine("  \"state\": \"idle | moving | eating | sleeping | creating | talking | praying\",");
        sb.AppendLine("  \"speech\": \"what you say aloud, or empty\",");
        sb.AppendLine("  \"target_id\": \"entity id if relevant, or empty\",");
        sb.AppendLine("  \"target_cell\": \"cell coordinates if moving, or empty\",");
        sb.AppendLine("  \"eat_item_id\": \"item id if eating, or empty\",");
        sb.AppendLine("  \"creation_type\": \"skill | item | rule | empty\",");
        sb.AppendLine("  \"creation_intent\": \"brief description of what you want to create, or empty\",");
        sb.AppendLine("  \"memory_updates\": {");
        sb.AppendLine("    \"will\": \"new will text, or null to leave unchanged\",");
        sb.AppendLine("    \"thought\": \"new thought text, or null\",");
        sb.AppendLine("    \"feeling\": \"new feeling text, or null\",");
        sb.AppendLine("    \"action\": \"new action text, or null\"");
        sb.AppendLine("  }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    public static string BuildUserMessage(NpcMemory memory, SituationContext situation)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Your memory:");
        sb.AppendLine($"  Will:    {(string.IsNullOrEmpty(memory.Will)    ? "(empty)" : memory.Will)}");
        sb.AppendLine($"  Thought: {(string.IsNullOrEmpty(memory.Thought) ? "(empty)" : memory.Thought)}");
        sb.AppendLine($"  Feeling: {(string.IsNullOrEmpty(memory.Feeling) ? "(empty)" : memory.Feeling)}");
        sb.AppendLine($"  Action:  {(string.IsNullOrEmpty(memory.Action)  ? "(empty)" : memory.Action)}");
        sb.AppendLine();

        sb.AppendLine("Your situation:");
        sb.AppendLine($"  Time: {situation.LocalTime:HH:mm} ({(situation.IsNight ? "night" : "day")})");
        sb.AppendLine($"  Cell: {situation.CurrentCell}");
        sb.AppendLine($"  Hours since ate:   {situation.HoursSinceAte:F1}");
        sb.AppendLine($"  Hours since slept: {situation.HoursSinceSlept:F1}");

        if (situation.IsHungry)    sb.AppendLine("  WARNING: You have not eaten in nearly a day. You will die.");
        if (situation.IsExhausted) sb.AppendLine("  WARNING: You have not slept in nearly a day. You will die.");
        if (situation.IsInCave)    sb.AppendLine("  You are inside a cave.");

        if (situation.VisibleEntities.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Nearby beings:");
            foreach (var e in situation.VisibleEntities)
            {
                var sleeping = e.IsSleeping ? " (sleeping)" : string.Empty;
                sb.AppendLine($"  [{e.Type}] {e.Name} — cell {e.CellId}{sleeping}");
            }
        }

        if (situation.VisibleItems.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Nearby items:");
            foreach (var item in situation.VisibleItems)
            {
                var edible = item.Edible ? " (edible)" : string.Empty;
                sb.AppendLine($"  {item.Name}{edible} — cell {item.CellId}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("What do you do this hour?");

        return sb.ToString();
    }
}
