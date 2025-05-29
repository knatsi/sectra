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
    public string PatientId { get; set; }
    public string Sex { get; set; }
    public Study Study { get; set; }
}

public class Study
{
    public string AccessionNumber { get; set; }
    public string StudyId { get; set; }

    public Series Series { get; set; }
}

public class Series
{
    public string SeriesInstanceUID { get; set; }
    public string SPSId { get; set; }

    [XmlElement("Parameter")]
    public List<Parameter> Parameters { get; set; }
}

public class Parameter
{
    public string DisplayUnit { get; set; }
    public string MeasureId { get; set; }
    public string ParameterId { get; set; }
    public string ResultNo { get; set; }
    public string DisplayValue { get; set; }
}
public class CompatibilityInfo
{
    public string Uid { get; set; } = string.Empty;
    public int Version { get; set; }
}