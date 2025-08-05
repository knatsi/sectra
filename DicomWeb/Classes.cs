public class DicomServerConfig
{
    public int Port { get; set; }
    public string? AETitle { get; set; }
    public string? StoragePath { get; set; }
}

public class StoreScpSettings
{
    public string? AeTitle { get; set; }
    public string? StoragePath { get; set; }
}