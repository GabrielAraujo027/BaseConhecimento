using BaseConhecimento.DTOs.Auth;
using BaseConhecimento.DTOs.Auth.Requests;
using BaseConhecimento.DTOs.Auth.Responses;
using BaseConhecimento.Models.Auth;
using BaseConhecimento.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

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
            var user = await _userManager.FindByNameAsync(User.Identity!.Name!);
            if (user is null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            return Ok(new { user.Email, user.FullName, roles });
        }
    }
}
