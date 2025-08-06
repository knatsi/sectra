using Microsoft.AspNetCore.Mvc;
using FellowOakDicom;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using System.Xml.Linq;

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

                // First try to extract from embedded XML (preferred method)
                var xmlMeasurements = ExtractMeasurementsFromEmbeddedXml(dicomFile.Dataset, studyUid);
                if (xmlMeasurements.Count > 0)
                {
                    foreach (var measurement in xmlMeasurements)
                    {
                        result[measurement.Key] = measurement.Value;
                    }
                    _logger.LogDebug("Extracted {Count} measurements from embedded XML in {FilePath}", xmlMeasurements.Count, fileInfo.FilePath);
                }
                else
                {
                    // Fallback to DICOM SR parsing if no XML data found
                    var srMeasurements = ExtractMeasurementsFromSR(dicomFile.Dataset);
                    foreach (var measurement in srMeasurements)
                    {
                        result[measurement.Key] = measurement.Value;
                    }
                    _logger.LogDebug("Extracted {Count} measurements from DICOM SR in {FilePath}", srMeasurements.Count, fileInfo.FilePath);
                }
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

    private Dictionary<string, string> ExtractMeasurementsFromEmbeddedXml(DicomDataset dataset, string studyUid)
    {
        var result = new Dictionary<string, string>();

        try
        {
            // Look for embedded XML data in various possible tags
            string? xmlContent = null;

            // Check common tags where XML might be embedded
            var possibleXmlTags = new[]
            {
                DicomTag.TextValue,
                DicomTag.ConceptCodeSequence,
                // The XML might be in a private tag - let's check the dataset
            };

            // First, let's look through all elements to find XML content
            foreach (var element in dataset)
            {
                if (element.ValueRepresentation == DicomVR.LT || 
                    element.ValueRepresentation == DicomVR.ST || 
                    element.ValueRepresentation == DicomVR.UT)
                {
                    var value = dataset.GetSingleValueOrDefault(element.Tag, "");
                    if (!string.IsNullOrEmpty(value) && value.Contains("<MeasurementExport"))
                    {
                        xmlContent = value;
                        _logger.LogDebug("Found XML content in tag: {Tag}", element.Tag);
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(xmlContent))
            {
                _logger.LogDebug("No embedded XML found in DICOM dataset");
                return result;
            }

            // Parse the XML content
            using var stringReader = new StringReader(xmlContent);
            var xmlDoc = System.Xml.Linq.XDocument.Load(stringReader);

            // Navigate to the parameters
            var parameters = xmlDoc.Descendants("Parameter").Where(p => 
            {
                var resultNo = p.Element("ResultNo")?.Value;
                return resultNo == "-1"; // Only get average/summary values
            });

            foreach (var param in parameters)
            {
                var parameterId = param.Element("ParameterId")?.Value;
                var resultNo = param.Element("ResultNo")?.Value;
                var displayValue = param.Element("DisplayValue")?.Value;
                var resultValue = param.Element("ResultValue")?.Value;
                var displayUnit = param.Element("DisplayUnit")?.Value;

                if (!string.IsNullOrEmpty(parameterId) && !string.IsNullOrEmpty(displayValue))
                {
                    var key = $"{parameterId}_{resultNo}";
                    var value = !string.IsNullOrEmpty(displayUnit) ? $"{displayValue} {displayUnit}" : displayValue;
                    
                    result[key] = value;
                    
                    _logger.LogDebug("Extracted XML measurement: {Key} = {Value}", key, value);
                }
                else if (!string.IsNullOrEmpty(parameterId) && !string.IsNullOrEmpty(resultValue))
                {
                    // Fallback to ResultValue if DisplayValue is not available
                    var key = $"{parameterId}_{resultNo}";
                    var numericValue = double.TryParse(resultValue, out var num) ? num.ToString("F2") : resultValue;
                    var value = !string.IsNullOrEmpty(displayUnit) ? $"{numericValue} {displayUnit}" : numericValue;
                    
                    result[key] = value;
                    
                    _logger.LogDebug("Extracted XML measurement (from ResultValue): {Key} = {Value}", key, value);
                }
            }

            _logger.LogInformation("Extracted {Count} measurements from embedded XML", result.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting measurements from embedded XML");
        }

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
                                var unitItem = unitSequence.Items[0];
                                unit = unitItem.GetSingleValueOrDefault(DicomTag.CodeMeaning, "");
                                if (string.IsNullOrEmpty(unit))
                                {
                                    unit = unitItem.GetSingleValueOrDefault(DicomTag.CodeValue, "");
                                }
                            }
                        }

                        // Create a more meaningful key - prioritize concept name over code value
                        string key;
                        if (!string.IsNullOrEmpty(conceptName) && conceptName != "Findings" && conceptName != "Measurement Group")
                        {
                            // Use concept name (more descriptive)
                            key = $"{conceptName}_{index}";
                        }
                        else if (!string.IsNullOrEmpty(codeValue))
                        {
                            // Fallback to code value
                            key = $"{codeValue}_{index}";
                        }
                        else
                        {
                            // Last resort - generic key
                            key = $"Measurement_{index}";
                        }
                        
                        // Store value with unit if available
                        var value = !string.IsNullOrEmpty(unit) ? $"{numericValue} {unit}" : numericValue;
                        
                        // Avoid duplicate keys by checking if it already exists
                        var finalKey = key;
                        int duplicateCounter = 1;
                        while (measurements.ContainsKey(finalKey))
                        {
                            finalKey = $"{key}_{duplicateCounter}";
                            duplicateCounter++;
                        }
                        
                        measurements[finalKey] = value;
                        
                        _logger.LogDebug("Extracted measurement: {Key} = {Value} (ConceptName: '{ConceptName}', CodeValue: '{CodeValue}')", 
                            finalKey, value, conceptName, codeValue);
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