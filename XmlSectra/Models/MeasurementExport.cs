using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Text.Json.Serialization;


[XmlRoot("MeasurementExport")]
public class MeasurementExport
{
    [XmlAttribute]
    public string docVersion { get; set; }
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
    public string ParameterId { get; set; }
    public string ResultNo { get; set; }
    public string DisplayValue { get; set; }
}
