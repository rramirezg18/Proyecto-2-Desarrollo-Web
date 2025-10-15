using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Scoreboard.Hubs;
using Scoreboard.Infrastructure;
using Scoreboard.Models.DTOs;

using MatchEntity      = Scoreboard.Models.Entities.Match;
using TeamEntity       = Scoreboard.Models.Entities.Team;
using ScoreEventEntity = Scoreboard.Models.Entities.ScoreEvent;
using FoulEntity       = Scoreboard.Models.Entities.Foul;
using TeamWinEntity    = Scoreboard.Models.Entities.TeamWin;

namespace Scoreboard.Controllers;

[ApiController]
[Route("api/matches")]
public class MatchesController(AppDbContext db, IHubContext<ScoreHub> hub, IMatchRunTime rt) : ControllerBase
{
    public record ProgramarPartidoDto(int HomeTeamId, int AwayTeamId, DateTime DateMatchUtc, int? QuarterDurationSeconds);
    public record ReprogramarDto(DateTime NewDateMatchUtc);
    public record StartTimerDto(int? QuarterDurationSeconds);

    public record ScoreEventItem(int TeamId, int? PlayerId, int Points, DateTime DateRegister);
    public record FoulItem(int TeamId, int? PlayerId, DateTime DateRegister);
    public record FinishMatchDto(
        int HomeScore, int AwayScore, int HomeFouls, int AwayFouls,
        List<ScoreEventItem>? ScoreEvents, List<FoulItem>? Fouls);


