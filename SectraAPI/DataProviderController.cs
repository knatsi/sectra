using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Xml.Serialization;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace HeartProviderAdults.WebService;

// TODO: This should require authorization using a basic auth header. This header will automatically be included in requests to all Sectra Forms data providers.
[Route("")]
[ApiController]
public class DataProviderController : ControllerBase {

    private readonly IConfiguration _configuration;

    public DataProviderController(IConfiguration configuration) {_configuration = configuration;}

    private static readonly CompatibilityInfo CompatibilityInfoV1 = new() { Uid = "HeartProviderAdults", Version = 1 };

    /// <summary>
    /// Gets the compatibility information about this service.
    /// </summary>
    [HttpGet]
    [Route("v1/GetStructuredDataCompatibility")]
    public CompatibilityInfo GetStructuredDataCompatibility() {
        return CompatibilityInfoV1;
    }

    /// <summary>
    /// Gets the structured data to use in templates.
    /// </summary>
    [HttpPost]
    [Route("v1/GetStructuredData")]
    public GetStructuredDataResult GetStructuredData([FromBody] GetStructuredDataRequest request) {
        var studyUid = request.Exam?.StudyUid;
        var data = ExtractData(studyUid);
        return new GetStructuredDataResult { Compatibility = CompatibilityInfoV1, PropValues = data };
    }

    private Dictionary<string, string> ExtractData(string studyUid)
    {
    var result = new Dictionary<string, string>();

    var folder = _configuration["XmlSettings:FolderPath"] ?? "Data";
    var file = _configuration["XmlSettings:FileName"] ?? "HeartProviderAdults.xml";
    var path = Path.Combine(folder, file);

    if (!System.IO.File.Exists(path))
        throw new FileNotFoundException($"File not found: {path}");

    var serializer = new XmlSerializer(typeof(MeasurementExport));
    using var reader = new StreamReader(path);
    var export = (MeasurementExport?)serializer.Deserialize(reader);

    if (export?.Patient?.Study == null)
        return result;

    var study = export.Patient.Study;

    // Only proceed if StudyId matches request. Correct? What should be checked against?
    if (study.StudyId != studyUid)
        return result;

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

