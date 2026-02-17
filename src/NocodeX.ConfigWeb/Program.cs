using System.Text.Json;
using NocodeX.Infrastructure.Configuration;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.WriteIndented = true;
});

WebApplication app = builder.Build();

string configPath = Environment.GetEnvironmentVariable("NOCODEX_CONFIG_PATH")
    ?? Path.Combine(Directory.GetCurrentDirectory(), "nocodex.config.json");

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/config", () =>
{
    NocodeXConfiguration config = NocodeXConfiguration.Load(configPath, app.Logger);
    return Results.Ok(config);
});

app.MapPost("/api/config", async (HttpRequest request) =>
{
    NocodeXConfiguration? incoming;

    try
    {
        incoming = await JsonSerializer.DeserializeAsync<NocodeXConfiguration>(request.Body, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }
    catch (JsonException ex)
    {
        app.Logger.LogError(ex, "Invalid payload while saving config");
        return Results.BadRequest(new { message = "JSON non valido" });
    }

    if (incoming is null)
    {
        return Results.BadRequest(new { message = "Payload mancante" });
    }

    Directory.CreateDirectory(Path.GetDirectoryName(configPath) ?? Directory.GetCurrentDirectory());
    string json = JsonSerializer.Serialize(incoming, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(configPath, json);

    app.Logger.LogInformation("Configuration saved to {Path}", configPath);
    return Results.Ok(new { message = "Configurazione salvata", path = configPath });
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
