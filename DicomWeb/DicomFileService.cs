using FellowOakDicom;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public interface IDicomFileService
{
    Task<DicomFile?> GetByInstanceUidAsync(string instanceUid);
    Task<List<DicomFileInfo>> GetAllFilesAsync();
    Task<List<DicomFileInfo>> GetByStudyUidAsync(string studyUid);
    Task<bool> DeleteByInstanceUidAsync(string instanceUid);
    Task<DicomFileInfo?> GetFileInfoByInstanceUidAsync(string instanceUid);
}

public class DicomFileService : IDicomFileService
{
    private readonly ILogger<DicomFileService> _logger;
    private readonly string _storagePath;

    public DicomFileService(ILogger<DicomFileService> logger, IOptions<DicomServerConfig> config)
    {
        _logger = logger;
        _storagePath = Path.GetFullPath(config.Value.StoragePath ?? @".\DICOM");
    }

    public async Task<DicomFile?> GetByInstanceUidAsync(string instanceUid)
    {
        try
        {
            var filePath = FindFileByInstanceUid(instanceUid);
            if (filePath == null)
            {
                _logger.LogWarning("File not found for Instance UID: {InstanceUid}", instanceUid);
                return null;
            }

            return await DicomFile.OpenAsync(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading DICOM file for Instance UID: {InstanceUid}", instanceUid);
            return null;
        }
    }

    public async Task<List<DicomFileInfo>> GetAllFilesAsync()
    {
        var files = new List<DicomFileInfo>();
        
        try
        {
            if (!Directory.Exists(_storagePath))
                return files;

            var dicomFiles = Directory.GetFiles(_storagePath, "*.dcm", SearchOption.AllDirectories);
            
            foreach (var filePath in dicomFiles)
            {
                var fileInfo = await GetFileInfoFromPath(filePath);
                if (fileInfo != null)
                    files.Add(fileInfo);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all DICOM files");
        }

        return files;
    }

    public async Task<List<DicomFileInfo>> GetByStudyUidAsync(string studyUid)
    {
        var files = new List<DicomFileInfo>();
        
        try
        {
            var studyPath = Path.Combine(_storagePath, studyUid);
            if (!Directory.Exists(studyPath))
                return files;

            var dicomFiles = Directory.GetFiles(studyPath, "*.dcm");
            
            foreach (var filePath in dicomFiles)
            {
                var fileInfo = await GetFileInfoFromPath(filePath);
                if (fileInfo != null)
                    files.Add(fileInfo);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving DICOM files for Study UID: {StudyUid}", studyUid);
        }

        return files;
    }

    public Task<bool> DeleteByInstanceUidAsync(string instanceUid)
    {
        try
        {
            var filePath = FindFileByInstanceUid(instanceUid);
            if (filePath == null)
            {
                _logger.LogWarning("File not found for deletion, Instance UID: {InstanceUid}", instanceUid);
                return Task.FromResult(false);
            }

            File.Delete(filePath);
            _logger.LogInformation("Deleted DICOM file for Instance UID: {InstanceUid}", instanceUid);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting DICOM file for Instance UID: {InstanceUid}", instanceUid);
            return Task.FromResult(false);
        }
    }

    public async Task<DicomFileInfo?> GetFileInfoByInstanceUidAsync(string instanceUid)
    {
        try
        {
            var filePath = FindFileByInstanceUid(instanceUid);
            if (filePath == null)
                return null;

            return await GetFileInfoFromPath(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file info for Instance UID: {InstanceUid}", instanceUid);
            return null;
        }
    }

    private string? FindFileByInstanceUid(string instanceUid)
    {
        if (!Directory.Exists(_storagePath))
            return null;

        // Look for the file pattern: StudyUID/InstanceUID.dcm
        var searchPattern = $"{instanceUid}.dcm";
        var files = Directory.GetFiles(_storagePath, searchPattern, SearchOption.AllDirectories);
        
        return files.FirstOrDefault();
    }

    private async Task<DicomFileInfo?> GetFileInfoFromPath(string filePath)
    {
        try
        {
            var dicomFile = await DicomFile.OpenAsync(filePath);
            var dataset = dicomFile.Dataset;

            return new DicomFileInfo
            {
                FilePath = filePath,
                InstanceUID = dataset.GetSingleValueOrDefault(DicomTag.SOPInstanceUID, ""),
                StudyUID = dataset.GetSingleValueOrDefault(DicomTag.StudyInstanceUID, ""),
                SeriesUID = dataset.GetSingleValueOrDefault(DicomTag.SeriesInstanceUID, ""),
                PatientID = dataset.GetSingleValueOrDefault(DicomTag.PatientID, ""),
                PatientName = dataset.GetSingleValueOrDefault(DicomTag.PatientName, ""),
                StudyDate = dataset.GetSingleValueOrDefault(DicomTag.StudyDate, ""),
                Modality = dataset.GetSingleValueOrDefault(DicomTag.Modality, ""),
                SOPClassUID = dataset.GetSingleValueOrDefault(DicomTag.SOPClassUID, ""),
                FileSize = new FileInfo(filePath).Length,
                CreatedDate = File.GetCreationTime(filePath)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading DICOM file info from: {FilePath}", filePath);
            return null;
        }
    }
}