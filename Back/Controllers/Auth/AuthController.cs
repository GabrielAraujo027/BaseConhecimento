using BaseConhecimento.DTOs.Auth;
using BaseConhecimento.DTOs.Auth.Requests;
using BaseConhecimento.DTOs.Auth.Responses;
using BaseConhecimento.Models.Auth;
using BaseConhecimento.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace BaseConhecimento.Controllers.Auth
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ITokenService _tokenSvc;
        private readonly IConfiguration _cfg;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ITokenService tokenSvc,
            IConfiguration cfg)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _tokenSvc = tokenSvc;
            _cfg = cfg;
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] CadastroDTO dto)
        {
            var exists = await _userManager.FindByEmailAsync(dto.Email);
            if (exists is not null)
                return BadRequest(new { error = "E-mail já cadastrado." });

            var user = new ApplicationUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                FullName = dto.FullName
            };

            var result = await _userManager.CreateAsync(user, dto.Password);

            if (!await _roleManager.RoleExistsAsync("Solicitante"))
                await _roleManager.CreateAsync(new IdentityRole("Solicitante"));

            await _userManager.AddToRoleAsync(user, "Solicitante");

            if (!result.Succeeded)
                return BadRequest(new { error = string.Join("; ", result.Errors.Select(e => e.Description)) });

            return Ok(new { message = "Usuário registrado com sucesso." });
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthResponseDTO>> Login([FromBody] LoginDTO dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user is null) return Unauthorized(new { error = "Credenciais inválidas." });

            var passOk = await _userManager.CheckPasswordAsync(user, dto.Password);
            if (!passOk) return Unauthorized(new { error = "Credenciais inválidas." });

            var roles = await _userManager.GetRolesAsync(user);
            var expMinutes = _cfg.GetValue<int>("Jwt:ExpiresMinutes", 120);
            var expiresAt = DateTime.UtcNow.AddMinutes(expMinutes);

            var token = await _tokenSvc.GenerateAsync(user, roles, expiresAt);

            return Ok(new AuthResponseDTO
            {
                Token = token,
                ExpiresAt = expiresAt,
                Email = user.Email!,
                Roles = roles
            });
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> Me()
        {
            // pega do token: NameIdentifier ou sub; fallback para email
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            ApplicationUser? user = null;

            if (!string.IsNullOrWhiteSpace(userId))
            {
                user = await _userManager.FindByIdAsync(userId);
            }
            else
            {
                var email = User.FindFirstValue(ClaimTypes.Email)
                          ?? User.FindFirstValue(JwtRegisteredClaimNames.Email);

                if (string.IsNullOrWhiteSpace(email))
                    return Unauthorized(new { error = "Usuário não identificado no token." });

                user = await _userManager.FindByEmailAsync(email);
            }

            if (user is null) return NotFound(new { error = "Usuário não encontrado." });

            var roles = await _userManager.GetRolesAsync(user);
            return Ok(new { user.Email, user.FullName, roles });
        }

        [HttpPost("assign-role")]
        [Authorize(Roles = "Atendente")]
        public async Task<IActionResult> AssignRole([FromQuery] string email, [FromQuery] string role)
        {
            if (!await _roleManager.RoleExistsAsync(role))
                await _roleManager.CreateAsync(new IdentityRole(role));

            var user = await _userManager.FindByEmailAsync(email);
            if (user is null) return NotFound(new { error = "Usuário não encontrado." });

            var res = await _userManager.AddToRoleAsync(user, role);
            if (!res.Succeeded)
                return BadRequest(new { error = string.Join("; ", res.Errors.Select(e => e.Description)) });

            return Ok(new { message = $"Papel '{role}' atribuído a {email}." });
        }
    }
}
