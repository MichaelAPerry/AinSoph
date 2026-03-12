namespace AinSoph.NPC;

/// <summary>
/// One of the 72 personality seeds.
/// Loaded from data/ain_soph_72.json at startup.
/// The decan is the NPC's nature — it does not change over their life.
/// </summary>
public class DecanSeed
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int KillModifier { get; set; } = 0;

    // Personality dimensions that inform LLM prompting
    public string Drive { get; set; } = string.Empty;
    public string Avoidance { get; set; } = string.Empty;
    public string ConversationalStyle { get; set; } = string.Empty;
    public string StressResponse { get; set; } = string.Empty;
    public string EconomicBehavior { get; set; } = string.Empty;
    public string PoliticalTendency { get; set; } = string.Empty;
    public string TrustDynamic { get; set; } = string.Empty;
    public string BetrayalResponse { get; set; } = string.Empty;
}
