﻿
using FellowOakDicom;
using FellowOakDicom.Network;
using FellowOakDicom.Samples.CStoreSCP;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.CommandLine;

// Build configuration
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)     // Important to set base path for appsettings.json
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddCommandLine(args)                      // Optional: override config by command line args
    .Build();

// Bind configuration section to strongly typed object
var dicomConfig = config.GetSection("DicomServer").Get<DicomServerConfig>();

// Fallback defaults if config missing
int port = dicomConfig?.Port ?? 11112;
string aeTitle = dicomConfig?.AETitle ?? "DICOMSRSERVER";
string storagePath = dicomConfig?.StoragePath ?? @".\DICOM";

Console.WriteLine($"Starting C-Store SCP server on port {port}, AE Title '{aeTitle}', Storage Path '{storagePath}'");

// Setup DICOM configuration or services (if needed)
new DicomSetupBuilder()
    .RegisterServices(s => s.AddFellowOakDicom())
    .Build();

// You need a way to pass the AE Title and storage path down to your StoreScp instance.
// One common approach is to modify the StoreScp constructor to accept those parameters
// and then create a custom factory using DicomServer.Create overload.
var userState = new StoreScpSettings
{
    AeTitle = aeTitle,
    StoragePath = storagePath
};

var server = DicomServerFactory.Create<StoreScp>(port, userState: userState);

Console.WriteLine("Press <return> to end...");
Console.ReadLine();

server.Dispose();
