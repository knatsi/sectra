using Microsoft.AspNetCore.Mvc;
using FellowOakDicom;
using System.Text.Json;

[ApiController]
[Route("api/[controller]")]
public class DicomController : ControllerBase
{
    private readonly IDicomFileService _dicomFileService;
    private readonly ILogger<DicomController> _logger;

    public DicomController(IDicomFileService dicomFileService, ILogger<DicomController> logger)
    {
        _dicomFileService = dicomFileService;
        _logger = logger;
    }

    /// <summary>
    /// Get all DICOM files
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<DicomFileInfo>>> GetAllFiles()
    {
        try
        {
            var files = await _dicomFileService.GetAllFilesAsync();
            return Ok(files);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all DICOM files");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get DICOM file info by Instance UID
    /// </summary>
    [HttpGet("instance/{instanceUid}")]
    public async Task<ActionResult<DicomFileInfo>> GetFileInfo(string instanceUid)
    {
        try
        {
            var fileInfo = await _dicomFileService.GetFileInfoByInstanceUidAsync(instanceUid);
            if (fileInfo == null)
            {
                return NotFound($"DICOM file with Instance UID '{instanceUid}' not found");
            }

            return Ok(fileInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving DICOM file info for Instance UID: {InstanceUid}", instanceUid);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get DICOM file content by Instance UID
    /// </summary>
    [HttpGet("instance/{instanceUid}/download")]
    public async Task<IActionResult> DownloadFile(string instanceUid)
    {
        try
        {
            var dicomFile = await _dicomFileService.GetByInstanceUidAsync(instanceUid);
            if (dicomFile == null)
            {
                return NotFound($"DICOM file with Instance UID '{instanceUid}' not found");
            }

            var memoryStream = new MemoryStream();
            await dicomFile.SaveAsync(memoryStream);
            memoryStream.Position = 0;

            return File(memoryStream, "application/dicom", $"{instanceUid}.dcm");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading DICOM file for Instance UID: {InstanceUid}", instanceUid);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get DICOM metadata as JSON by Instance UID
    /// </summary>
    [HttpGet("instance/{instanceUid}/metadata")]
    public async Task<IActionResult> GetMetadata(string instanceUid)
    {
        try
        {
            var dicomFile = await _dicomFileService.GetByInstanceUidAsync(instanceUid);
            if (dicomFile == null)
            {
                return NotFound($"DICOM file with Instance UID '{instanceUid}' not found");
            }

            var metadata = ExtractMetadata(dicomFile.Dataset);
            return Ok(metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving metadata for Instance UID: {InstanceUid}", instanceUid);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get files by Study UID
    /// </summary>
    [HttpGet("study/{studyUid}")]
    public async Task<ActionResult<List<DicomFileInfo>>> GetFilesByStudy(string studyUid)
    {
        try
        {
            var files = await _dicomFileService.GetByStudyUidAsync(studyUid);
            return Ok(files);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving DICOM files for Study UID: {StudyUid}", studyUid);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Delete DICOM file by Instance UID
    /// </summary>
    [HttpDelete("instance/{instanceUid}")]
    public async Task<IActionResult> DeleteFile(string instanceUid)
    {
        try
        {
            var success = await _dicomFileService.DeleteByInstanceUidAsync(instanceUid);
            if (!success)
            {
                return NotFound($"DICOM file with Instance UID '{instanceUid}' not found");
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting DICOM file for Instance UID: {InstanceUid}", instanceUid);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Upload DICOM file
    /// </summary>
    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file provided");
            }

            if (!file.ContentType.Equals("application/dicom", StringComparison.OrdinalIgnoreCase) 
                && !file.FileName.EndsWith(".dcm", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("File must be a DICOM file (.dcm)");
            }

            using var stream = file.OpenReadStream();
            var dicomFile = await DicomFile.OpenAsync(stream);
            
            // Validate it's an SR document
            var sopClassUid = dicomFile.Dataset.GetSingleValue<string>(DicomTag.SOPClassUID);
            if (!IsStructuredReportSopClass(sopClassUid))
            {
                return BadRequest("File must be a DICOM Structured Report");
            }

            // This would trigger the normal storage logic via C-Store if needed
            // For now, we'll just return the file info
            var instanceUid = dicomFile.Dataset.GetSingleValue<string>(DicomTag.SOPInstanceUID);
            
            return Ok(new { 
                Message = "File processed successfully", 
                InstanceUID = instanceUid,
                SOPClassUID = sopClassUid
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading DICOM file");
            return StatusCode(500, "Internal server error");
        }
    }

    private static Dictionary<string, object> ExtractMetadata(DicomDataset dataset)
    {
        var metadata = new Dictionary<string, object>();

        // Common DICOM tags
        var commonTags = new[]
        {
            DicomTag.SOPInstanceUID,
            DicomTag.StudyInstanceUID,
            DicomTag.SeriesInstanceUID,
            DicomTag.PatientID,
            DicomTag.PatientName,
            DicomTag.StudyDate,
            DicomTag.StudyTime,
            DicomTag.Modality,
            DicomTag.SOPClassUID,
            DicomTag.SeriesNumber,
            DicomTag.InstanceNumber,
            DicomTag.StudyDescription,
            DicomTag.SeriesDescription
        };

        foreach (var tag in commonTags)
        {
            if (dataset.Contains(tag))
            {
                var value = dataset.GetSingleValueOrDefault(tag, "");
                metadata[tag.DictionaryEntry.Name] = value;
            }
        }

        return metadata;
    }

    private static bool IsStructuredReportSopClass(string sopClassUid)
    {
        var srSopClasses = new[]
        {
            DicomUID.EnhancedSRStorage.UID,
            DicomUID.ComprehensiveSRStorage.UID,
            DicomUID.BasicTextSRStorage.UID,
            DicomUID.KeyObjectSelectionDocumentStorage.UID,
            DicomUID.MammographyCADSRStorage.UID,
            DicomUID.ChestCADSRStorage.UID,
            DicomUID.ColonCADSRStorage.UID,
            DicomUID.ProcedureLogStorage.UID
        };

        return srSopClasses.Contains(sopClassUid);
    }
}