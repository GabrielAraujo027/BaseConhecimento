using BaseConhecimento.Data;
using BaseConhecimento.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// MVC / APIs
builder.Services.AddControllersWithViews();
builder.Services.AddControllers();

// EF Core
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// CORS (ajuste para produção)
builder.Services.AddCors(o => o.AddPolicy("Frontend", p =>
    p.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin()
));

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// HttpClient para o Ollama (localhost:11434)
builder.Services.AddHttpClient("ollama", client =>
{
    client.BaseAddress = new Uri("http://localhost:11434/");
});

// Serviço de embeddings (usa Ollama)
builder.Services.AddScoped<IEmbeddingService, OllamaEmbeddingService>();

var app = builder.Build();

// Auto-migrate (opcional)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseCors("Frontend");
app.UseAuthorization();

app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
