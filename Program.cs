using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
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

// CORS — allow your React frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("GameClient", policy =>
    {
        policy.WithOrigins(
        "http://localhost:3000",
        "http://localhost:5173",
        "https://insomnicode-odin.vercel.app");
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("GameClient");
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
