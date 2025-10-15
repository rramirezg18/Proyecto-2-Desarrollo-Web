using Microsoft.IdentityModel.Tokens;
using Scoreboard.Models.DTOs;
using Scoreboard.Models.Entities;
using Scoreboard.Repositories.Interfaces;
using Scoreboard.Services.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Scoreboard.Services
{
    public class AuthService(IUserRepository userRepository, IRoleRepository roleRepository, IConfiguration config)
        : IAuthService
    {
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IRoleRepository _roleRepository = roleRepository;
        private readonly IConfiguration _config = config;

        public async Task<LoginResponseDto?> AuthenticateAsync(LoginRequestDto request)
        {
            var user = await _userRepository.GetUserWithRoleAsync(request.Username, request.Password);
            if (user == null) return null;

            var token = GenerateJwtToken(user);

            return new LoginResponseDto
            {
                Username = user.Username,
                Role = new RoleDto { Name = user.Role?.Name ?? "" },
                Token = token
            };
        }

        public async Task<string?> RegisterAsync(RegisterRequestDto request)
        {
            var existingUser = await _userRepository.GetByUsernameAsync(request.Username);
            if (existingUser != null) return null;

            var role = await _roleRepository.GetRoleByIdAsync(request.RoleId);
            if (role == null) return null;

            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

            var user = new User
            {
                Username = request.Username,
                Password = hashedPassword,
                RoleId = role.Id
            };

            await _userRepository.AddAsync(user);
            return "Usuario registrado correctamente.";
        }

        private string GenerateJwtToken(User user)
        {
            var jwtSettings = _config.GetSection("Jwt");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"] ?? "SuperSecretKey123"));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);


            var roleName = string.IsNullOrWhiteSpace(user.Role?.Name) ? "User" : user.Role!.Name;
            if (roleName.Equals("administrador", StringComparison.OrdinalIgnoreCase)) roleName = "Admin";
            if (roleName.Equals("admin", StringComparison.OrdinalIgnoreCase))          roleName = "Admin";

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),


                new Claim(ClaimTypes.Role, roleName),


                new Claim("role", roleName),


                new Claim("roles", System.Text.Json.JsonSerializer.Serialize(new []{ roleName })),

                new Claim("Id", user.Id.ToString())
            };

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(Convert.ToDouble(jwtSettings["ExpiresInMinutes"] ?? "60")),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
