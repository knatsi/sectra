using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

string connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Get list of exams for a patient
app.MapGet("/api/exams/{patientId}", async (string patientId) =>
{
    var exams = new List<object>();

    using var conn = new SqlConnection(connectionString);
    await conn.OpenAsync();

    var cmd = new SqlCommand(@"
        SELECT ExamID, ExamName, ExamDate, PdfPath
        FROM Exams
        WHERE PersonalID = @pid
        ORDER BY ExamDate DESC", conn);

    cmd.Parameters.AddWithValue("@pid", patientId);

    using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        exams.Add(new
        {
            ExamID = reader.GetInt32(0),
            ExamName = reader.GetString(1),
            ExamDate = reader.GetDateTime(2),
            PdfPath = reader.GetString(3)
        });
    }

    return Results.Json(exams);
});

// Get PDF file by exam id
app.MapGet("/api/exams/file/{examId}", async (int examId) =>
{
    using var conn = new SqlConnection(connectionString);
    await conn.OpenAsync();

    var cmd = new SqlCommand("SELECT PdfPath FROM Exams WHERE ExamID = @id", conn);
    cmd.Parameters.AddWithValue("@id", examId);

    var path = (string?)await cmd.ExecuteScalarAsync();
    if (path == null || !System.IO.File.Exists(path))
        return Results.NotFound();

    return Results.File(path, "application/pdf");
});

app.Run();