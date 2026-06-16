using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Configure Database Context
var connectionString = ResolveConnectionString(builder.Configuration);
var hasConnectionString = !string.IsNullOrWhiteSpace(connectionString);
if (hasConnectionString)
{
    builder.Services.AddDbContext<SDMTekContext>(options =>
        options.UseNpgsql(connectionString));
}
else
{
    builder.Services.AddDbContext<SDMTekContext>(options =>
        options.UseInMemoryDatabase("sdmtek-fallback"));
}

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();

var captchaSecretKey = ResolveCaptchaSecretKey(builder.Configuration);
builder.Services.AddOptions<CaptchaOptions>()
    .Configure(options => options.SecretKey = captchaSecretKey);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular",
        policy =>
        {
            if (allowedOrigins.Length > 0)
            {
                policy
                    .WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            }
            else
            {
                policy
                    .WithOrigins("http://localhost:4202")
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            }
        });
});

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddHttpClient();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (string.IsNullOrWhiteSpace(captchaSecretKey))
{
    app.Logger.LogWarning(
        "Captcha secret key is missing. Configure one of: Captcha__SecretKey, RECAPTCHA_SECRET_KEY, or GOOGLE_RECAPTCHA_SECRET.");
}
else
{
    app.Logger.LogInformation("Captcha secret key is configured.");
}

if (!hasConnectionString)
{
    var envProbe = BuildConnectionEnvProbe(builder.Configuration);
    app.Logger.LogWarning(
        "Connection string 'ConnectionStrings:SDMTekConnection' is missing. " +
        "Running with in-memory fallback database. Configure one of: " +
        "'ConnectionStrings__SDMTekConnection', 'SDMTekConnection', 'DATABASE_URL', 'POSTGRES_URL', or PG* vars. {Probe}",
        envProbe);
}

// Ensure schema is created/updated at startup so DB issues surface immediately.
if (hasConnectionString)
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<SDMTekContext>();
        try
        {
            db.Database.Migrate();
            app.Logger.LogInformation("Database migration check completed successfully.");
        }
        catch (Exception ex)
        {
            app.Logger.LogCritical(ex, "Database migration failed during startup.");
            throw;
        }
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// CRITICAL: UseStaticFiles must be before UseRouting for Azure
app.UseStaticFiles();
app.UseCors("AllowAngular");

app.UseRouting();

app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok("ok"));

// Fallback to index.html for Angular routing
app.MapFallbackToFile("index.html");

app.Run();

static string? ResolveConnectionString(IConfiguration configuration)
{
    var raw = configuration.GetConnectionString("SDMTekConnection")
        ?? configuration["SDMTekConnection"]
        ?? configuration["DATABASE_URL"]
        ?? configuration["POSTGRES_URL"]
        ?? configuration["POSTGRESQL_URL"]
        ?? Environment.GetEnvironmentVariable("ConnectionStrings__SDMTekConnection")
        ?? Environment.GetEnvironmentVariable("SDMTekConnection")
        ?? Environment.GetEnvironmentVariable("DATABASE_URL")
        ?? Environment.GetEnvironmentVariable("POSTGRES_URL")
        ?? Environment.GetEnvironmentVariable("POSTGRESQL_URL");

    if (string.IsNullOrWhiteSpace(raw))
    {
        return null;
    }

    var trimmed = raw.Trim();
    if (trimmed.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
        || trimmed.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        var uri = new Uri(trimmed);
        var userInfo = uri.UserInfo.Split(':', 2);
        var username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : string.Empty;
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
        var database = uri.AbsolutePath.Trim('/');

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port,
            Database = database,
            Username = username,
            Password = password,
            SslMode = SslMode.Require
        };

        return builder.ConnectionString;
    }

    // Fallback for platforms that expose PostgreSQL settings as separate vars.
    var pgHost = Environment.GetEnvironmentVariable("PGHOST") ?? configuration["PGHOST"];
    var pgPortRaw = Environment.GetEnvironmentVariable("PGPORT") ?? configuration["PGPORT"];
    var pgDatabase = Environment.GetEnvironmentVariable("PGDATABASE") ?? configuration["PGDATABASE"];
    var pgUser = Environment.GetEnvironmentVariable("PGUSER") ?? configuration["PGUSER"];
    var pgPassword = Environment.GetEnvironmentVariable("PGPASSWORD") ?? configuration["PGPASSWORD"];

    if (!string.IsNullOrWhiteSpace(pgHost)
        && !string.IsNullOrWhiteSpace(pgDatabase)
        && !string.IsNullOrWhiteSpace(pgUser)
        && !string.IsNullOrWhiteSpace(pgPassword))
    {
        var pgBuilder = new NpgsqlConnectionStringBuilder
        {
            Host = pgHost,
            Database = pgDatabase,
            Username = pgUser,
            Password = pgPassword,
            SslMode = SslMode.Require
        };

        if (int.TryParse(pgPortRaw, out var pgPort))
        {
            pgBuilder.Port = pgPort;
        }

        return pgBuilder.ConnectionString;
    }

    return trimmed;
}

