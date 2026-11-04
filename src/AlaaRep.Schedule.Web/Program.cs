using System.IO;
using AlaaRep.Schedule.Core.Models;
using AlaaRep.Schedule.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Register Core services
builder.Services.AddSingleton<EvmCalculationEngine>();
builder.Services.AddSingleton<ExcelScheduleParser>();
builder.Services.AddSingleton<SampleScheduleGenerator>();

// Add CORS and Static Files
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

// In-memory current schedule state
ScheduleProjectSummary? currentSummary = null;

// Endpoint: Download Sample Schedule Excel Template
app.MapGet("/api/schedule/sample", (SampleScheduleGenerator generator) =>
{
    var bytes = generator.GenerateSampleWorkbookBytes();
    return Results.File(
        bytes,
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "Royat_Al_Ebtikar_Schedule.xlsx"
    );
});

// Endpoint: Default/Demo Schedule Data
app.MapGet("/api/schedule/default-data", (SampleScheduleGenerator generator, ExcelScheduleParser parser) =>
{
    if (currentSummary == null)
    {
        var bytes = generator.GenerateSampleWorkbookBytes();
        using var stream = new MemoryStream(bytes);
        currentSummary = parser.Parse(stream);
    }
    return Results.Ok(currentSummary);
});

// Endpoint: Upload Schedule Excel File
app.MapPost("/api/schedule/upload", async (HttpRequest request, ExcelScheduleParser parser) =>
{
    if (!request.HasFormContentType || !request.Form.Files.Any())
    {
        return Results.BadRequest(new { error = "No Excel file was uploaded. Please provide an .xlsx file." });
    }

    var file = request.Form.Files[0];
    if (file.Length == 0)
    {
        return Results.BadRequest(new { error = "The uploaded file is empty." });
    }

    try
    {
        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        stream.Position = 0;

        var summary = parser.Parse(stream);
        summary.ProjectTitle = Path.GetFileNameWithoutExtension(file.FileName);
        currentSummary = summary;

        return Results.Ok(summary);
    }
    catch (System.Exception ex)
    {
        return Results.BadRequest(new { error = $"Error reading schedule Excel: {ex.Message}" });
    }
});

// Endpoint: Recalculate Lookahead with custom days
app.MapGet("/api/schedule/lookahead", (int days, EvmCalculationEngine engine) =>
{
    if (currentSummary == null)
    {
        return Results.NotFound(new { error = "No schedule data loaded." });
    }
    var baseDate = currentSummary.DataDate ?? System.DateTime.Today;
    var sourceActivities = (currentSummary.DetailedActivities != null && currentSummary.DetailedActivities.Any())
        ? currentSummary.DetailedActivities
        : currentSummary.Activities;
    var lookaheadItems = engine.CalculateLookahead(sourceActivities, baseDate, days);
    return Results.Ok(lookaheadItems);
});

// Endpoint: Get Current Summary
app.MapGet("/api/schedule/summary", () =>
{
    if (currentSummary == null)
    {
        return Results.NotFound(new { error = "No schedule loaded yet. Please upload an Excel sheet or load sample data." });
    }
    return Results.Ok(currentSummary);
});

// Endpoint: Health check
app.MapGet("/api/health", () => Results.Ok(new { status = "online", timestamp = System.DateTime.UtcNow }));

app.Run();

// Required for WebApplicationFactory in integration tests
public partial class Program { }
