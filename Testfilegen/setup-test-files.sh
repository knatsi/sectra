#!/bin/bash

echo "Setting up DICOM SR test files..."

# Create a temporary directory for the test file generator
mkdir -p TestFileGenerator
cd TestFileGenerator

# Create the generator files
cat > Program.cs << 'EOF'
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
EOF

# Create the project file
cat > TestFileGenerator.csproj << 'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="fo-dicom" Version="5.2.2" />
    <PackageReference Include="fo-dicom.Codecs" Version="5.15.4" />
  </ItemGroup>
</Project>
EOF

# Copy the DicomSrGenerator.cs from the main project (you'll need to create this file)
echo "Please create DicomSrGenerator.cs file with the generator code provided."

# Build and run
echo "Building test file generator..."
dotnet build

if [ $? -eq 0 ]; then
    echo "Running test file generator..."
    dotnet run "../DICOM"
    
    # Copy files to the main DICOM directory structure
    if [ -d "../DICOM" ]; then
        echo "Test files have been generated in ../DICOM"
        ls -la ../DICOM/*.dcm 2>/dev/null || echo "No .dcm files found. Please check the generator output."
    fi
else
    echo "Build failed. Please check for errors."
fi

cd ..
echo "Setup complete!"