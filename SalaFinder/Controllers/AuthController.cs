using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SalaFinder.Data;
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
        private readonly ApplicationDbContext _context;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IConfiguration config,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _config = config;
            _context = context;
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
            if (roles.Count == 0) roles = new List<string> { "Student" };

            var expiresAt = DateTime.UtcNow.AddHours(8);
            var token = GenerateToken(user, roles, expiresAt);

            return Ok(new AuthResponseDto
            {
                Token = token,
                Email = user.Email!,
                FullName = user.FullName,
                Role = roles[0],
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
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _userManager.Users.ToListAsync();
            var result = new List<object>();
            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                result.Add(new
                {
                    u.Id,
                    u.FullName,
                    u.Email,
                    u.Program,
                    u.IsBlocked,
                    u.BlockedUntil,
                    u.NoShowCount,
                    Role = roles.FirstOrDefault() ?? "Student"
                });
            }
            return Ok(result);
        }

        [HttpPatch("users/{id}/role")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ChangeRole(string id, [FromBody] ChangeRoleDto dto)
        {
            var allowedRoles = new[] { "Student", "Staff", "Admin" };
            if (!allowedRoles.Contains(dto.NewRole))
                return BadRequest(new { message = "Rol inválido. Debe ser: Student, Staff o Admin." });

            var currentAdminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (id == currentAdminId)
                return BadRequest(new { message = "No puedes cambiar tu propio rol." });

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound(new { message = "Usuario no encontrado." });

            var existingRoles = await _userManager.GetRolesAsync(user);
            var oldRole = existingRoles.FirstOrDefault() ?? "Student";

            if (oldRole == dto.NewRole)
                return BadRequest(new { message = "El usuario ya tiene ese rol." });

            if (oldRole == "Admin" && dto.NewRole != "Admin")
            {
                var admins = await _userManager.GetUsersInRoleAsync("Admin");
                if (admins.Count <= 1)
                    return BadRequest(new { message = "No puedes degradar al último administrador." });
            }

            if (existingRoles.Count > 0)
            {
                var removeResult = await _userManager.RemoveFromRolesAsync(user, existingRoles);
                if (!removeResult.Succeeded)
                    return BadRequest(new { errors = removeResult.Errors.Select(e => e.Description) });
            }

            if (!await _roleManager.RoleExistsAsync(dto.NewRole))
                await _roleManager.CreateAsync(new IdentityRole(dto.NewRole));

            var addResult = await _userManager.AddToRoleAsync(user, dto.NewRole);
            if (!addResult.Succeeded)
                return BadRequest(new { errors = addResult.Errors.Select(e => e.Description) });

            await LogAuditAsync(
                userId: currentAdminId!,
                action: "ROLE_CHANGED",
                details: $"{user.Email}: {dto.Reason}",
                previousStatus: oldRole,
                newStatus: dto.NewRole
            );

            return Ok(new { message = "Rol actualizado." });
        }

        [HttpPost("users/{id}/lock")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> LockUser(string id, [FromBody] LockUserDto dto)
        {
            var currentAdminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (id == currentAdminId)
                return BadRequest(new { message = "No puedes bloquearte a ti mismo." });

            if (dto.BlockedUntil <= DateTime.UtcNow)
                return BadRequest(new { message = "La fecha de bloqueo debe ser futura." });

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound(new { message = "Usuario no encontrado." });

            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains("Admin"))
                return BadRequest(new { message = "No puedes bloquear a un administrador." });

            user.BlockedUntil = dto.BlockedUntil;
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
                return BadRequest(new { errors = updateResult.Errors.Select(e => e.Description) });

            await LogAuditAsync(
                userId: currentAdminId!,
                action: "USER_LOCKED",
                details: $"{user.Email} bloqueado hasta {dto.BlockedUntil:o}: {dto.Reason}",
                previousStatus: "",
                newStatus: ""
            );

            return Ok(new { message = "Usuario bloqueado." });
        }

        [HttpPost("users/{id}/unlock")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UnlockUser(string id, [FromBody] UnlockUserDto dto)
        {
            var currentAdminId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound(new { message = "Usuario no encontrado." });

            user.BlockedUntil = null;
            user.NoShowCount = 0;
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
                return BadRequest(new { errors = updateResult.Errors.Select(e => e.Description) });

            await LogAuditAsync(
                userId: currentAdminId!,
                action: "USER_UNLOCKED",
                details: $"{user.Email}: {dto.Reason}",
                previousStatus: "",
                newStatus: ""
            );

            return Ok(new { message = "Usuario desbloqueado." });
        }

        private async Task LogAuditAsync(string userId, string action, string details, string previousStatus, string newStatus)
        {
            var log = new AuditLog
            {
                ReservationId = null,
                UserId = userId,
                Action = action,
                Details = details,
                PreviousStatus = previousStatus,
                NewStatus = newStatus,
                Timestamp = DateTime.UtcNow
            };
            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }

        private string GenerateToken(ApplicationUser user, IList<string> roles, DateTime expiresAt)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email!),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            };

            // === Roles → claims ===
            // Un Claim por rol para que [Authorize(Roles = "...")] funcione
            // si el usuario tiene multiples roles asignados.
            foreach (var role in roles)
                claims.Add(new Claim(ClaimTypes.Role, role));

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
