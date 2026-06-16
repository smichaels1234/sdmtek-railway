using Microsoft.EntityFrameworkCore;
using backend.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Configure Database Context
var connectionString = builder.Configuration.GetConnectionString("SDMTekConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'ConnectionStrings:SDMTekConnection' is missing. " +
        "Set environment variable 'ConnectionStrings__SDMTekConnection'.");
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
