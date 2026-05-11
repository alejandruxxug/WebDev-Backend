using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using SalaFinder.DTOs.Auth;
using SalaFinder.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SalaFinder.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IConfiguration _config;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IConfiguration config)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _config = config;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            var existingUser = await _userManager.FindByEmailAsync(dto.Email);
            if (existingUser != null)
                return BadRequest(new { message = "Ya existe un usuario con ese correo " });

            var allowedRoles = new[] { "Student", "Staff", "Admin" };
            if (!allowedRoles.Contains(dto.Role))
                return BadRequest(new { message = "Rol inválido. Debe ser: Student, Staff o Admin." });

            var user = new ApplicationUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                FullName = dto.FullName,
                Program = dto.Program,
                IsBlocked = false,
                NoShowCount = 0
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
                return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

            if (!await _roleManager.RoleExistsAsync(dto.Role))
                await _roleManager.CreateAsync(new IdentityRole(dto.Role));

            await _userManager.AddToRoleAsync(user, dto.Role);

            return Ok(new { message = "Usuario registrado correctamente.", userId = user.Id, role = dto.Role });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
                return Unauthorized(new { message = "Credenciales inválidas." });

            var passwordValid = await _userManager.CheckPasswordAsync(user, dto.Password);
            if (!passwordValid)
                return Unauthorized(new { message = "Credenciales inválidas." });

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? "Student";

            var expiresAt = DateTime.UtcNow.AddHours(8);
            var token = GenerateToken(user, role, expiresAt);

            return Ok(new AuthResponseDto
            {
                Token = token,
                Email = user.Email!,
                FullName = user.FullName,
                Role = role,
                ExpiresAt = expiresAt
            });
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetProfile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId!);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);

            return Ok(new
            {
                user.Id,
                user.FullName,
                user.Email,
                user.Program,
                user.IsBlocked,
                user.BlockedUntil,
                user.NoShowCount,
                Role = roles.FirstOrDefault() ?? "Student"
            });
        }

        [HttpGet("users")]
        [Authorize(Roles = "Admin")]
        public IActionResult GetAllUsers()
        {
            var users = _userManager.Users.Select(u => new
            {
                u.Id,
                u.FullName,
                u.Email,
                u.Program,
                u.IsBlocked,
                u.BlockedUntil,
                u.NoShowCount
            }).ToList();

            return Ok(users);
        }

        private string GenerateToken(ApplicationUser user, string role, DateTime expiresAt)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email!),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Role, role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: expiresAt,
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
