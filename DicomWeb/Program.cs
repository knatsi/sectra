using FellowOakDicom;
using FellowOakDicom.Network;
using FellowOakDicom.Samples.CStoreSCP;

var builder = WebApplication.CreateBuilder(args);

// Configure to listen on all interfaces
builder.WebHost.UseUrls("http://0.0.0.0:5000");

// Add services
builder.Services.Configure<DicomServerConfig>(
    builder.Configuration.GetSection("DicomServer"));

// Register DICOM services
builder.Services.AddSingleton<IDicomServerService, DicomServerService>();
builder.Services.AddSingleton<IDicomFileService, DicomFileService>();

// Add Web API
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo 
    { 
        Title = "DICOM SR Server API", 
        Version = "v1",
        Description = "API for managing DICOM Structured Reports"
    });
});

// Setup DICOM
new DicomSetupBuilder()
    .RegisterServices(s => s.AddFellowOakDicom())
    .Build();

var app = builder.Build();

// Always enable Swagger (remove environment check for testing)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "DICOM SR Server API V1");
    c.RoutePrefix = "swagger";
});

app.UseRouting();
app.MapControllers();

// Add simple endpoints for testing
app.MapGet("/", () => "DICOM SR Server is running! Go to /swagger for API documentation");
app.MapGet("/health", () => new { status = "healthy", timestamp = DateTime.UtcNow });

// Start DICOM server
try
{
    var dicomServerService = app.Services.GetRequiredService<IDicomServerService>();
    await dicomServerService.StartAsync();
    Console.WriteLine("✅ DICOM server started successfully");
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️  DICOM server failed to start: {ex.Message}");
    Console.WriteLine("Web API will still work for testing");
}

Console.WriteLine("🚀 DICOM SR Server and Web API started!");
Console.WriteLine($"📍 Root: http://0.0.0.0:5000/");
Console.WriteLine($"📋 Swagger: http://0.0.0.0:5000/swagger");
Console.WriteLine($"❤️  Health: http://0.0.0.0:5000/health");
Console.WriteLine("Check the PORTS panel in VS Code for your public URLs");
Console.WriteLine("\nPress <return> to end...");

// Run the web API host in background
var cancellationTokenSource = new CancellationTokenSource();
var runTask = app.RunAsync(cancellationTokenSource.Token);

// Wait for user input
Console.ReadLine();

// Shutdown
try
{
    var dicomServerService = app.Services.GetRequiredService<IDicomServerService>();
    await dicomServerService.StopAsync();
}
catch { }

cancellationTokenSource.Cancel();

try
{
    await runTask;
}
catch (OperationCanceledException)
{
    // Expected when cancellation is requested
}