    [HttpGet]
    [HttpGet("list")]
    public async Task<IActionResult> Listar(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] int? teamId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0 || pageSize > 200) pageSize = 20;

        var q = db.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(m => m.Status == status);

        if (teamId is int tid && tid > 0)
            q = q.Where(m => m.HomeTeamId == tid || m.AwayTeamId == tid);

        if (from is DateTime f)
            q = q.Where(m => m.DateMatch >= DateTime.SpecifyKind(f, DateTimeKind.Utc));

        if (to is DateTime t)
            q = q.Where(m => m.DateMatch <= DateTime.SpecifyKind(t, DateTimeKind.Utc));

        var total = await q.CountAsync();

        var items = await q
            .OrderByDescending(m => m.DateMatch)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new
            {
                id = m.Id,
                dateMatchUtc = m.DateMatch,
                status = m.Status,
                homeTeamId = m.HomeTeamId,
                awayTeamId = m.AwayTeamId,
                homeTeam = m.HomeTeam.Name,
                awayTeam = m.AwayTeam.Name,
                homeScore = m.HomeScore,
                awayScore = m.AwayScore,
                quarter = m.Period,
                quarterDurationSeconds = m.QuarterDurationSeconds,
                // totales de faltas por equipo
                homeFouls = db.Fouls.Count(f => f.MatchId == m.Id && f.TeamId == m.HomeTeamId),
                awayFouls = db.Fouls.Count(f => f.MatchId == m.Id && f.TeamId == m.AwayTeamId)
            })
            .ToListAsync();

        return Ok(new { items, total, page, pageSize });
    }


    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var m = await db.Matches
            .Include(x => x.HomeTeam)
            .Include(x => x.AwayTeam)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (m is null) return NotFound();

        var snap = rt.GetOrCreate(id, m.QuarterDurationSeconds);

        var homeFouls = await db.Fouls.CountAsync(f => f.MatchId == id && f.TeamId == m.HomeTeamId);
        var awayFouls = await db.Fouls.CountAsync(f => f.MatchId == id && f.TeamId == m.AwayTeamId);

        return Ok(new
        {
            id = m.Id,
            homeTeamId = m.HomeTeamId,
            awayTeamId = m.AwayTeamId,
            homeTeam = m.HomeTeam.Name,
            awayTeam = m.AwayTeam.Name,
            homeScore = m.HomeScore,
            awayScore = m.AwayScore,
            status = m.Status,
            quarterDurationSeconds = m.QuarterDurationSeconds,
            quarter = m.Period,
            timer = new
            {
                running = snap.IsRunning,
                remainingSeconds = snap.RemainingSeconds,
                quarterEndsAtUtc = snap.EndsAt
            },
            homeFouls,
            awayFouls
        });
    }


    [HttpPost("programar")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Programar([FromBody] ProgramarPartidoDto dto)
    {
        if (dto.HomeTeamId <= 0 || dto.AwayTeamId <= 0 || dto.HomeTeamId == dto.AwayTeamId)
            return BadRequest("Selecciona equipos válidos y distintos.");

        var home = await db.Teams.FindAsync(dto.HomeTeamId);
        var away = await db.Teams.FindAsync(dto.AwayTeamId);
        if (home is null || away is null) return BadRequest("Equipo inválido.");

        var when = DateTime.SpecifyKind(dto.DateMatchUtc, DateTimeKind.Utc);

        var match = new MatchEntity
        {
            HomeTeamId = home.Id,
            AwayTeamId = away.Id,
            Status = "Scheduled",
            QuarterDurationSeconds = dto.QuarterDurationSeconds is > 0 ? dto.QuarterDurationSeconds.Value : 10,
            HomeScore = 0,
            AwayScore = 0,
            Period = 1,
            DateMatch = when
        };

        db.Matches.Add(match);
        await db.SaveChangesAsync();

        return Ok(new { matchId = match.Id });
    }

    [HttpPut("{id:int}/reprogramar")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Reprogramar(int id, [FromBody] ReprogramarDto dto)
    {
        var m = await db.Matches.FindAsync(id);
        if (m is null) return NotFound();

        if (m.Status is "Live" or "Finished")
            return BadRequest("No se puede reprogramar un partido en vivo o finalizado.");

        var nuevaUtc = DateTime.SpecifyKind(dto.NewDateMatchUtc, DateTimeKind.Utc);
        if (nuevaUtc < DateTime.UtcNow.AddMinutes(-1))
            return BadRequest("La nueva fecha (UTC) debe ser futura.");

        m.DateMatch = nuevaUtc;
        m.Status = "Scheduled";
        await db.SaveChangesAsync();

        rt.Reset(id);

        return Ok(new { id = m.Id, dateMatchUtc = m.DateMatch, status = m.Status });
    }

    [HttpGet("proximos")]
    public async Task<IActionResult> Proximos()
    {
        var ahora = DateTime.UtcNow;
        var list = await db.Matches
            .Include(x => x.HomeTeam).Include(x => x.AwayTeam)
            .Where(x => x.Status == "Scheduled" && x.DateMatch >= ahora)
            .OrderBy(x => x.DateMatch)
            .Select(x => new
            {
                id = x.Id,
                homeTeamId = x.HomeTeamId,
                awayTeamId = x.AwayTeamId,
                homeTeam = x.HomeTeam.Name,
                awayTeam = x.AwayTeam.Name,
                dateMatchUtc = x.DateMatch,
                status = x.Status
            })
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("rango")]
    public async Task<IActionResult> Rango([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var fromUtc = DateTime.SpecifyKind(from, DateTimeKind.Utc);
        var toUtc   = DateTime.SpecifyKind(to,   DateTimeKind.Utc);
        if (toUtc <= fromUtc) return BadRequest("'to' debe ser mayor que 'from'.");

        var list = await db.Matches
            .Include(x => x.HomeTeam).Include(x => x.AwayTeam)
            .Where(x => x.DateMatch >= fromUtc && x.DateMatch <= toUtc)
            .OrderBy(x => x.DateMatch)
            .Select(x => new
            {
                id = x.Id,
                homeTeam = x.HomeTeam.Name,
                awayTeam = x.AwayTeam.Name,
                dateMatchUtc = x.DateMatch,
                status = x.Status
            })
            .ToListAsync();

        return Ok(list);
    }


    [HttpPost("new")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> NewGame([FromBody] NewGameDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.HomeName) || string.IsNullOrWhiteSpace(dto.AwayName))
            return BadRequest("Team names required");

        var home = new TeamEntity { Name = dto.HomeName.Trim() };
        var away = new TeamEntity { Name = dto.AwayName.Trim() };
        db.AddRange(home, away);
        await db.SaveChangesAsync();

        var match = new MatchEntity
        {
            HomeTeamId = home.Id,
            AwayTeamId = away.Id,
            Status = "Scheduled",
            QuarterDurationSeconds = dto.QuarterDurationSeconds is > 0 ? dto.QuarterDurationSeconds!.Value : 10,
            HomeScore = 0,
            AwayScore = 0,
            Period = 1,
            DateMatch = DateTime.UtcNow
        };
        db.Matches.Add(match);
        await db.SaveChangesAsync();

        rt.Reset(match.Id);

        return Ok(new
        {
            matchId = match.Id,
            homeTeamId = home.Id,
            awayTeamId = away.Id,
            quarterDurationSeconds = match.QuarterDurationSeconds
        });
    }

    [HttpPost("new-by-teams")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> NewByTeams([FromBody] NewGameByTeamsDto dto)
    {
        if (dto.HomeTeamId <= 0 || dto.AwayTeamId <= 0 || dto.HomeTeamId == dto.AwayTeamId)
            return BadRequest("Select two different teams");

        var home = await db.Teams.FindAsync(dto.HomeTeamId);
        var away = await db.Teams.FindAsync(dto.AwayTeamId);
        if (home is null || away is null) return BadRequest("Invalid team ids");

        var match = new MatchEntity
        {
            HomeTeamId = home.Id,
            AwayTeamId = away.Id,
            Status = "Scheduled",
            QuarterDurationSeconds = dto.QuarterDurationSeconds is > 0 ? dto.QuarterDurationSeconds!.Value : 10,
            HomeScore = 0,
            AwayScore = 0,
            Period = 1,
            DateMatch = DateTime.UtcNow
        };

        db.Matches.Add(match);
        await db.SaveChangesAsync();

        rt.Reset(match.Id);

        return Ok(new
        {
            matchId = match.Id,
            homeTeamId = home.Id,
            awayTeamId = away.Id,
            quarterDurationSeconds = match.QuarterDurationSeconds
        });
    }

    [HttpPost("{id:int}/score")]
    public async Task<IActionResult> AddScore(int id, [FromBody] AddScoreDto dto)
    {
        if (dto.Points is not (1 or 2 or 3)) return BadRequest("Points must be 1,2,3");

        var m = await db.Matches.FindAsync(id);
        if (m is null || m.Status != "Live") return NotFound();

        if (dto.TeamId == m.HomeTeamId) m.HomeScore += dto.Points;
        else if (dto.TeamId == m.AwayTeamId) m.AwayScore += dto.Points;
        else return BadRequest("Invalid teamId for this match");

        db.ScoreEvents.Add(new ScoreEventEntity { MatchId = id, TeamId = dto.TeamId, Points = dto.Points, DateRegister = DateTime.Now });
        await db.SaveChangesAsync();

        await hub.Clients.Group($"match-{id}")
            .SendAsync("scoreUpdated", new { homeScore = m.HomeScore, awayScore = m.AwayScore });

        return Ok();
    }

    [HttpPost("{id:int}/score/adjust")]
    public async Task<IActionResult> AdjustScore(int id, [FromBody] AdjustScoreDto dto)
    {
        var m = await db.Matches.FindAsync(id);
        if (m is null) return NotFound();

        if (dto.TeamId == m.HomeTeamId)
        {
            if (m.HomeScore + dto.Delta < 0)
                return BadRequest("Score cannot be negative");
            m.HomeScore += dto.Delta;
        }
        else if (dto.TeamId == m.AwayTeamId)
        {
            if (m.AwayScore + dto.Delta < 0)
                return BadRequest("Score cannot be negative");
            m.AwayScore += dto.Delta;
        }
        else return BadRequest("Invalid teamId for this match");

        db.ScoreEvents.Add(new ScoreEventEntity { MatchId = id, TeamId = dto.TeamId, Points = dto.Delta, DateRegister = DateTime.Now });
        await db.SaveChangesAsync();

        await hub.Clients.Group($"match-{id}")
            .SendAsync("scoreUpdated", new { homeScore = m.HomeScore, awayScore = m.AwayScore });

        return Ok();
    }


    [HttpPost("{id:int}/fouls")]
    public async Task<IActionResult> AddFoul(int id, [FromBody] AddFoulDto dto)
    {
        var m = await db.Matches.FindAsync(id);
        if (m is null) return NotFound();

        if (dto.TeamId != m.HomeTeamId && dto.TeamId != m.AwayTeamId)
            return BadRequest("Invalid teamId for this match");

        db.Fouls.Add(new FoulEntity
        {
            MatchId = id,
            TeamId = dto.TeamId,
            PlayerId = dto.PlayerId,
            Type = dto.Type,
            DateRegister = DateTime.Now
        });
        await db.SaveChangesAsync();

        var homeFouls = await db.Fouls.CountAsync(f => f.MatchId == id && f.TeamId == m.HomeTeamId);
        var awayFouls = await db.Fouls.CountAsync(f => f.MatchId == id && f.TeamId == m.AwayTeamId);

        await hub.Clients.Group($"match-{id}")
            .SendAsync("foulsUpdated", new { homeFouls, awayFouls });

        return Ok(new { homeFouls, awayFouls });
    }

    [HttpPost("{id:int}/fouls/adjust")]
    public async Task<IActionResult> AdjustFoul(int id, [FromBody] AdjustFoulDto dto)
    {
        var m = await db.Matches.FindAsync(id);
        if (m is null) return NotFound();

        if (dto.TeamId != m.HomeTeamId && dto.TeamId != m.AwayTeamId)
            return BadRequest("Invalid teamId for this match");

        if (dto.Delta > 0)
        {
            for (int i = 0; i < dto.Delta; i++)
                db.Fouls.Add(new FoulEntity { MatchId = id, TeamId = dto.TeamId, DateRegister = DateTime.Now });
        }
        else if (dto.Delta < 0)
        {
            var toRemove = await db.Fouls
                .Where(f => f.MatchId == id && f.TeamId == dto.TeamId)
                .OrderByDescending(f => f.Id)
                .Take(Math.Abs(dto.Delta))
                .ToListAsync();

            if (toRemove.Count == 0)
                return BadRequest("No fouls to remove");

            db.Fouls.RemoveRange(toRemove);
        }

        await db.SaveChangesAsync();

        var homeFouls = await db.Fouls.CountAsync(f => f.MatchId == id && f.TeamId == m.HomeTeamId);
        var awayFouls = await db.Fouls.CountAsync(f => f.MatchId == id && f.TeamId == m.AwayTeamId);

        await hub.Clients.Group($"match-{id}")
            .SendAsync("foulsUpdated", new { homeFouls, awayFouls });

        return Ok(new { homeFouls, awayFouls });
    }


    [HttpPost("{id:int}/start")]
    [HttpPost("{id:int}/timer/start")]
    [Authorize(Roles = "Control")]
    public async Task<IActionResult> StartTimer(int id, [FromBody] StartTimerDto? dto)
    {
        var m = await db.Matches.FindAsync(id);
        if (m is null) return NotFound();

        if (dto?.QuarterDurationSeconds is int q && q > 0)
            m.QuarterDurationSeconds = q;

        m.Status = "Live";
        await db.SaveChangesAsync();

        rt.Start(id, m.QuarterDurationSeconds);
        var snap = rt.Get(id);

        await hub.Clients.Group($"match-{id}")
          .SendAsync("timerStarted", new { quarterEndsAtUtc = snap.EndsAt, remainingSeconds = snap.RemainingSeconds });

        await hub.Clients.Group($"match-{id}")
          .SendAsync("quarterChanged", new { quarter = m.Period });
        await hub.Clients.Group($"match-{id}")
          .SendAsync("buzzer", new { reason = "quarter-start" });

        return Ok(new { remainingSeconds = snap.RemainingSeconds, quarterEndsAtUtc = snap.EndsAt });
    }

    [HttpPost("{id:int}/timer/pause")]
    public async Task<IActionResult> PauseTimer(int id)
    {
        var m = await db.Matches.FindAsync(id);
        if (m is null) return NotFound();

        var rem = rt.Pause(id);

        await hub.Clients.Group($"match-{id}")
          .SendAsync("timerPaused", new { remainingSeconds = rem });

        return Ok(new { remainingSeconds = rem });
    }

    [HttpPost("{id:int}/timer/resume")]
    public async Task<IActionResult> ResumeTimer(int id)
    {
        var m = await db.Matches.FindAsync(id);
        if (m is null) return NotFound();

        var snapBefore = rt.Get(id);
        if (snapBefore.RemainingSeconds <= 0) return BadRequest("Nothing to resume");

        rt.Resume(id);
        var snap = rt.Get(id);

        await hub.Clients.Group($"match-{id}")
          .SendAsync("timerResumed", new { quarterEndsAtUtc = snap.EndsAt, remainingSeconds = snap.RemainingSeconds });

        return Ok(new { remainingSeconds = snap.RemainingSeconds, quarterEndsAtUtc = snap.EndsAt });
    }

    [HttpPost("{id:int}/timer/reset")]
    public async Task<IActionResult> ResetTimer(int id)
    {
        var m = await db.Matches.FindAsync(id);
        if (m is null) return NotFound();

        rt.Reset(id);

        await hub.Clients.Group($"match-{id}")
          .SendAsync("timerReset", new { remainingSeconds = 0 });

        return Ok(new { remainingSeconds = 0 });
    }

    [HttpPost("{id:int}/quarters/advance")]
    public async Task<IActionResult> AdvanceQuarter(int id)
    {
        var m = await db.Matches.FindAsync(id);
        if (m is null) return NotFound();

        if (m.Period < 4) m.Period += 1;
        else
        {
            m.Status = "Finished";
            await RecordWinIfFinishedAsync(m);
        }

        await db.SaveChangesAsync();

        await hub.Clients.Group($"match-{id}")
            .SendAsync("quarterChanged", new { quarter = m.Period });
        await hub.Clients.Group($"match-{id}")
            .SendAsync("buzzer", new { reason = "quarter-end" });

        return Ok(new { quarter = m.Period });
    }

    [HttpPost("{id:int}/quarters/auto-advance")]
    public async Task<IActionResult> AutoAdvanceQuarter(int id)
    {
        var m = await db.Matches.FindAsync(id);
        if (m is null) return NotFound();

        if (m.Status == "Finished")
            return Ok(new { quarter = m.Period });

        if (m.Period < 4) m.Period += 1;
        else
        {
            m.Status = "Finished";
            await RecordWinIfFinishedAsync(m);
        }

        await db.SaveChangesAsync();

        await hub.Clients.Group($"match-{id}")
            .SendAsync("quarterChanged", new { quarter = m.Period });
        await hub.Clients.Group($"match-{id}")
            .SendAsync("buzzer", new { reason = "quarter-end" });

        if (m.Status == "Finished")
        {
            await hub.Clients.Group($"match-{id}")
                .SendAsync("gameEnded", new
                {
                    home = m.HomeScore,
                    away = m.AwayScore,
                    winner = m.HomeScore == m.AwayScore ? "draw" : (m.HomeScore > m.AwayScore ? "home" : "away")
                });
        }

        return Ok(new { quarter = m.Period });
    }


    [HttpPost("{id:int}/finish")]
    public async Task<IActionResult> Finish(int id, [FromBody] FinishMatchDto dto)
    {
        var m = await db.Matches
            .Include(x => x.HomeTeam)
            .Include(x => x.AwayTeam)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (m is null) return NotFound();

        // 1) marcador final
        m.HomeScore = dto.HomeScore;
        m.AwayScore = dto.AwayScore;

        // 2) fijar faltas exactas por equipo
        async Task SyncFouls(int teamId, int targetCount)
        {
            var current = await db.Fouls.CountAsync(f => f.MatchId == id && f.TeamId == teamId);
            if (targetCount > current)
            {
                for (int i = 0; i < targetCount - current; i++)
                    db.Fouls.Add(new FoulEntity { MatchId = id, TeamId = teamId, DateRegister = DateTime.UtcNow });
            }
            else if (targetCount < current)
            {
                var toRemove = await db.Fouls
                    .Where(f => f.MatchId == id && f.TeamId == teamId)
                    .OrderByDescending(f => f.Id)
                    .Take(current - targetCount)
                    .ToListAsync();
                db.Fouls.RemoveRange(toRemove);
            }
        }

        await SyncFouls(m.HomeTeamId, dto.HomeFouls);
        await SyncFouls(m.AwayTeamId, dto.AwayFouls);

        // 3) (opcional) persistir eventos si llegaron
        if (dto.ScoreEvents is { Count: > 0 })
            foreach (var se in dto.ScoreEvents)
                db.ScoreEvents.Add(new ScoreEventEntity
                {
                    MatchId = id,
                    TeamId = se.TeamId,
                    PlayerId = se.PlayerId,
                    Points = se.Points,
                    DateRegister = se.DateRegister == default ? DateTime.UtcNow : se.DateRegister
                });

        if (dto.Fouls is { Count: > 0 })
            foreach (var f in dto.Fouls)
                db.Fouls.Add(new FoulEntity
                {
                    MatchId = id,
                    TeamId = f.TeamId,
                    PlayerId = f.PlayerId,
                    DateRegister = f.DateRegister == default ? DateTime.UtcNow : f.DateRegister
                });

        // 4) cerrar y registrar victoria
        m.Status = "Finished";
        await RecordWinIfFinishedAsync(m);
        await db.SaveChangesAsync();

        await hub.Clients.Group($"match-{id}")
            .SendAsync("gameEnded", new
            {
                home = m.HomeScore,
                away = m.AwayScore,
                winner = m.HomeScore == m.AwayScore ? "draw" : (m.HomeScore > m.AwayScore ? "home" : "away")
            });

        return Ok(new { id = m.Id, status = m.Status, homeScore = m.HomeScore, awayScore = m.AwayScore });
    }


    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id)
    {
        var m = await db.Matches.FindAsync(id);
        if (m is null) return NotFound();

        m.Status = "Canceled";
        await db.SaveChangesAsync();

        await hub.Clients.Group($"match-{id}")
            .SendAsync("gameCanceled", new { status = m.Status });

        return Ok(new { status = m.Status });
    }

    [HttpPost("{id:int}/suspend")]
    public async Task<IActionResult> Suspend(int id)
    {
        var m = await db.Matches.FindAsync(id);
        if (m is null) return NotFound();

        m.Status = "Suspended";
        await db.SaveChangesAsync();

        await hub.Clients.Group($"match-{id}")
            .SendAsync("gameSuspended", new { status = m.Status });

        return Ok(new { status = m.Status });
    }


    private async Task RecordWinIfFinishedAsync(MatchEntity m)
    {
        if (m.Status != "Finished") return;
        if (m.HomeScore == m.AwayScore) return;

        var winnerTeamId = m.HomeScore > m.AwayScore ? m.HomeTeamId : m.AwayTeamId;

        var exists = await db.TeamWins.AnyAsync(tw => tw.TeamId == winnerTeamId && tw.MatchId == m.Id);
        if (!exists)
        {
            db.TeamWins.Add(new TeamWinEntity
            {
                TeamId = winnerTeamId,
                MatchId = m.Id,
                DateRegistered = DateTime.UtcNow
            });
        }
    }
}
