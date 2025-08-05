using FellowOakDicom.Network;
using FellowOakDicom.Samples.CStoreSCP;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public interface IDicomServerService
{
    Task StartAsync();
    Task StopAsync();
}

public class DicomServerService : IDicomServerService
{
    private readonly ILogger<DicomServerService> _logger;
    private readonly DicomServerConfig _config;
    private IDicomServer? _server;

    public DicomServerService(ILogger<DicomServerService> logger, IOptions<DicomServerConfig> config)
    {
        _logger = logger;
        _config = config.Value;
    }

    public Task StartAsync()
    {
        try
        {
            var userState = new StoreScpSettings
            {
                AeTitle = _config.AETitle ?? "DICOMSRSERVER",
                StoragePath = _config.StoragePath ?? @".\DICOM"
            };

            _server = DicomServerFactory.Create<StoreScp>(_config.Port, userState: userState);
            
            _logger.LogInformation(
                "Started DICOM C-Store SCP server on port {Port}, AE Title '{AETitle}', Storage Path '{StoragePath}'",
                _config.Port,
                userState.AeTitle,
                userState.StoragePath);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start DICOM server");
            throw;
        }
    }

    public Task StopAsync()
    {
        try
        {
            _server?.Dispose();
            _logger.LogInformation("DICOM server stopped");
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping DICOM server");
            throw;
        }
    }
}