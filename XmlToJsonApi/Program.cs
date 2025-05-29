using System.Text.Json;
using System.Xml.Serialization;
using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Load appsettings.json
builder.Configuration.AddJsonFile("appsettings.json");

var app = builder.Build();

// Read values from appsettings.json
var xmlFolderPath = builder.Configuration["XmlSettings:FolderPath"] ?? "Data";
var xmlFileName = builder.Configuration["XmlSettings:FileName"] ?? "input.xml";
var xmlFilePath = Path.Combine(xmlFolderPath, xmlFileName);


// GET: Get("v1/GetStructuredDataCompatibility")
app.MapGet("/v1/GetStructuredDataCompatibility", () =>
{
    var compatibilityInfoV1 = new CompatibilityInfo
    {
        Uid = "ExampleFormsDataProvider",
        Version = 1
    };
    return compatibilityInfoV1;
});

// POST: v1/GetStructuredData → Read input.xml and return parsed JSON
app.MapPost("/v1/GetStructuredData", () =>
{
    if (!File.Exists(xmlFilePath))
        return Results.NotFound($"XML file not found: {xmlFilePath}");

    try
    {
        var serializer = new XmlSerializer(typeof(MeasurementExport));
        using var reader = new StreamReader(xmlFilePath);
        var data = (MeasurementExport?)serializer.Deserialize(reader);

        if (data == null)
            return Results.BadRequest("Failed to parse XML.");

        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        return Results.Text(json, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error: {ex.Message}");
    }
});

app.Run();