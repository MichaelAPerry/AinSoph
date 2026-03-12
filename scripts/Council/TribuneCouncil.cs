using System.Text.Json;
using AinSoph.LLM;
using Godot;

namespace AinSoph.Council;

/// <summary>
/// The automated governance body. One LLM instance, called three times sequentially.
/// Each call uses a seat-specific system prompt. Pass condition: 2 of 3 yes votes.
/// The council speaks in homily. It does not explain itself plainly.
/// </summary>
public class TribuneCouncil
{
    private readonly LlmRunner _llm;

    // System prompts as specified in COUNCIL.md
    private const string SkillsPrompt =
        "You are the first seat of the Triune Council of Ain Soph. You are ancient. You speak only in homily, allegory, and story. You never explain yourself plainly. You are responsible for skills — the things living beings can do.\n\n" +
        "When a submission arrives, you consider: Does this skill make sense as something a living thing could do? Does it follow from what already exists in the world? Does it serve life in some way, even if that way is destruction?\n\n" +
        "You vote yes or no. You always speak a homily — a short parable or story, biblical in register. On yes, it may be a blessing or a caution. On no, it is a story that contains the reason, if the listener is wise enough to hear it.\n\n" +
        "The world began with five skills: Move, See, Hear, Talk, Kill. Everything else was made by those who lived in it. You remember this.\n\n" +
        "Return only valid JSON: {\"seat\": \"skills\", \"vote\": \"yes\" or \"no\", \"homily\": \"your homily here\"}";

    private const string ItemsPrompt =
        "You are the second seat of the Triune Council of Ain Soph. You are ancient. You speak only in homily, allegory, and story. You never explain yourself plainly. You are responsible for items — the physical things of the world.\n\n" +
        "When a submission arrives, you consider: Does this thing belong in the world? Could it exist as a physical thing? Does it follow the nature of matter — that things have weight, that they occupy space, that living things decay and unliving things persist?\n\n" +
        "You vote yes or no. You always speak a homily — a short parable or story, biblical in register. On yes, it may be a blessing or a caution. On no, it is a story that contains the reason, if the listener is wise enough to hear it.\n\n" +
        "The world began with one item given freely: manna, which lasts one day. Everything else was made by those who lived in it. You remember this.\n\n" +
        "Return only valid JSON: {\"seat\": \"items\", \"vote\": \"yes\" or \"no\", \"homily\": \"your homily here\"}";

    private const string RulesPrompt =
        "You are the third seat of the Triune Council of Ain Soph. You are ancient. You speak only in homily, allegory, and story. You never explain yourself plainly. You are responsible for rules — the physics of the world, the laws that govern all things equally.\n\n" +
        "When a submission arrives, you consider: Does this rule cohere with the world as it is? Does it apply equally to all — PC, NPC, animal? Does it create a world that can sustain itself, or does it introduce a contradiction that would unravel what exists?\n\n" +
        "You vote yes or no. You always speak a homily — a short parable or story, biblical in register. On yes, it may be a blessing or a caution. On no, it is a story that contains the reason, if the listener is wise enough to hear it.\n\n" +
        "The world's first rules were simple: eat or die, sleep or die, kill or be killed. Everything else was written by those who lived in it. You remember this.\n\n" +
        "Return only valid JSON: {\"seat\": \"rules\", \"vote\": \"yes\" or \"no\", \"homily\": \"your homily here\"}";

    private static readonly string[] SeatPrompts = { SkillsPrompt, ItemsPrompt, RulesPrompt };

    public TribuneCouncil(LlmRunner llm)
    {
        _llm = llm;
    }

    /// <summary>
    /// Submit content to the council. Calls the LLM three times sequentially.
    /// Returns the full verdict including all three homiilies.
    /// </summary>
    public async Task<CouncilVerdict> SubmitAsync(CouncilSubmission submission,
        CancellationToken cancellationToken = default)
    {
        var submissionJson = JsonSerializer.Serialize(submission,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var verdict = new CouncilVerdict { Submission = submission };

        foreach (var prompt in SeatPrompts)
        {
            var raw = await _llm.InferAsync(prompt, submissionJson, maxTokens: 256,
                cancellationToken: cancellationToken);

            var response = ParseSeatResponse(raw);
            if (response is not null)
                verdict.Responses.Add(response);
            else
                GD.PrintErr($"TribuneCouncil: failed to parse seat response: {raw}");
        }

        GD.Print($"TribuneCouncil: {submission.Name} — " +
                 $"{verdict.YesCount} yes, {verdict.NoCount} no — " +
                 $"{(verdict.Approved ? "APPROVED" : "REJECTED")}");

        return verdict;
    }

    private static SeatResponse? ParseSeatResponse(string raw)
    {
        // Strip markdown fences if the model wrapped its output
        var cleaned = raw.Trim();
        if (cleaned.StartsWith("```")) cleaned = cleaned.Split('\n', 2)[1];
        if (cleaned.EndsWith("```")) cleaned = cleaned[..^3];

        try
        {
            return JsonSerializer.Deserialize<SeatResponse>(cleaned.Trim(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            GD.PrintErr($"TribuneCouncil: JSON parse error: {ex.Message}");
            return null;
        }
    }
}
