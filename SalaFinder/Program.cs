using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using SalaFinder.Data;
using SalaFinder.Interfaces;
using SalaFinder.Models;
using SalaFinder.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddControllers();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        // Azure SQL serverless auto-pauses; the first connection after pause throws transient
        // errors while the DB wakes up. EnableRetryOnFailure gives EF Core an execution strategy
        // that retries those errors with backoff.
        sql => sql.EnableRetryOnFailure(
            maxRetryCount: 8,
            maxRetryDelay: TimeSpan.FromSeconds(15),
            errorNumbersToAdd: null)));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{ // identity
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

var jwtKey      = builder.Configuration["Jwt:Key"]!;
var jwtIssuer   = builder.Configuration["Jwt:Issuer"]!;
var jwtAudience = builder.Configuration["Jwt:Audience"]!;

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer           = true,
        ValidateAudience         = true,
        ValidateLifetime         = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer     = jwtIssuer,
        ValidAudience   = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtKey))
    };
});

builder.Services.AddScoped<ISpaceService, SpaceService>(); // servicios
builder.Services.AddScoped<IReservationService, ReservationService>();

// CORS — orígenes permitidos vienen de configuración (appsettings.json / env vars).
// En Azure App Service se configura como "Cors__AllowedOrigins__0", "Cors__AllowedOrigins__1", etc.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? Array.Empty<string>();
if (allowedOrigins.Length == 0)
{
    // Fallback razonable para que un deploy mal configurado no genere 405 silenciosos
    // en los preflight CORS. Si esto se dispara en producción, revisar la app setting
    // Cors__AllowedOrigins__0 en Azure App Service.
    allowedOrigins = new[] { "http://localhost:5173" };
}
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// === Aplicar migraciones + sembrar datos demo al iniciar ===
// En Azure SQL Serverless la BD puede estar auto-pausada; el primer intento
// suele fallar mientras la BD "despierta" (~30-60s). Usamos la execution
// strategy de EF Core para reintentar de forma transparente.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var strategy = db.Database.CreateExecutionStrategy();
    await strategy.ExecuteAsync(async () =>
    {
        db.Database.Migrate();
        await DbSeeder.SeedAsync(scope.ServiceProvider);
    });
}

app.Run();