static string BuildConnectionEnvProbe(IConfiguration configuration)
{
    static string State(string? value) => string.IsNullOrWhiteSpace(value) ? "missing" : "present";

    var known = new (string Key, string? Value)[]
    {
        ("ConnectionStrings__SDMTekConnection", Environment.GetEnvironmentVariable("ConnectionStrings__SDMTekConnection") ?? configuration["ConnectionStrings:SDMTekConnection"]),
        ("SDMTekConnection", Environment.GetEnvironmentVariable("SDMTekConnection") ?? configuration["SDMTekConnection"]),
        ("DATABASE_URL", Environment.GetEnvironmentVariable("DATABASE_URL") ?? configuration["DATABASE_URL"]),
        ("POSTGRES_URL", Environment.GetEnvironmentVariable("POSTGRES_URL") ?? configuration["POSTGRES_URL"]),
        ("POSTGRESQL_URL", Environment.GetEnvironmentVariable("POSTGRESQL_URL") ?? configuration["POSTGRESQL_URL"]),
        ("PGHOST", Environment.GetEnvironmentVariable("PGHOST") ?? configuration["PGHOST"]),
        ("PGPORT", Environment.GetEnvironmentVariable("PGPORT") ?? configuration["PGPORT"]),
        ("PGDATABASE", Environment.GetEnvironmentVariable("PGDATABASE") ?? configuration["PGDATABASE"]),
        ("PGUSER", Environment.GetEnvironmentVariable("PGUSER") ?? configuration["PGUSER"]),
        ("PGPASSWORD", Environment.GetEnvironmentVariable("PGPASSWORD") ?? configuration["PGPASSWORD"])
    };

    var knownSummary = string.Join(
        ", ",
        known.Select(k => $"{k.Key}={State(k.Value)}"));

    var discovered = Environment.GetEnvironmentVariables()
        .Keys
        .Cast<object>()
        .Select(k => k?.ToString() ?? string.Empty)
        .Where(k => !string.IsNullOrWhiteSpace(k))
        .Where(k => k.Contains("DATABASE", StringComparison.OrdinalIgnoreCase)
            || k.Contains("POSTGRES", StringComparison.OrdinalIgnoreCase)
            || k.Contains("PG", StringComparison.OrdinalIgnoreCase))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    var discoveredSummary = discovered.Length == 0
        ? "none"
        : string.Join("|", discovered);

    return $"Probe: {knownSummary}. Discovered env keys: {discoveredSummary}.";
}

static string? ResolveCaptchaSecretKey(IConfiguration configuration)
{
    var secretKey = configuration["Captcha:SecretKey"]
        ?? configuration["Captcha__SecretKey"]
        ?? Environment.GetEnvironmentVariable("Captcha__SecretKey")
        ?? Environment.GetEnvironmentVariable("CAPTCHA__SECRETKEY")
        ?? Environment.GetEnvironmentVariable("RECAPTCHA_SECRET_KEY")
        ?? Environment.GetEnvironmentVariable("GOOGLE_RECAPTCHA_SECRET");

    return string.IsNullOrWhiteSpace(secretKey) ? null : secretKey.Trim();
}
