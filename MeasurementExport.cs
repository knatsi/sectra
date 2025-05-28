using System;
using System.Collections.Generic;
using System.Xml.Serialization;

[XmlRoot("MeasurementExport")]
public class MeasurementExport
{
    [XmlAttribute]
    public string docVersion { get; set; }

    public Patient Patient { get; set; }
}

public class Patient
{
    public string Birthdate { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string PatientId { get; set; }
    public string Sex { get; set; }
    public Study Study { get; set; }
}

public class Study
{
    public string AccessionNumber { get; set; }
    public string Height { get; set; }
    public string StudyDateTime { get; set; }
    public string StudyDescription { get; set; }
    public string StudyId { get; set; }
    public string Weight { get; set; }
    public string BSA { get; set; }
    public string RequestedProcedureDescription { get; set; }

    public Series Series { get; set; }
}

public class Series
{
    public string Category { get; set; }
    public string DepartmentName { get; set; }
    public string InstitutionName { get; set; }
    public string Modality { get; set; }
    public string ProtocolName { get; set; }
    public string SeriesDateTime { get; set; }
    public string SeriesInstanceUID { get; set; }
    public string SPSId { get; set; }

    [XmlElement("Parameter")]
    public List<Parameter> Parameters { get; set; }
}

public class Parameter
{
    public string AverageType { get; set; }
    public string Category { get; set; }
    public string DisplayUnit { get; set; }
    public bool Edited { get; set; }
    public bool ExcludedFromAvg { get; set; }
    public bool ExcludedFromCalc { get; set; }
    public string MeasureId { get; set; }
    public string ParameterId { get; set; }
    public string ParameterName { get; set; }
    public int ResultNo { get; set; }
    public double ResultValue { get; set; }
    public string ScanMode { get; set; }
    public string StudyId { get; set; }
    public string ParameterType { get; set; }
    public string DisplayValue { get; set; }
}
