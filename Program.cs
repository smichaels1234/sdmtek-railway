using Microsoft.EntityFrameworkCore;
using backend.Data;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

var connectionString = GetFirstNonEmpty(
    builder.Configuration.GetConnectionString("SDMTekConnection"),
    builder.Configuration["ConnectionStrings:SDMTekConnection"],
    builder.Configuration["ConnectionStrings__SDMTekConnection"],
    Environment.GetEnvironmentVariable("ConnectionStrings__SDMTekConnection"),
    ConvertDatabaseUrl(Environment.GetEnvironmentVariable("DATABASE_URL"))
);

// Configure Database Context
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

static string GetFirstNonEmpty(params string?[] values)
{
    foreach (var value in values)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }
    }

    return string.Empty;
}

static string? ConvertDatabaseUrl(string? databaseUrl)
{
    if (string.IsNullOrWhiteSpace(databaseUrl))
    {
        return null;
    }

    if (!Uri.TryCreate(databaseUrl, UriKind.Absolute, out var uri))
    {
        return databaseUrl;
    }

    if (uri.Scheme is not "postgres" and not "postgresql")
    {
        return databaseUrl;
    }

    var userInfo = uri.UserInfo.Split(':', 2);
    var username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : string.Empty;
    var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;

    var builder = new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.Port > 0 ? uri.Port : 5432,
        Database = uri.AbsolutePath.Trim('/'),
        Username = username,
        Password = password,
        SslMode = SslMode.Require,
        TrustServerCertificate = true
    };

    return builder.ConnectionString;
}
