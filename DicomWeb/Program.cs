using FellowOakDicom;
using FellowOakDicom.Network;
using FellowOakDicom.Samples.CStoreSCP;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.Configure<DicomServerConfig>(
    builder.Configuration.GetSection("DicomServer"));

// Register DICOM services
builder.Services.AddSingleton<IDicomServerService, DicomServerService>();
builder.Services.AddSingleton<IDicomFileService, DicomFileService>();

// Add Web API
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Setup DICOM
new DicomSetupBuilder()
    .RegisterServices(s => s.AddFellowOakDicom())
    .Build();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.MapControllers();

// Start DICOM server
var dicomServerService = app.Services.GetRequiredService<IDicomServerService>();
await dicomServerService.StartAsync();

Console.WriteLine("DICOM SR Server and Web API started.");
Console.WriteLine("Web API available at: http://localhost:5000");
Console.WriteLine("Swagger UI available at: http://localhost:5000/swagger");
Console.WriteLine("Press <return> to end...");

// Run the web API host in background
var cancellationTokenSource = new CancellationTokenSource();
var runTask = app.RunAsync(cancellationTokenSource.Token);

// Wait for user input
Console.ReadLine();

// Shutdown
await dicomServerService.StopAsync();
cancellationTokenSource.Cancel();

try
{
    await runTask;
}
catch (OperationCanceledException)
{
    // Expected when cancellation is requested
}