using Microsoft.EntityFrameworkCore;
using backend.Data;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Configure Database Context
var connectionString = ResolveConnectionString(builder.Configuration);
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "No PostgreSQL connection string found. Set ConnectionStrings__SDMTekConnection or DATABASE_URL.");
}

builder.Services.AddDbContext<SDMTekContext>(options =>
    options.UseNpgsql(connectionString));

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();

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

var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

try
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<SDMTekContext>();
    dbContext.Database.Migrate();
    startupLogger.LogInformation("Database migration check completed.");
}
catch (Exception ex)
{
    startupLogger.LogCritical(ex, "Failed to initialize database.");
    throw;
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
    var configuredConnection = configuration.GetConnectionString("SDMTekConnection");
    if (!string.IsNullOrWhiteSpace(configuredConnection))
    {
        return configuredConnection;
    }

    var databaseUrl = configuration["DATABASE_URL"] ?? configuration["DATABASE_PUBLIC_URL"];
    if (string.IsNullOrWhiteSpace(databaseUrl))
    {
        return null;
    }

    if (!Uri.TryCreate(databaseUrl, UriKind.Absolute, out var databaseUri) ||
        (databaseUri.Scheme != "postgres" && databaseUri.Scheme != "postgresql"))
    {
        return databaseUrl;
    }

    var userInfo = databaseUri.UserInfo.Split(':', 2, StringSplitOptions.TrimEntries);
    var username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : string.Empty;
    var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;

    var builder = new NpgsqlConnectionStringBuilder
    {
        Host = databaseUri.Host,
        Port = databaseUri.Port > 0 ? databaseUri.Port : 5432,
        Database = databaseUri.AbsolutePath.Trim('/'),
        Username = username,
        Password = password,
        SslMode = SslMode.Require,
        TrustServerCertificate = true
    };

    return builder.ConnectionString;
}
