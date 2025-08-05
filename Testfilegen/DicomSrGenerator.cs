using FellowOakDicom;
using System.Text;

public class DicomSrGenerator
{
    public static void GenerateTestFiles(string outputPath = "./TestDicomFiles")
    {
        // Create output directory
        Directory.CreateDirectory(outputPath);
        
        // Generate different types of SR documents
        GenerateBasicTextSR(outputPath);
        GenerateEnhancedSR(outputPath);
        GenerateComprehensiveSR(outputPath);
        GenerateKeyObjectSelection(outputPath);
        GenerateMammographyCADSR(outputPath);
        
        Console.WriteLine($"Generated test DICOM SR files in: {Path.GetFullPath(outputPath)}");
    }
    
    private static void GenerateBasicTextSR(string outputPath)
    {
        var dataset = CreateBaseSRDataset("Basic Text SR Test", DicomUID.BasicTextSRStorage);
        
        // Add specific Basic Text SR content
        dataset.AddOrUpdate(DicomTag.CompletionFlag, "COMPLETE");
        dataset.AddOrUpdate(DicomTag.VerificationFlag, "VERIFIED");
        
        // Create content sequence for Basic Text SR
        var contentSequence = new DicomSequence(DicomTag.ContentSequence);
        
        // Add a CONTAINER item
        var containerItem = new DicomDataset();
        containerItem.AddOrUpdate(DicomTag.RelationshipType, "CONTAINS");
        containerItem.AddOrUpdate(DicomTag.ValueType, "CONTAINER");
        
        var conceptNameSequence = new DicomSequence(DicomTag.ConceptNameCodeSequence);
        var conceptName = new DicomDataset();
        conceptName.AddOrUpdate(DicomTag.CodeValue, "11528-7");
        conceptName.AddOrUpdate(DicomTag.CodingSchemeDesignator, "LN");
        conceptName.AddOrUpdate(DicomTag.CodeMeaning, "Radiology Report");
        conceptNameSequence.Items.Add(conceptName);
        containerItem.AddOrUpdate(conceptNameSequence);
        
        contentSequence.Items.Add(containerItem);
        
        // Add a TEXT item
        var textItem = new DicomDataset();
        textItem.AddOrUpdate(DicomTag.RelationshipType, "CONTAINS");
        textItem.AddOrUpdate(DicomTag.ValueType, "TEXT");
        textItem.AddOrUpdate(DicomTag.TextValue, "This is a sample radiology report generated for testing purposes. Patient shows normal chest X-ray findings.");
        
        var textConceptName = new DicomSequence(DicomTag.ConceptNameCodeSequence);
        var textConcept = new DicomDataset();
        textConcept.AddOrUpdate(DicomTag.CodeValue, "121070");
        textConcept.AddOrUpdate(DicomTag.CodingSchemeDesignator, "DCM");
        textConcept.AddOrUpdate(DicomTag.CodeMeaning, "Findings");
        textConceptName.Items.Add(textConcept);
        textItem.AddOrUpdate(textConceptName);
        
        contentSequence.Items.Add(textItem);
        dataset.AddOrUpdate(contentSequence);
        
        SaveDicomFile(dataset, Path.Combine(outputPath, "BasicTextSR_Test.dcm"));
    }
    
    private static void GenerateEnhancedSR(string outputPath)
    {
        var dataset = CreateBaseSRDataset("Enhanced SR Test", DicomUID.EnhancedSRStorage);
        
        dataset.AddOrUpdate(DicomTag.CompletionFlag, "COMPLETE");
        dataset.AddOrUpdate(DicomTag.VerificationFlag, "VERIFIED");
        
        // Add Enhanced SR specific content
        var contentSequence = new DicomSequence(DicomTag.ContentSequence);
        
        // Add measurement data
        var measurementItem = new DicomDataset();
        measurementItem.AddOrUpdate(DicomTag.RelationshipType, "CONTAINS");
        measurementItem.AddOrUpdate(DicomTag.ValueType, "NUM");
        measurementItem.AddOrUpdate(DicomTag.NumericValue, "45.7");
        
        var measuredValueSequence = new DicomSequence(DicomTag.MeasuredValueSequence);
        var measuredValue = new DicomDataset();
        measuredValue.AddOrUpdate(DicomTag.NumericValue, "45.7");
        
        var measurementUnitsSequence = new DicomSequence(DicomTag.MeasurementUnitsCodeSequence);
        var units = new DicomDataset();
        units.AddOrUpdate(DicomTag.CodeValue, "mm");
        units.AddOrUpdate(DicomTag.CodingSchemeDesignator, "UCUM");
        units.AddOrUpdate(DicomTag.CodeMeaning, "millimeter");
        measurementUnitsSequence.Items.Add(units);
        measuredValue.AddOrUpdate(measurementUnitsSequence);
        
        measuredValueSequence.Items.Add(measuredValue);
        measurementItem.AddOrUpdate(measuredValueSequence);
        
        contentSequence.Items.Add(measurementItem);
        dataset.AddOrUpdate(contentSequence);
        
        SaveDicomFile(dataset, Path.Combine(outputPath, "EnhancedSR_Test.dcm"));
    }
    
