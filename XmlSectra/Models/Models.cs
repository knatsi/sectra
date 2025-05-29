using System.Text.Json.Serialization;

public class GetStructuredDataRequest
{
    public User? User { get; set; }
    public Patient? Patient { get; set; }
    public Exam? Exam { get; set; }
}

public class User
{
    public string? Login { get; set; }
    public string? Domain { get; set; }
    public string? Name { get; set; }
}

public class Patient
{
    public string[]? Ids { get; set; }
}

public class Exam
{
    public string? ExamNo { get; set; }
    public string? AccNo { get; set; }
    public string? StudyUid { get; set; }
    public string? Date { get; set; }
}

public class CompatibilityInfo
{
    public required string Uid { get; init; }
    public required int Version { get; init; }
}

public class GetStructuredDataResult
{
    [JsonPropertyName("compatibility")]
    public required CompatibilityInfo Compatibility { get; init; }

    [JsonPropertyName("propValues")]
    public required IReadOnlyDictionary<string, object> PropValues { get; init; }
}


