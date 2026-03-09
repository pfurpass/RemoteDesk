using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using RemoteDesk.Server.Models;

namespace RemoteDesk.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly ILogger<AuthController> _logger;

        private static readonly Dictionary<string, string> _users = new(StringComparer.OrdinalIgnoreCase)
        {
            ["admin"] = "admin123",
            ["user1"] = "password1"
        };

        public AuthController(IConfiguration config, ILogger<AuthController> logger)
        {
            _config = config;
            _logger = logger;
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] RemoteDesk.Server.Models.LoginRequest req)
        {
            if (!_users.TryGetValue(req.Username, out var storedPw) || storedPw != req.Password)
            {
                _logger.LogWarning("Failed login for user: {User}", req.Username);
                return Unauthorized(new { message = "Invalid credentials." });
            }
            var token = GenerateJwt(req.Username);
            _logger.LogInformation("User logged in: {User}", req.Username);
            return Ok(new { token, username = req.Username });
        }

        private string GenerateJwt(string username)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, "User"),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };
            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: creds);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}