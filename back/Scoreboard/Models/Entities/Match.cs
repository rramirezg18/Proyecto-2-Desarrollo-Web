namespace Scoreboard.Models.Entities;

public class Match
{
    public int Id { get; set; }

    public int HomeTeamId { get; set; }
    public int AwayTeamId { get; set; }

    public string Status { get; set; } = "Scheduled";

    // Duración del período en segundos configurable por partido
    public int QuarterDurationSeconds { get; set; } = 600;

    public int HomeScore { get; set; }
    public int AwayScore { get; set; }


    public int Period { get; set; } = 1;        // 1..4
    public DateTime DateMatch { get; set; } = DateTime.Now;


    public Team HomeTeam { get; set; } = null!;
    public Team AwayTeam { get; set; } = null!;

    public ICollection<ScoreEvent> ScoreEvents { get; set; } = new List<ScoreEvent>();
}
