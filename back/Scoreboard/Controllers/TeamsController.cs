using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scoreboard.Infrastructure;
using Scoreboard.Models.Entities;
using Microsoft.AspNetCore.Authorization; 

namespace Scoreboard.Controllers;

[ApiController]
[Route("api/teams")]
public class TeamsController(AppDbContext db) : ControllerBase
{
    // GET /api/teams?page=1&pageSize=10&q=texto
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? q = null
    )
    {
        var query = db.Teams.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(t => t.Name.Contains(term));
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderBy(t => t.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                id = t.Id,
                name = t.Name,
                color = t.Color,
                playersCount = db.Players.Count(p => p.TeamId == t.Id)
            })
            .ToListAsync();

        return Ok(new { items, totalCount = total });
    }

    // GET /api/teams/{id}
    [HttpGet("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetById(int id)
    {
        var team = await db.Teams.FirstOrDefaultAsync(t => t.Id == id);
        if (team is null) return NotFound();

        var players = await db.Players
            .Where(p => p.TeamId == id)
            .OrderBy(p => p.Number)
            .Select(p => new { id = p.Id, number = p.Number, name = p.Name })
            .ToListAsync();

        return Ok(new
        {
            id = team.Id,
            name = team.Name,
            color = team.Color,
            players
        });
    }

    // DTOs locales
    public record PlayerItemDto(int? Number, string Name);
    public record CreateTeamDto(string Name, string? Color, List<PlayerItemDto>? Players);

    // POST /api/teams
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateTeamDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest("Team name is required");

        var exists = await db.Teams.AnyAsync(t => t.Name == dto.Name);
        if (exists) return Conflict("Team name already exists");

        var team = new Team
        {
            Name = dto.Name.Trim(),
            Color = string.IsNullOrWhiteSpace(dto.Color) ? null : dto.Color!.Trim()
        };

        db.Teams.Add(team);
        await db.SaveChangesAsync();

        if (dto.Players is { Count: > 0 })
        {
            var numbers = dto.Players.Where(p => p.Number.HasValue).Select(p => p.Number!.Value).ToList();
            if (numbers.Count != numbers.Distinct().Count())
                return BadRequest("Duplicated jersey numbers within the same team");

            foreach (var p in dto.Players)
            {
                db.Players.Add(new Player
                {
                    TeamId = team.Id,
                    Number = p.Number,
                    Name = (p.Name ?? string.Empty).Trim()
                });
            }
            await db.SaveChangesAsync();
        }

        return CreatedAtAction(nameof(GetById), new { id = team.Id }, new { id = team.Id, name = team.Name });
    }

    // PUT /api/teams/{id}
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateTeamDto dto)
    {
        var team = await db.Teams.Include(t => t.Players).FirstOrDefaultAsync(t => t.Id == id);
        if (team is null) return NotFound();

        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest("Team name is required");

        var exists = await db.Teams.AnyAsync(t => t.Name == dto.Name && t.Id != id);
        if (exists) return Conflict("Team name already exists");

        team.Name = dto.Name.Trim();
        team.Color = string.IsNullOrWhiteSpace(dto.Color) ? null : dto.Color!.Trim();

        // Actualizar jugadores si vienen en el DTO
        if (dto.Players is { Count: > 0 })
        {
            var numbers = dto.Players.Where(p => p.Number.HasValue).Select(p => p.Number!.Value).ToList();
            if (numbers.Count != numbers.Distinct().Count())
                return BadRequest("Duplicated jersey numbers within the same team");

            db.Players.RemoveRange(team.Players);

            foreach (var p in dto.Players)
            {
                db.Players.Add(new Player
                {
                    TeamId = team.Id,
                    Number = p.Number,
                    Name = (p.Name ?? string.Empty).Trim()
                });
            }
        }

        await db.SaveChangesAsync();
        return NoContent();
    }

    // DELETE /api/teams/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var team = await db.Teams
            .Include(t => t.Players)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (team is null) return NotFound();

        // 👇 eliminar dependencias
        var teamWins = db.TeamWins.Where(tw => tw.TeamId == id);
        db.TeamWins.RemoveRange(teamWins);

        var fouls = db.Fouls.Where(f => f.TeamId == id);
        db.Fouls.RemoveRange(fouls);

        var scores = db.ScoreEvents.Where(se => se.TeamId == id);
        db.ScoreEvents.RemoveRange(scores);

        // 👇 borrar jugadores
        if (team.Players.Any())
            db.Players.RemoveRange(team.Players);

        // 👇 borrar equipo
        db.Teams.Remove(team);

        await db.SaveChangesAsync();
        return NoContent();
    }
}
