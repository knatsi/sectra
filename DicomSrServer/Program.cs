using Dicom;
using Dicom.Network;
using System.Text;

public class DicomSrServer : DicomService, IDicomServiceProvider, IDicomCStoreProvider
{
    private static readonly string[] AllowedSrSopClasses = new[]
    {
        DicomUID.BasicTextSR.UID,
        DicomUID.EnhancedSR.UID,
        DicomUID.ComprehensiveSR.UID
    };

    public DicomSrServer(INetworkStream stream, Encoding fallbackEncoding, Logger? log = null)
        : base(stream, fallbackEncoding, log) { }

    public async Task OnReceiveAssociationRequestAsync(DicomAssociation association)
    {
        foreach (var pc in association.PresentationContexts)
        {
            if (!AllowedSrSopClasses.Contains(pc.AbstractSyntax.UID))
            {
                pc.SetResult(DicomPresentationContextResult.RejectAbstractSyntaxNotSupported);
            }
            else
            {
                pc.AcceptTransferSyntaxes(DicomTransferSyntax.All);
            }
        }

        await SendAssociationAcceptAsync(association);
    }

    public Task OnReceiveAssociationReleaseRequestAsync()
    {
        return SendAssociationReleaseResponseAsync();
    }

    public DicomCStoreResponse OnCStoreRequest(DicomCStoreRequest request)
    {
        var sopClass = request.SOPClassUID.UID;

        if (!AllowedSrSopClasses.Contains(sopClass))
        {
            Console.WriteLine($"Rejected non-SR SOP Class: {sopClass}");
            return new DicomCStoreResponse(request, DicomStatus.SOPClassNotSupported);
        }

        Console.WriteLine($"✅ Received SR file: {request.SOPInstanceUID.UID}");
        // You can process or save the DICOM file here
        return new DicomCStoreResponse(request, DicomStatus.Success);
    }

    public void OnCStoreRequestException(string tempFileName, Exception e)
    {
        Console.WriteLine($"C-STORE Error: {e.Message}");
    }

    public void OnReceiveAbort(DicomAbortSource source, DicomAbortReason reason)
    {
        Console.WriteLine($"Association aborted: {source}, {reason}");
    }

    public void OnConnectionClosed(Exception? exception)
    {
        if (exception != null)
            Console.WriteLine($"Connection closed due to error: {exception.Message}");
        else
            Console.WriteLine("Connection closed.");
    }
}

class Program
{
    static void Main()
    {
        const int port = 11112;

        Console.WriteLine($"🚀 Starting DICOM SR Server on port {port}...");
        var server = DicomServer.Create<DicomSrServer>(port);
        Console.WriteLine("✅ Server is running. Press Enter to quit...");
        Console.ReadLine();
    }
}
