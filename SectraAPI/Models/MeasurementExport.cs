using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Text.Json.Serialization;


[XmlRoot("MeasurementExport")]
public class MeasurementExport
{
    public Patient Patient { get; set; }

    [XmlAttribute("docVersion")]
    public string DocVersion { get; set; }
}

public class Patient
{
    public string PatientId { get; set; }
    public string Sex { get; set; }

    public Study Study { get; set; }
}

public class Study
{
    public string StudyId { get; set; }
    public string AccessionNumber { get; set; }

    public Series Series { get; set; }
}

public class Series
{
    [XmlElement("Parameter")]
    public List<Parameter> Parameters { get; set; }
}

public class Parameter
{
    public string ParameterId { get; set; }
    public string ResultNo { get; set; }
    public string DisplayValue { get; set; }
}