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
        "Connection string 'ConnectionStrings:SDMTekConnection' is missing. " +
    "Set one of: 'ConnectionStrings__SDMTekConnection', 'SDMTekConnection', or 'DATABASE_URL'.");
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

// Ensure schema is created/updated at startup so DB issues surface immediately.
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
        ?? Environment.GetEnvironmentVariable("ConnectionStrings__SDMTekConnection")
        ?? Environment.GetEnvironmentVariable("SDMTekConnection")
        ?? Environment.GetEnvironmentVariable("DATABASE_URL");

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

    return trimmed;
}
