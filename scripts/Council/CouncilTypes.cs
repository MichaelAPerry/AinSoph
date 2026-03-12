namespace AinSoph.Council;

/// <summary>
/// A submission sent to the Triune Council.
/// Every skill, item, or rule created by a player or NPC passes through this.
/// </summary>
public class CouncilSubmission
{
    public string Type { get; set; } = string.Empty;      // "skill" | "item" | "rule"
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty; // entity id
    public List<string> BaseSkills { get; set; } = new();
    public SubmissionCost Cost { get; set; } = new();
    public Dictionary<string, object> Properties { get; set; } = new();
}

public class SubmissionCost
{
    public float? TimeHours { get; set; }
    public List<string> ItemsConsumed { get; set; } = new();
    public string Notes { get; set; } = string.Empty;
}

/// <summary>
/// Response from one council seat.
/// The council speaks in homily. Never in technical terms.
/// </summary>
public class SeatResponse
{
    public string Seat { get; set; } = string.Empty; // "skills" | "items" | "rules"
    public string Vote { get; set; } = string.Empty; // "yes" | "no"
    public string Homily { get; set; } = string.Empty;

    public bool Passed => Vote.Equals("yes", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The resolved verdict after all three seats have voted.
/// Pass condition: 2 of 3 yes votes.
/// </summary>
public class CouncilVerdict
{
    public CouncilSubmission Submission { get; set; } = new();
    public List<SeatResponse> Responses { get; set; } = new();

    public bool Approved => Responses.Count(r => r.Passed) >= 2;

    public int YesCount => Responses.Count(r => r.Passed);
    public int NoCount  => Responses.Count(r => !r.Passed);
}
