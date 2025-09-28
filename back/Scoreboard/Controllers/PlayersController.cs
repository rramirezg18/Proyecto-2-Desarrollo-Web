using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scoreboard.Infrastructure;
using Scoreboard.Models.Entities;
using Microsoft.AspNetCore.Authorization; 

namespace Scoreboard.Controllers;

[ApiController]
[Route("api/players")]
public class PlayersController(AppDbContext db) : ControllerBase
{

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] int? teamId = null,
        [FromQuery] string? search = null
    )
    {
        var query = db.Players.Include(p => p.Team).AsQueryable();


        if (teamId.HasValue)
            query = query.Where(p => p.TeamId == teamId.Value);


        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(term) ||
                (p.Number != null && p.Number.ToString().Contains(term))
            );
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderBy(p => p.Number)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                id = p.Id,
                number = p.Number,
                name = p.Name,
                teamId = p.TeamId,
                teamName = p.Team != null ? p.Team.Name : null
            })
            .ToListAsync();

        return Ok(new { items, totalCount = total });
    }

    // GET /api/players/5
    [HttpGet("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetById(int id)
    {
        var player = await db.Players.Include(p => p.Team)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (player == null) return NotFound();

        return Ok(new
        {
            id = player.Id,
            number = player.Number,
            name = player.Name,
            teamId = player.TeamId,
            teamName = player.Team?.Name
        });
    }

    // POST /api/players
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] Player player)
    {
        if (string.IsNullOrWhiteSpace(player.Name))
            return BadRequest("El nombre es requerido");

        db.Players.Add(player);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = player.Id }, player);
    }

    // PUT /api/players/5
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] Player dto)
    {
        var player = await db.Players.FindAsync(id);
        if (player == null) return NotFound();

        player.Name = dto.Name;
        player.Number = dto.Number;
        player.TeamId = dto.TeamId;

        await db.SaveChangesAsync();
        return NoContent();
    }

    // DELETE /api/players/5
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var player = await db.Players.FindAsync(id);
        if (player == null) return NotFound();

        db.Players.Remove(player);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
