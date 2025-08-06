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

            _logger.LogDebug("Loading DICOM file: {FilePath}", filePath);
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
            {
                _logger.LogWarning("Storage path does not exist: {StoragePath}", _storagePath);
                return files;
            }

            // Search for all .dcm files recursively
            var dicomFiles = Directory.GetFiles(_storagePath, "*.dcm", SearchOption.AllDirectories);
            _logger.LogDebug("Found {Count} DICOM files in storage", dicomFiles.Length);
            
            foreach (var filePath in dicomFiles)
            {
                var fileInfo = await GetFileInfoFromPath(filePath);
                if (fileInfo != null)
                    files.Add(fileInfo);
            }

            _logger.LogInformation("Successfully loaded {Count} DICOM files", files.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all DICOM files from {StoragePath}", _storagePath);
        }

        return files;
    }

    public async Task<List<DicomFileInfo>> GetByStudyUidAsync(string studyUid)
    {
        var files = new List<DicomFileInfo>();
        
        try
        {
            if (!Directory.Exists(_storagePath))
            {
                _logger.LogWarning("Storage path does not exist: {StoragePath}", _storagePath);
                return files;
            }

            // First try the organized structure: StudyUID/InstanceUID.dcm
            var studyPath = Path.Combine(_storagePath, studyUid);
            if (Directory.Exists(studyPath))
            {
                var studyFiles = Directory.GetFiles(studyPath, "*.dcm");
                _logger.LogDebug("Found {Count} files in study directory: {StudyPath}", studyFiles.Length, studyPath);
                
                foreach (var filePath in studyFiles)
                {
                    var fileInfo = await GetFileInfoFromPath(filePath);
                    if (fileInfo != null && fileInfo.StudyUID == studyUid)
                        files.Add(fileInfo);
                }
            }

            // Also search all files recursively and filter by StudyUID
            // This handles cases where files aren't organized by study folders
            var allDicomFiles = Directory.GetFiles(_storagePath, "*.dcm", SearchOption.AllDirectories);
            
            foreach (var filePath in allDicomFiles)
            {
                // Skip if we already processed this file in the study directory
                if (files.Any(f => f.FilePath == filePath))
                    continue;

                var fileInfo = await GetFileInfoFromPath(filePath);
                if (fileInfo != null && fileInfo.StudyUID == studyUid)
                    files.Add(fileInfo);
            }

            _logger.LogInformation("Found {Count} DICOM files for Study UID: {StudyUid}", files.Count, studyUid);
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

        // Method 1: Look for the organized structure pattern: StudyUID/InstanceUID.dcm
        var searchPattern = $"{instanceUid}.dcm";
        var files = Directory.GetFiles(_storagePath, searchPattern, SearchOption.AllDirectories);
        
        if (files.Length > 0)
        {
            _logger.LogDebug("Found file using organized structure: {FilePath}", files[0]);
            return files[0];
        }

        // Method 2: Search all .dcm files and check their Instance UID by reading the file
        var allDicomFiles = Directory.GetFiles(_storagePath, "*.dcm", SearchOption.AllDirectories);
        _logger.LogDebug("Searching through {Count} DICOM files for Instance UID: {InstanceUid}", allDicomFiles.Length, instanceUid);

        foreach (var filePath in allDicomFiles)
        {
            try
            {
                // Quick check: if filename contains the instance UID, prioritize it
                if (Path.GetFileNameWithoutExtension(filePath).Contains(instanceUid))
                {
                    _logger.LogDebug("Found file by filename pattern: {FilePath}", filePath);
                    return filePath;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error checking filename pattern for: {FilePath}", filePath);
                continue;
            }
        }

        // Method 3: Actually read DICOM files to find the matching Instance UID (slower but thorough)
        foreach (var filePath in allDicomFiles)
        {
            try
            {
                var dicomFile = DicomFile.Open(filePath);
                var fileInstanceUid = dicomFile.Dataset.GetSingleValueOrDefault(DicomTag.SOPInstanceUID, "");
                if (fileInstanceUid == instanceUid)
                {
                    _logger.LogDebug("Found file by reading DICOM header: {FilePath}", filePath);
                    return filePath;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error reading DICOM file: {FilePath}", filePath);
                continue;
            }
        }

        _logger.LogDebug("No file found for Instance UID: {InstanceUid}", instanceUid);
        return null;
    }

    private async Task<DicomFileInfo?> GetFileInfoFromPath(string filePath)
    {
        try
        {
            var dicomFile = await DicomFile.OpenAsync(filePath);
            var dataset = dicomFile.Dataset;

            var fileInfo = new DicomFileInfo
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

            _logger.LogDebug("Loaded file info: {InstanceUID} from {FilePath}", fileInfo.InstanceUID, filePath);
            return fileInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading DICOM file info from: {FilePath}", filePath);
            return null;
        }
    }
}