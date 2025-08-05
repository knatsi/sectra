
using FellowOakDicom.Network;
using Microsoft.Extensions.Logging;
using System.Text;

namespace FellowOakDicom.Samples.CStoreSCP;

internal class StoreScp : DicomService, IDicomServiceProvider, IDicomCStoreProvider, IDicomCEchoProvider
{
    private readonly string _aeTitle;
    private readonly string _storagePath;

    private static readonly DicomTransferSyntax[] _acceptedTransferSyntaxes = new DicomTransferSyntax[]
    {
               DicomTransferSyntax.ExplicitVRLittleEndian,
               DicomTransferSyntax.ExplicitVRBigEndian,
               DicomTransferSyntax.ImplicitVRLittleEndian
    };


    private static readonly HashSet<DicomUID> AcceptedSopClasses = new HashSet<DicomUID>
    {
        DicomUID.EnhancedSRStorage,
        DicomUID.ComprehensiveSRStorage,
        DicomUID.BasicTextSRStorage,
        DicomUID.KeyObjectSelectionDocumentStorage,
        DicomUID.MammographyCADSRStorage
        // Add any other SR SOP Classes as needed
    };

    public StoreScp(INetworkStream stream, Encoding fallbackEncoding, ILogger log, DicomServiceDependencies dependencies, object userState)
        : base(stream, fallbackEncoding, log, dependencies)
    {
        if (userState is StoreScpSettings settings)
        {
            _aeTitle = settings.AeTitle;
            _storagePath = settings.StoragePath;
        }
        else
        {
            _aeTitle = "DICOMSRSERVER";  // default fallback
            _storagePath = @".\DICOM";
        }
    }


    public async Task OnReceiveAssociationRequestAsync(DicomAssociation association)
    {
        if (association.CalledAE != _aeTitle)  // now using configured AE Title
        {
            await SendAssociationRejectAsync(
                DicomRejectResult.Permanent,
                DicomRejectSource.ServiceUser,
                DicomRejectReason.CalledAENotRecognized);
            return;
        }

        bool acceptedAny = false;

        foreach (var pc in association.PresentationContexts)
        {
            if (AcceptedSopClasses.Contains(pc.AbstractSyntax))
            {
                pc.AcceptTransferSyntaxes(_acceptedTransferSyntaxes);
                acceptedAny = true;
            }
            else
            {
                pc.SetResult(DicomPresentationContextResult.RejectAbstractSyntaxNotSupported);
            }
        }

        if (!acceptedAny)
        {
            await SendAssociationRejectAsync(
                DicomRejectResult.Permanent,
                DicomRejectSource.ServiceUser,
                DicomRejectReason.NoReasonGiven);
            return;
        }

        await SendAssociationAcceptAsync(association);
    }

    public Task OnReceiveAssociationReleaseRequestAsync()
    {
        return SendAssociationReleaseResponseAsync();
    }


    public void OnReceiveAbort(DicomAbortSource source, DicomAbortReason reason)
    {
        /* nothing to do here */
    }


    public void OnConnectionClosed(Exception exception)
    {
        /* nothing to do here */
    }


    public async Task<DicomCStoreResponse> OnCStoreRequestAsync(DicomCStoreRequest request)
    {
        var studyUid = request.Dataset.GetSingleValue<string>(DicomTag.StudyInstanceUID).Trim();
        var instUid = request.SOPInstanceUID.UID;

        var path = Path.GetFullPath(_storagePath);
        path = Path.Combine(path, studyUid);

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        path = Path.Combine(path, instUid) + ".dcm";

        await request.File.SaveAsync(path);

        return new DicomCStoreResponse(request, DicomStatus.Success);
    }


    public Task OnCStoreRequestExceptionAsync(string tempFileName, Exception e)
    {
        // let library handle logging and error response
        return Task.CompletedTask;
    }


    public Task<DicomCEchoResponse> OnCEchoRequestAsync(DicomCEchoRequest request)
    {
        return Task.FromResult(new DicomCEchoResponse(request, DicomStatus.Success));
    }

}