public class DicomFileInfo
{
    public string FilePath { get; set; } = string.Empty;
    public string InstanceUID { get; set; } = string.Empty;
    public string StudyUID { get; set; } = string.Empty;
    public string SeriesUID { get; set; } = string.Empty;
    public string PatientID { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string StudyDate { get; set; } = string.Empty;
    public string Modality { get; set; } = string.Empty;
    public string SOPClassUID { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime CreatedDate { get; set; }
}