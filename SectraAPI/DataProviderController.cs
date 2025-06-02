using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace ExampleFormsDataProvider.WebService;

// TODO: This should require authorization using a basic auth header. This header will automatically be included in requests to all Sectra Forms data providers.
[Route("")]
[ApiController]
public class DataProviderController : ControllerBase {
    private static readonly CompatibilityInfo CompatibilityInfoV1 = new() { Uid = "ExampleFormsDataProvider", Version = 1 };

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
        var data = GetTemplateData(studyUid);
        return new GetStructuredDataResult { Compatibility = CompatibilityInfoV1, PropValues = data };
    }

    private Dictionary<string, string> GetTemplateData(string? studyUid) {
        var result = new Dictionary<string, string>();

        if (!string.IsNullOrWhiteSpace(studyUid)) {
            // TODO: This should collect the actual data to be returned to the template. Specified as strings here, but could be any JSON-compatible value, like arrays or objects if needed.
            result.Add("param1", "1.2");
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
