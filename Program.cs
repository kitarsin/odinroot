using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;
using System.Text;
using ODIN.Api.Data;
using ODIN.Api.Services;
using ODIN.Api.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════════════════
// Database — Connect to YOUR Supabase PostgreSQL instance
// ═══════════════════════════════════════════════════════════
builder.Services.AddDbContext<OdinDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("SupabaseDb"),
        npgsql => { }));

// ═══════════════════════════════════════════════════════════
// Authentication — Validate Supabase JWT tokens
//
// Your React frontend already authenticates users via Supabase Auth.
// The ASP.NET API validates those same JWT tokens so it knows which
// user (profiles.id) is making the request.
// ═══════════════════════════════════════════════════════════
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var supabaseUrl = builder.Configuration["Supabase:Url"];
        var jwtSecret = builder.Configuration["Supabase:JwtSecret"];

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = $"{supabaseUrl}/auth/v1",
            ValidateAudience = true,
            ValidAudience = "authenticated",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSecret!)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

// ═══════════════════════════════════════════════════════════
// Register ODIN Engine Services
// ═══════════════════════════════════════════════════════════
builder.Services.AddScoped<IHbdaService, HbdaService>();
builder.Services.AddSingleton<IDiagnosticEngine, DiagnosticEngine>();
builder.Services.AddSingleton<ICodeExecutionService, CodeExecutionService>();
builder.Services.AddScoped<IBktService, BktService>();
builder.Services.AddScoped<IAffectiveStateService, AffectiveStateService>();
builder.Services.AddScoped<IInterventionController, InterventionControllerService>();

// ═══════════════════════════════════════════════════════════
// API Configuration
// ═══════════════════════════════════════════════════════════
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "ODIN API",
        Version = "v1",
        Description = "Pedagogical Kernel for the ODIN Intelligent Tutoring System (Supabase-integrated)"
    });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Paste your Supabase JWT token here"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// CORS — merge appsettings array + Cors:AllowedOriginsCsv + CORS_ALLOWED_ORIGINS (comma-separated, for Railway/Vercel)
var corsOriginAllowlist = BuildCorsOriginAllowlist(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("GameClient", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
                !string.IsNullOrWhiteSpace(origin) &&
                corsOriginAllowlist.Contains(origin.Trim()))
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("GameClient");

// Unhandled exceptions skip normal CORS handling; echo Allow-Origin for allowlisted frontends so browsers surface real errors.
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        var log = context.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("UnhandledException");
        log.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);

        if (context.Response.HasStarted)
            throw;

        AppendCorsHeadersIfAllowed(context, corsOriginAllowlist);
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json; charset=utf-8";
        var json = app.Environment.IsDevelopment()
            ? JsonSerializer.Serialize(new { error = "Internal server error", detail = ex.Message })
            : JsonSerializer.Serialize(new { error = "Internal server error" });
        await context.Response.WriteAsync(json);
    }
});

app.UseAuthentication();  // Validate Supabase JWT
app.UseAuthorization();
app.MapControllers();

Console.WriteLine(@"
 ╔═══════════════════════════════════════════════════════════╗
 ║  ODIN API — Supabase Integration Mode                    ║
 ║  Connected to existing profiles/progress/submissions     ║
 ║  Pipeline: HBDA → AST → BKT → Affective → Intervention  ║
 ╚═══════════════════════════════════════════════════════════╝
");

app.Run();

static HashSet<string> BuildCorsOriginAllowlist(IConfiguration configuration)
{
    var fromSection = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    var csv = configuration["Cors:AllowedOriginsCsv"] ?? configuration["CORS_ALLOWED_ORIGINS"];
    var fromCsv = string.IsNullOrWhiteSpace(csv)
        ? []
        : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var o in fromSection.Concat(fromCsv))
    {
        var t = o?.Trim();
        if (!string.IsNullOrEmpty(t))
            set.Add(t);
    }

    return set;
}

static void AppendCorsHeadersIfAllowed(HttpContext context, HashSet<string> allowlist)
{
    var origin = context.Request.Headers.Origin.ToString();
    if (string.IsNullOrEmpty(origin) || !allowlist.Contains(origin.Trim()))
        return;
    context.Response.Headers.Append("Access-Control-Allow-Origin", origin.Trim());
    context.Response.Headers.Append("Access-Control-Allow-Credentials", "true");
    context.Response.Headers.Append("Vary", "Origin");
}
