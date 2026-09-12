using Application.Interfaces;
using Application.Common;
using Application.Services;
using DotNetEnv;
using FluentValidation.AspNetCore;
using Infrastructure.Persistence.Repositories;
using Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Infrastructure.Configurations;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var envPath = Path.Combine(
    builder.Environment.ContentRootPath,
    "..",
    "..",
    ".env"
);

Env.Load(Path.GetFullPath(envPath));

// Database
builder.Services.AddDatabase();

// Unit of Work + Repositories
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

var jwtSettings = new JwtSettings
{
    Secret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? string.Empty,
    Issuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? string.Empty,
    Audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? string.Empty,
    ExpirationMinutes = int.TryParse(
        Environment.GetEnvironmentVariable("JWT_EXPIRATION_MINUTES"),
        out var expiration)
        ? expiration
        : 60
};

if (string.IsNullOrWhiteSpace(jwtSettings.Secret) ||
    Encoding.UTF8.GetByteCount(jwtSettings.Secret) < 32)
{
    throw new InvalidOperationException(
        "JWT_SECRET must be configured with at least 32 characters in .env.");
}

if (string.IsNullOrWhiteSpace(jwtSettings.Issuer))
{
    throw new InvalidOperationException(
        "JWT_ISSUER must be configured in .env.");
}

if (string.IsNullOrWhiteSpace(jwtSettings.Audience))
{
    throw new InvalidOperationException(
        "JWT_AUDIENCE must be configured in .env.");
}

// Register JwtSettings so JwtTokenService can receive IOptions<JwtSettings>
builder.Services.Configure<JwtSettings>(options =>
{
    options.Secret = jwtSettings.Secret;
    options.Issuer = jwtSettings.Issuer;
    options.Audience = jwtSettings.Audience;
    options.ExpirationMinutes = jwtSettings.ExpirationMinutes;
});

builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings.Secret)),

            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,

            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,

            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddScoped<ISemesterService, SemesterService>();
builder.Services.AddScoped<ISemesterRepository, SemesterRepository>();
builder.Services.AddScoped<IExaminationService, ExaminationService>();
builder.Services.AddScoped<IExaminationRepository, ExaminationRepository>();
builder.Services.AddScoped<IExamMaterialService, ExamMaterialService>();
builder.Services.AddScoped<IExamMaterialRepository, ExamMaterialRepository>();
builder.Services.AddHttpClient<SupabaseStorage>();

builder.Services.AddScoped<ISupabaseStorage>(sp =>
{
    var inner = sp.GetRequiredService<SupabaseStorage>();
    return new EncryptedSupabaseStorage(inner);
});

// Controllers
builder.Services.AddControllers();

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();

// CORS
var corsOrigins = (Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS") ?? "")
    .Split(
        ',',
        StringSplitOptions.RemoveEmptyEntries |
        StringSplitOptions.TrimEntries);

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
{
    if (corsOrigins.Length > 0)
    {
        p.WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    }
    else
    {
        p.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    }
}));

// Swagger
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc(
        "v1",
        new OpenApiInfo
        {
            Title = "Backend.Server1",
            Version = "v1"
        });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    server = "Backend.Server1",
    time = DateTime.UtcNow
}));

app.Run();

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    server = "Backend.Server1",
    time = DateTime.UtcNow
}));

app.Run();