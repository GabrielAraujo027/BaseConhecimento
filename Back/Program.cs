// Program.cs
using BaseConhecimento.Data;
using BaseConhecimento.Models.Auth;
using BaseConhecimento.Services;
using BaseConhecimento.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// -------------------------
// MVC / APIs
// -------------------------
builder.Services.AddControllersWithViews();
builder.Services.AddControllers();

// -------------------------
// EF Core
// -------------------------
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// -------------------------
// Identity + Roles
// -------------------------
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(opt =>
    {
        opt.Password.RequiredLength = 6;
        opt.Password.RequireDigit = false;
        opt.Password.RequireUppercase = false;
        opt.Password.RequireNonAlphanumeric = false;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// -------------------------
// JWT Auth
// -------------------------
var jwt = builder.Configuration.GetSection("Jwt");
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"] ?? "CHAVE-DEV-ALTERE-NO-APPSETTINGS"));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwt["Issuer"] ?? "BaseConhecimento",
        ValidAudience = jwt["Audience"] ?? "BaseConhecimento",
        IssuerSigningKey = signingKey,
        ClockSkew = TimeSpan.FromMinutes(1)
    };
});

builder.Services.AddAuthorization();

// -------------------------
// CORS (permite qualquer origem/método/header)
// -------------------------
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .SetIsOriginAllowed(_ => true)   // aceita QUALQUER origem
                                             // .AllowCredentials()            // NÃO usar com Bearer simples
    );
});

// -------------------------
// Swagger (com JWT Bearer)
// -------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opt =>
{
    opt.SwaggerDoc("v1", new OpenApiInfo { Title = "BaseConhecimento API", Version = "v1" });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Insira: Bearer {seu_token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    opt.AddSecurityDefinition("Bearer", securityScheme);
    opt.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { securityScheme, Array.Empty<string>() }
    });
});

// -------------------------
// HttpClient para o Ollama (localhost:11434)
// -------------------------
builder.Services.AddHttpClient("ollama", client =>
{
    client.BaseAddress = new Uri("http://localhost:11434/");
});

// -------------------------
// Serviços de aplicação
// -------------------------
builder.Services.AddScoped<ITokenService, TokenService>();                  // gera JWT
builder.Services.AddScoped<IEmbeddingService, OllamaEmbeddingService>();    // embeddings
builder.Services.AddHttpClient<ILlamaService, LlamaService>();              // Llama (se usar)

var app = builder.Build();

// -------------------------
// Migrations + Seed (roles + usuário opcional)
// -------------------------
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;

    // DB migrate
    var db = sp.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    // Cria roles padrão
    var roleMgr = sp.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var role in new[] { "Solicitante", "Atendente" })
    {
        if (!await roleMgr.RoleExistsAsync(role))
            await roleMgr.CreateAsync(new IdentityRole(role));
    }

    // Seed opcional via appsettings:
    // "Auth": { "SeedUser": { "Email": "...", "Password": "...", "Role": "Atendente" } }
    var cfg = app.Configuration;
    var seedEmail = cfg["Auth:SeedUser:Email"];
    var seedPass = cfg["Auth:SeedUser:Password"];
    var seedRole = cfg["Auth:SeedUser:Role"] ?? "Atendente";

    if (!string.IsNullOrWhiteSpace(seedEmail))
    {
        var userMgr = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userMgr.FindByEmailAsync(seedEmail);

        if (user is null && !string.IsNullOrWhiteSpace(seedPass))
        {
            user = new ApplicationUser { UserName = seedEmail, Email = seedEmail };
            var res = await userMgr.CreateAsync(user, seedPass);
            if (res.Succeeded)
            {
                if (!await roleMgr.RoleExistsAsync(seedRole))
                    await roleMgr.CreateAsync(new IdentityRole(seedRole));
                await userMgr.AddToRoleAsync(user, seedRole);
            }
        }
        else if (user is not null)
        {
            if (!await userMgr.IsInRoleAsync(user, seedRole))
                await userMgr.AddToRoleAsync(user, seedRole);
        }
    }
}


if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseStaticFiles();

app.UseRouting();

app.UseCors("Frontend");         // CORS entre Routing e Auth

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