    private static void GenerateComprehensiveSR(string outputPath)
    {
        var dataset = CreateBaseSRDataset("Comprehensive SR Test", DicomUID.ComprehensiveSRStorage);
        
        dataset.AddOrUpdate(DicomTag.CompletionFlag, "COMPLETE");
        dataset.AddOrUpdate(DicomTag.VerificationFlag, "VERIFIED");
        
        // Add comprehensive content with multiple sections
        var contentSequence = new DicomSequence(DicomTag.ContentSequence);
        
        // Clinical History section
        var historySection = CreateTextContent("CONTAINS", "Clinical History", "Patient presents with chest pain and shortness of breath. Previous cardiac studies normal.");
        contentSequence.Items.Add(historySection);
        
        // Findings section
        var findingsSection = CreateTextContent("CONTAINS", "Findings", "Heart size is normal. Lungs are clear. No acute abnormalities detected.");
        contentSequence.Items.Add(findingsSection);
        
        // Impression section
        var impressionSection = CreateTextContent("CONTAINS", "Impression", "Normal chest radiograph. No acute cardiopulmonary abnormalities.");
        contentSequence.Items.Add(impressionSection);
        
        dataset.AddOrUpdate(contentSequence);
        
        SaveDicomFile(dataset, Path.Combine(outputPath, "ComprehensiveSR_Test.dcm"));
    }
    
    private static void GenerateKeyObjectSelection(string outputPath)
    {
        var dataset = CreateBaseSRDataset("Key Object Selection Test", DicomUID.KeyObjectSelectionDocumentStorage);
        
        dataset.AddOrUpdate(DicomTag.CompletionFlag, "COMPLETE");
        dataset.AddOrUpdate(DicomTag.VerificationFlag, "UNVERIFIED");
        
        // Add key object selection content
        var contentSequence = new DicomSequence(DicomTag.ContentSequence);
        
        // Add reference to key images
        var keyImageItem = new DicomDataset();
        keyImageItem.AddOrUpdate(DicomTag.RelationshipType, "CONTAINS");
        keyImageItem.AddOrUpdate(DicomTag.ValueType, "IMAGE");
        
        // Referenced SOP sequence
        var referencedSOPSequence = new DicomSequence(DicomTag.ReferencedSOPSequence);
        var referencedSOP = new DicomDataset();
        referencedSOP.AddOrUpdate(DicomTag.ReferencedSOPClassUID, DicomUID.CTImageStorage.UID);
        referencedSOP.AddOrUpdate(DicomTag.ReferencedSOPInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID().UID);
        referencedSOPSequence.Items.Add(referencedSOP);
        keyImageItem.AddOrUpdate(referencedSOPSequence);
        
        contentSequence.Items.Add(keyImageItem);
        dataset.AddOrUpdate(contentSequence);
        
        SaveDicomFile(dataset, Path.Combine(outputPath, "KeyObjectSelection_Test.dcm"));
    }
    
    private static void GenerateMammographyCADSR(string outputPath)
    {
        var dataset = CreateBaseSRDataset("Mammography CAD SR Test", DicomUID.MammographyCADSRStorage);
        
        dataset.AddOrUpdate(DicomTag.CompletionFlag, "COMPLETE");
        dataset.AddOrUpdate(DicomTag.VerificationFlag, "VERIFIED");
        
        // Add mammography CAD specific content
        var contentSequence = new DicomSequence(DicomTag.ContentSequence);
        
        // CAD finding
        var cadFinding = new DicomDataset();
        cadFinding.AddOrUpdate(DicomTag.RelationshipType, "CONTAINS");
        cadFinding.AddOrUpdate(DicomTag.ValueType, "TEXT");
        cadFinding.AddOrUpdate(DicomTag.TextValue, "CAD detected suspicious area in upper outer quadrant, left breast. Recommend further evaluation.");
        
        var cadConceptName = new DicomSequence(DicomTag.ConceptNameCodeSequence);
        var cadConcept = new DicomDataset();
        cadConcept.AddOrUpdate(DicomTag.CodeValue, "F-01710");
        cadConcept.AddOrUpdate(DicomTag.CodingSchemeDesignator, "SRT");
        cadConcept.AddOrUpdate(DicomTag.CodeMeaning, "CAD Finding");
        cadConceptName.Items.Add(cadConcept);
        cadFinding.AddOrUpdate(cadConceptName);
        
        contentSequence.Items.Add(cadFinding);
        dataset.AddOrUpdate(contentSequence);
        
        SaveDicomFile(dataset, Path.Combine(outputPath, "MammographyCADSR_Test.dcm"));
    }
    
