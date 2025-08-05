using FellowOakDicom;

Console.WriteLine("DICOM SR Test File Generator");
Console.WriteLine("============================");

// Setup DICOM
new DicomSetupBuilder()
    .RegisterServices(s => s.AddFellowOakDicom())
    .Build();

// Get output path from command line or use default
string outputPath = args.Length > 0 ? args[0] : "./TestDicomFiles";

try
{
    DicomSrGenerator.GenerateTestFiles(outputPath);
    
    Console.WriteLine("\nGenerated test files:");
    var files = Directory.GetFiles(outputPath, "*.dcm");
    foreach (var file in files)
    {
        var fileInfo = new FileInfo(file);
        Console.WriteLine($"  - {Path.GetFileName(file)} ({fileInfo.Length} bytes)");
    }
    
    Console.WriteLine($"\nTotal files generated: {files.Length}");
    Console.WriteLine($"Output directory: {Path.GetFullPath(outputPath)}");
}
catch (Exception ex)
{
    Console.WriteLine($"Error generating test files: {ex.Message}");
    Environment.Exit(1);
}

Console.WriteLine("\nTest files generated successfully!");
Console.WriteLine("You can now use these files to test your DICOM SR Server.");