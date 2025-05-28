using System;
using System.IO;
using System.Text.Json;
using System.Xml.Serialization;

class Program
{
    static void Main()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "settings.json");
        var configJson = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<Config>(configJson);

        if (config == null || string.IsNullOrEmpty(config.XmlFolderPath))
        {
            Console.WriteLine("Invalid configuration.");
            return;
        }

        string xmlFilePath = Path.Combine(config.XmlFolderPath, "input.xml");
        if (!File.Exists(xmlFilePath))
        {
            Console.WriteLine($"XML file not found: {xmlFilePath}");
            return;
        }

        var serializer = new XmlSerializer(typeof(MeasurementExport));
        using var reader = new StreamReader(xmlFilePath);
        var data = (MeasurementExport)serializer.Deserialize(reader);

        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(json);
    }
}

class Config
{
    public string XmlFolderPath { get; set; }
}
