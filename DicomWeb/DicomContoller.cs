using Microsoft.AspNetCore.Mvc;
using FellowOakDicom;
using System.Text.Json;

[ApiController]
[Route("")]
public class DicomController : ControllerBase
{
    private readonly IDicomFileService _dicomFileService;
    private readonly ILogger<DicomController> _logger;
    private static readonly CompatibilityInfo CompatibilityInfoV1 = new() { Uid = "HeartProviderAdults", Version = 1 };

    public DicomController(IDicomFileService dicomFileService, ILogger<DicomController> logger)
    {
        _dicomFileService = dicomFileService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the compatibility information about this service.
    /// </summary>
    [HttpGet]
    [Route("v1/GetStructuredDataCompatibility")]
    public CompatibilityInfo GetStructuredDataCompatibility() {
        return CompatibilityInfoV1;
    }

    /// <summary>
    /// Get all DICOM files
    /// </summary>
    [HttpGet]
    [Route("Dicom/list")]
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
    /// Gets the structured data to use in templates.
    /// </summary>  
    [HttpPost]
    [Route("v1/GetStructuredData")]
    public GetStructuredDataResult GetStructuredData([FromBody] GetStructuredDataRequest request) {
        var studyUid = request.Exam?.StudyUid;
        var patientIds = request.Patient?.Ids;
        var filePath = FindMatchingFile(studyUid, patientIds);
            if (filePath == null)
                {
                    throw new FileNotFoundException("Matching XML file not found.");
                }
        var data = ExtractData(studyUid, filePath);
        
        return new GetStructuredDataResult { Compatibility = CompatibilityInfoV1, PropValues = data };
    }

    private Dictionary<string, string> ExtractData(string studyUid, string filePath)
    {
    var result = new Dictionary<string, string>();

    var path = filePath;
    Console.WriteLine($"[DEBUG] Loading file from path: {path}");

    if (!System.IO.File.Exists(path)) {
        Console.WriteLine("[ERROR] File not found.");
        throw new FileNotFoundException($"File not found: {path}");
    }

    var serializer = new XmlSerializer(typeof(MeasurementExport));
    using var reader = new StreamReader(path);
    var export = (MeasurementExport?)serializer.Deserialize(reader);

    if (export?.Patient?.Study == null) {
        Console.WriteLine("[WARN] No Patient study found.");
        return result;
    }
        
    var study = export.Patient.Study;
       Console.WriteLine($"[DEBUG] Found Study(XML): {study.StudyInstanceUID} With StudyID: {study.StudyId}");

    // Only proceed if StudyUID matches request. Correct? What should be checked against?
    if (study.StudyInstanceUID != studyUid) {
        Console.WriteLine($"[WARN] StudyUID(XML) does not match input ({studyUid}). Skipping.");
        return result;
    }

    var parameters = study.Series?.Parameters;
    if (parameters == null) return result;

    foreach (var param in parameters)
    {
        if (!string.IsNullOrEmpty(param.ParameterId))
        {
            result[param.ParameterId + "_" + param.ResultNo] = param.DisplayValue ?? "";
        }
    }

    return result;
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

/// <summary>Contains the information passed to the GetStructuredData endpoint.</summary>
public class GetStructuredDataRequest {
    public required User? User { get; init; }

    public required Patient? Patient { get; init; }

    public required Exam? Exam { get; init; }
}

/// <inheritdoc cref="GetStructuredDataRequest"/>
public class User {
    public required string? Login { get; init; }

    public required string? Domain { get; init; }

    public required string? Name { get; init; }
}

/// <inheritdoc cref="GetStructuredDataRequest"/>
public class Patient {
    public required string[]? Ids { get; init; }
}

/// <inheritdoc cref="GetStructuredDataRequest"/>
public class Exam {
    public required string? ExamNo { get; init; }

    public required string? AccNo { get; init; }

    public required string? StudyUid { get; init; }

    public required DateTime? Date { get; init; }
}

/// <summary>Contains the information returned from the GetStructuredData endpoint.</summary>
public class GetStructuredDataResult {
    [JsonProperty(PropertyName = "compatibility")]
    public required CompatibilityInfo Compatibility { get; init; }

    [JsonProperty(PropertyName = "propValues")]
    public required IReadOnlyDictionary<string, string> PropValues { get; init; }
}

/// <summary>Contains compatibility information for the data provider.</summary>
public class CompatibilityInfo {
    public required string Uid { get; init; }

    public required int Version { get; init; }
}