    private static DicomDataset CreateBaseSRDataset(string description, DicomUID sopClassUID)
    {
        var dataset = new DicomDataset();
        
        // SOP Common Module
        dataset.AddOrUpdate(DicomTag.SOPClassUID, sopClassUID.UID);
        dataset.AddOrUpdate(DicomTag.SOPInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID().UID);
        
        // Patient Module
        dataset.AddOrUpdate(DicomTag.PatientName, "TEST^PATIENT^SR");
        dataset.AddOrUpdate(DicomTag.PatientID, "TEST001");
        dataset.AddOrUpdate(DicomTag.PatientBirthDate, "19800101");
        dataset.AddOrUpdate(DicomTag.PatientSex, "M");
        
        // General Study Module
        var studyUID = DicomUIDGenerator.GenerateDerivedFromUUID().UID;
        dataset.AddOrUpdate(DicomTag.StudyInstanceUID, studyUID);
        dataset.AddOrUpdate(DicomTag.StudyDate, DateTime.Now.ToString("yyyyMMdd"));
        dataset.AddOrUpdate(DicomTag.StudyTime, DateTime.Now.ToString("HHmmss"));
        dataset.AddOrUpdate(DicomTag.ReferringPhysicianName, "TEST^PHYSICIAN");
        dataset.AddOrUpdate(DicomTag.StudyID, "ST001");
        dataset.AddOrUpdate(DicomTag.AccessionNumber, "ACC001");
        dataset.AddOrUpdate(DicomTag.StudyDescription, description);
        
        // General Series Module
        dataset.AddOrUpdate(DicomTag.Modality, "SR");
        dataset.AddOrUpdate(DicomTag.SeriesInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID().UID);
        dataset.AddOrUpdate(DicomTag.SeriesNumber, "1");
        dataset.AddOrUpdate(DicomTag.SeriesDescription, description);
        
        // General Equipment Module
        dataset.AddOrUpdate(DicomTag.Manufacturer, "Test Manufacturer");
        dataset.AddOrUpdate(DicomTag.ManufacturerModelName, "SR Generator");
        dataset.AddOrUpdate(DicomTag.SoftwareVersions, "1.0");
        
        // SR Document Module
        dataset.AddOrUpdate(DicomTag.InstanceNumber, "1");
        dataset.AddOrUpdate(DicomTag.ContentDate, DateTime.Now.ToString("yyyyMMdd"));
        dataset.AddOrUpdate(DicomTag.ContentTime, DateTime.Now.ToString("HHmmss"));
        dataset.AddOrUpdate(DicomTag.DocumentTitle, description);
        
        // SR Document Series Module - specific attributes
        dataset.AddOrUpdate(DicomTag.PresentationIntentType, "FOR_PRESENTATION");
        
        return dataset;
    }
    
    private static DicomDataset CreateTextContent(string relationship, string conceptMeaning, string textValue)
    {
        var textItem = new DicomDataset();
        textItem.AddOrUpdate(DicomTag.RelationshipType, relationship);
        textItem.AddOrUpdate(DicomTag.ValueType, "TEXT");
        textItem.AddOrUpdate(DicomTag.TextValue, textValue);
        
        var conceptNameSequence = new DicomSequence(DicomTag.ConceptNameCodeSequence);
        var conceptName = new DicomDataset();
        conceptName.AddOrUpdate(DicomTag.CodeValue, "121070");
        conceptName.AddOrUpdate(DicomTag.CodingSchemeDesignator, "DCM");
        conceptName.AddOrUpdate(DicomTag.CodeMeaning, conceptMeaning);
        conceptNameSequence.Items.Add(conceptName);
        textItem.AddOrUpdate(conceptNameSequence);
        
        return textItem;
    }
    
    private static void SaveDicomFile(DicomDataset dataset, string filePath)
    {
        var dicomFile = new DicomFile(dataset);
        dicomFile.Save(filePath);
        Console.WriteLine($"Created: {filePath}");
    }
}