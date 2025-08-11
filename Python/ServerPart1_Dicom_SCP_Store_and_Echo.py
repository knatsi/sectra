from pynetdicom import AE, evt
from pynetdicom.sop_class import (
    Verification,
    BasicTextSRStorage,
    EnhancedSRStorage,
    ComprehensiveSRStorage,
    Comprehensive3DSRStorage,
    KeyObjectSelectionDocumentStorage
)
import os

# -----------------------------------------------------
# Config
# -----------------------------------------------------
AE_TITLE = "DICOMSRSERVER"
PORT = 11112
STORAGE_FOLDER = "dicom_sr_in"

# Make sure the storage folder exists
os.makedirs(STORAGE_FOLDER, exist_ok=True)

# -----------------------------------------------------
# Event Handlers
# -----------------------------------------------------
def handle_echo(event):
    print(f"[C-ECHO] from {event.assoc.requestor.ae_title.decode()}")
    return 0x0000

def handle_store(event):
    ds = event.dataset
    ds.file_meta = event.file_meta

    # Save SR file
    filename = os.path.join(STORAGE_FOLDER, f"{ds.SOPInstanceUID}.dcm")
    ds.save_as(filename, write_like_original=False)
    print(f"[C-STORE] Stored SR file: {filename}")
    return 0x0000

# -----------------------------------------------------
# Setup AE
# -----------------------------------------------------
ae = AE(ae_title=AE_TITLE)

# Add Verification SCP
ae.add_supported_context(Verification)

# Add SR SOP Classes only
sr_sop_classes = [
    BasicTextSRStorage,
    EnhancedSRStorage,
    ComprehensiveSRStorage,
    Comprehensive3DSRStorage,
    KeyObjectSelectionDocumentStorage
]
for sop in sr_sop_classes:
    ae.add_supported_context(sop)

# Event handlers
handlers = [
    (evt.EVT_C_ECHO, handle_echo),
    (evt.EVT_C_STORE, handle_store),
]

print(f"📡 Starting DICOM SR server '{AE_TITLE}' on port {PORT}...")
print(f"📂 Incoming SR files will be stored in: {os.path.abspath(STORAGE_FOLDER)}")

# Start server
ae.start_server(("0.0.0.0", PORT), block=True, evt_handlers=handlers)
