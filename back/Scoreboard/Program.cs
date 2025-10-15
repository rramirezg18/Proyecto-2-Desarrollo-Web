using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;

using Scoreboard.Infrastructure;
using Scoreboard.Hubs;

// Repos & Services
using Scoreboard.Repositories;
using Scoreboard.Repositories.Interfaces;
using Scoreboard.Services;
using Scoreboard.Services.Interfaces;

// Aliases entidades
using TeamEntity  = Scoreboard.Models.Entities.Team;
using MatchEntity = Scoreboard.Models.Entities.Match;

var builder = WebApplication.CreateBuilder(args);

// 1) Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 2) EF Core
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 3) SignalR
builder.Services.AddSignalR();

// 4) CORS para Angular dev server
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
    {
        policy.WithOrigins("http://localhost:4200", "http://127.0.0.1:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// 5) Runtime del reloj en memoria
builder.Services.AddSingleton<IMatchRunTime, MatchRunTime>();

// 6) Repos/Servicios
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IMenuRepository, MenuRepository>();
builder.Services.AddScoped<IMenuService, MenuService>();

// 7) JWT
var jwtSettings = builder.Configuration.GetSection("Jwt");
var keyBytes = Encoding.UTF8.GetBytes(jwtSettings["Key"] ?? "ClaverSuperMegaHiperSecreta*1234");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer           = false,
        ValidateAudience         = false,
        ValidateLifetime         = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey         = new SymmetricSecurityKey(keyBytes),

        // Para [Authorize(Roles="Admin")]
        RoleClaimType = ClaimTypes.Role,
        NameClaimType = ClaimTypes.Name
    };

    // Duplica 'role' / 'roles' -> ClaimTypes.Role y log de diagnóstico
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = ctx =>
        {
            if (ctx.Principal?.Identity is ClaimsIdentity id)
            {
                var list = new List<string>();

                // role (string plano)
                list.AddRange(id.FindAll("role").Select(c => c.Value));

                // roles (json array opcional)
                var rolesJson = id.FindFirst("roles")?.Value;
                if (!string.IsNullOrWhiteSpace(rolesJson))
                {
                    try
                    {
                        var arr = System.Text.Json.JsonSerializer.Deserialize<string[]>(rolesJson) ?? [];
                        list.AddRange(arr);
                    } catch { /* ignore */ }
                }

                foreach (var r in list.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    var norm = (r.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
                                r.Equals("administrador", StringComparison.OrdinalIgnoreCase))
                               ? "Admin" : r;

                    if (!id.HasClaim(ClaimTypes.Role, norm))
                        id.AddClaim(new Claim(ClaimTypes.Role, norm));
                }

                // Log (útil para depurar; puedes quitarlo luego)
                try
                {
                    Console.WriteLine("== JWT Claims ==");
                    foreach (var c in id.Claims) Console.WriteLine($"  {c.Type} = {c.Value}");
                    Console.WriteLine("===============");
                } catch { }
            }

            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

// 8) Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors("DevCors");
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ScoreHub>("/hubs/score");


app.MapFallbackToFile("/index.html");


using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    if (!db.Teams.Any())
    {
        var home = new TeamEntity { Name = "Locales",    Color = "#0044FF" };
        var away = new TeamEntity { Name = "Visitantes", Color = "#FF3300" };
        db.AddRange(home, away);
        await db.SaveChangesAsync();

        db.Set<MatchEntity>().Add(new MatchEntity
        {
            HomeTeamId = home.Id,
            AwayTeamId = away.Id,
            Status = "Scheduled",
            QuarterDurationSeconds = 600,
            HomeScore = 0,
            AwayScore = 0,
            Period    = 1,
            DateMatch = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }
}

app.Run();
