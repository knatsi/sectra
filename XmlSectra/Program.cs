using System.Xml.Serialization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.json");
builder.Services.Configure<JsonOptions>(options =>
{
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
});
var app = builder.Build();



// GET: /v1/GetStructuredDataCompatibility
app.MapGet("/v1/GetStructuredDataCompatibility", () =>
{
    var compatibilityInfo = new CompatibilityInfo
    {
        Uid = "HeartProviderAdults",
        Version = 1
    };
    return Results.Ok(compatibilityInfo);
});

// POST: /v1/GetStructuredData
app.MapPost("/v1/GetStructuredData", async (HttpContext context) =>
{
    var request = await JsonSerializer.DeserializeAsync<GetStructuredDataRequest>(context.Request.Body);
    if (request == null)
        return Results.BadRequest("Missing request data.");
    
    Console.WriteLine($"User: {JsonSerializer.Serialize(request.User)}");
    Console.WriteLine($"Patient: {JsonSerializer.Serialize(request.Patient)}");
    Console.WriteLine($"Exam: {JsonSerializer.Serialize(request.Exam)}");
    
    if (request.Exam == null)
        return Results.BadRequest("Missing exam data.");

    var folder = builder.Configuration["XmlSettings:FolderPath"] ?? "Data";
    var file = builder.Configuration["XmlSettings:FileName"] ?? "HeartProviderAdults.xml";
    var path = Path.Combine(folder, file);

    if (!File.Exists(path))
        return Results.NotFound($"File not found: {path}");

    try
    {
        var serializer = new XmlSerializer(typeof(MeasurementExport));
        using var reader = new StreamReader(path);
        var export = (MeasurementExport?)serializer.Deserialize(reader);
        if (export == null)
            return Results.BadRequest("Failed to parse XML.");

        var data = ExtractData(export);
        var response = new GetStructuredDataResult
        {
            Compatibility = new CompatibilityInfo { Uid = "HeartProviderAdults", Version = 1 },
            PropValues = data
        };
        return Results.Json(response);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

app.Run();

Dictionary<string, object> ExtractData(MeasurementExport export)
{
    var result = new Dictionary<string, object>();
    FlattenObject(result, export, "root");
    return result;
}

void FlattenObject(Dictionary<string, object> dict, object? obj, string prefix)
{
    if (obj == null)
        return;

    var type = obj.GetType();

    if (type.IsPrimitive || obj is string || obj is DateTime || obj is decimal || obj is double || obj is float)
    {
        dict[prefix] = obj;
        return;
    }

    if (obj is IEnumerable<object> list)
    {
        int index = 0;
        foreach (var item in list)
        {
            FlattenObject(dict, item, $"{prefix}[{index}]");
            index++;
        }
        return;
    }

    foreach (var prop in type.GetProperties())
    {
        var value = prop.GetValue(obj);
        var name = $"{prefix}.{prop.Name}";

        if (value is System.Collections.IEnumerable enumerable && value is not string)
        {
            int i = 0;
            foreach (var element in enumerable)
            {
                FlattenObject(dict, element, $"{name}[{i}]");
                i++;
            }
        }
        else
        {
            FlattenObject(dict, value, name);
        }
    }
}