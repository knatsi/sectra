using Microsoft.AspNetCore.Mvc;
using FellowOakDicom;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

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
    public CompatibilityInfo GetStructuredDataCompatibility() 
    {
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
    /// Gets the structured data to use in templates from DICOM files.
    /// </summary>  
    [HttpPost]
    [Route("v1/GetStructuredData")]
    public async Task<ActionResult<GetStructuredDataResult>> GetStructuredData([FromBody] GetStructuredDataRequest request) 
    {
        try
        {
            var studyUid = request.Exam?.StudyUid;
            if (string.IsNullOrEmpty(studyUid))
            {
                _logger.LogWarning("No StudyUID provided in request");
                return BadRequest("StudyUID is required");
            }

            // Get all DICOM files for the study
            var dicomFiles = await _dicomFileService.GetByStudyUidAsync(studyUid);
            
            if (!dicomFiles.Any())
            {
                _logger.LogWarning("No DICOM files found for StudyUID: {StudyUID}", studyUid);
                return NotFound($"No DICOM files found for StudyUID: {studyUid}");
            }

            // Extract structured data from DICOM files
            var data = await ExtractDataFromDicomFiles(studyUid, dicomFiles);
            
            return Ok(new GetStructuredDataResult 
            { 
                Compatibility = CompatibilityInfoV1, 
                PropValues = data 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting structured data for StudyUID: {StudyUID}", request.Exam?.StudyUid);
            return StatusCode(500, "Internal server error");
        }
    }

    private async Task<Dictionary<string, string>> ExtractDataFromDicomFiles(string studyUid, List<DicomFileInfo> dicomFiles)
    {
        var result = new Dictionary<string, string>();

        _logger.LogDebug("Extracting data from {Count} DICOM files for StudyUID: {StudyUID}", dicomFiles.Count, studyUid);

        foreach (var fileInfo in dicomFiles)
        {
            try
            {
                // Only process Structured Report DICOM files
                if (!IsStructuredReportSopClass(fileInfo.SOPClassUID))
                {
                    _logger.LogDebug("Skipping non-SR file: {FilePath} (SOP Class: {SOPClass})", fileInfo.FilePath, fileInfo.SOPClassUID);
                    continue;
                }

                var dicomFile = await _dicomFileService.GetByInstanceUidAsync(fileInfo.InstanceUID);
                if (dicomFile == null)
                {
                    _logger.LogWarning("Could not load DICOM file: {FilePath}", fileInfo.FilePath);
                    continue;
                }

                var measurements = ExtractMeasurementsFromSR(dicomFile.Dataset);
                foreach (var measurement in measurements)
                {
                    result[measurement.Key] = measurement.Value;
                }

                _logger.LogDebug("Extracted {Count} measurements from {FilePath}", measurements.Count, fileInfo.FilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing DICOM file: {FilePath}", fileInfo.FilePath);
                continue;
            }
        }

        _logger.LogInformation("Total extracted measurements: {Count} for StudyUID: {StudyUID}", result.Count, studyUid);
        return result;
    }

    private Dictionary<string, string> ExtractMeasurementsFromSR(DicomDataset dataset)
    {
        var measurements = new Dictionary<string, string>();

        try
        {
            // Check if this is a structured report
            var sopClassUid = dataset.GetSingleValueOrDefault(DicomTag.SOPClassUID, "");
            if (!IsStructuredReportSopClass(sopClassUid))
            {
                return measurements;
            }

            // Extract content from the Content Sequence
            if (dataset.Contains(DicomTag.ContentSequence))
            {
                var contentSequence = dataset.GetSequence(DicomTag.ContentSequence);
                ProcessContentSequence(contentSequence, measurements, "");
            }

            _logger.LogDebug("Extracted {Count} measurements from SR dataset", measurements.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting measurements from SR dataset");
        }

        return measurements;
    }

    private void ProcessContentSequence(DicomSequence contentSequence, Dictionary<string, string> measurements, string prefix)
    {
        for (int i = 0; i < contentSequence.Items.Count; i++)
        {
            var item = contentSequence.Items[i];
            
            try
            {
                var valueType = item.GetSingleValueOrDefault(DicomTag.ValueType, "");
                var conceptNameSequence = item.GetSequence(DicomTag.ConceptNameCodeSequence);
                
                string conceptName = "";
                string codeValue = "";
                
                if (conceptNameSequence != null && conceptNameSequence.Items.Count > 0)
                {
                    var conceptItem = conceptNameSequence.Items[0];
                    conceptName = conceptItem.GetSingleValueOrDefault(DicomTag.CodeMeaning, "");
                    codeValue = conceptItem.GetSingleValueOrDefault(DicomTag.CodeValue, "");
                }

                // Process numeric measurements
                if (valueType == "NUM")
                {
                    ProcessNumericMeasurement(item, measurements, conceptName, codeValue, i);
                }
                // Process text values
                else if (valueType == "TEXT")
                {
                    var textValue = item.GetSingleValueOrDefault(DicomTag.TextValue, "");
                    if (!string.IsNullOrEmpty(conceptName) && !string.IsNullOrEmpty(textValue))
                    {
                        var key = !string.IsNullOrEmpty(codeValue) ? $"{codeValue}_{i}" : $"{conceptName}_{i}";
                        measurements[key] = textValue;
                    }
                }
                // Process containers (nested content)
                else if (valueType == "CONTAINER")
                {
                    if (item.Contains(DicomTag.ContentSequence))
                    {
                        var nestedSequence = item.GetSequence(DicomTag.ContentSequence);
                        var nestedPrefix = !string.IsNullOrEmpty(conceptName) ? $"{prefix}{conceptName}_" : prefix;
                        ProcessContentSequence(nestedSequence, measurements, nestedPrefix);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing content sequence item {Index}", i);
                continue;
            }
        }
    }

    private void ProcessNumericMeasurement(DicomDataset item, Dictionary<string, string> measurements, string conceptName, string codeValue, int index)
    {
        try
        {
            if (item.Contains(DicomTag.MeasuredValueSequence))
            {
                var measuredValueSequence = item.GetSequence(DicomTag.MeasuredValueSequence);
                if (measuredValueSequence?.Items.Count > 0)
                {
                    var measurementItem = measuredValueSequence.Items[0];
                    var numericValue = measurementItem.GetSingleValueOrDefault(DicomTag.NumericValue, "");
                    
                    if (!string.IsNullOrEmpty(numericValue))
                    {
                        // Get unit of measurement
                        string unit = "";
                        if (measurementItem.Contains(DicomTag.MeasurementUnitsCodeSequence))
                        {
                            var unitSequence = measurementItem.GetSequence(DicomTag.MeasurementUnitsCodeSequence);
                            if (unitSequence?.Items.Count > 0)
                            {
                                unit = unitSequence.Items[0].GetSingleValueOrDefault(DicomTag.CodeValue, "");
                            }
                        }

                        // Create key using code value or concept name
                        var key = !string.IsNullOrEmpty(codeValue) ? $"{codeValue}_{index}" : $"{conceptName}_{index}";
                        
                        // Store value with unit if available
                        var value = !string.IsNullOrEmpty(unit) ? $"{numericValue} {unit}" : numericValue;
                        measurements[key] = value;
                        
                        _logger.LogDebug("Extracted measurement: {Key} = {Value}", key, value);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing numeric measurement for concept: {ConceptName}", conceptName);
        }
    }

    private static bool IsStructuredReportSopClass(string sopClassUid)
    {
        if (string.IsNullOrEmpty(sopClassUid))
            return false;

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
public class GetStructuredDataRequest 
{
    public User? User { get; init; }
    public Patient? Patient { get; init; }
    public Exam? Exam { get; init; }
}

/// <inheritdoc cref="GetStructuredDataRequest"/>
public class User 
{
    public string? Login { get; init; }
    public string? Domain { get; init; }
    public string? Name { get; init; }
}

/// <inheritdoc cref="GetStructuredDataRequest"/>
public class Patient 
{
    public string[]? Ids { get; init; }
}

/// <inheritdoc cref="GetStructuredDataRequest"/>
public class Exam 
{
    public string? ExamNo { get; init; }
    public string? AccNo { get; init; }
    public string? StudyUid { get; init; }
    public DateTime? Date { get; init; }
}

/// <summary>Contains the information returned from the GetStructuredData endpoint.</summary>
public class GetStructuredDataResult 
{
    [JsonPropertyName("compatibility")]
    public CompatibilityInfo Compatibility { get; init; } = null!;

    [JsonPropertyName("propValues")]
    public IReadOnlyDictionary<string, string> PropValues { get; init; } = null!;
}

/// <summary>Contains compatibility information for the data provider.</summary>
public class CompatibilityInfo 
{
    public string Uid { get; init; } = null!;
    public int Version { get; init; }
}