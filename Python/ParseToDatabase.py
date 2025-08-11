import sqlite3
import pydicom
import os
import shutil
import time
import logging
from datetime import datetime, timedelta
from watchdog.observers import Observer
from watchdog.events import FileSystemEventHandler

# Configuration
SR_FOLDER = "dicom_sr_in"
ARCHIVE_FOLDER = "archive"
DB_FILE = "dicomsrmeasurements.db"
ARCHIVE_RETENTION_DAYS = 10

# Configure logging at the beginning of your script
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s: %(message)s',
    handlers=[
        logging.FileHandler('dicom_parser.log'),
        logging.StreamHandler()
    ]
)

# -----------------------------------------------------
# Database setup
# -----------------------------------------------------
def setup_database():
    conn = sqlite3.connect(DB_FILE)
    cursor = conn.cursor()
    cursor.execute('''
        CREATE TABLE IF NOT EXISTS measurements (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            study_uid TEXT,
            measurement_name TEXT,
            measurement_id TEXT,
            result_no INTEGER,
            display_value REAL,
            UNIQUE(study_uid, measurement_id, result_no)
        )
    ''')
    conn.commit()
    return conn

# -----------------------------------------------------
# Measurement extraction from SR
# -----------------------------------------------------
def extract_measurements(dataset):
    results = []
    try:
        if not hasattr(dataset, 'ContentSequence'):
            return results

        for item in dataset.ContentSequence:
            try:
                measurement_id = None
                measurement_name = None
                display_values = []

                if hasattr(item, 'ConceptNameCodeSequence'):
                    cncs = item.ConceptNameCodeSequence[0]
                    measurement_id = f"{cncs.get('CodeValue', 'Unknown')}"
                    measurement_name = cncs.get('CodeMeaning', 'Unknown')

                if hasattr(item, 'MeasuredValueSequence'):
                    mvs = item.MeasuredValueSequence[0]
                    try:
                        val = float(mvs.get('NumericValue', 0))
                        display_values.append(val)
                    except (ValueError, TypeError) as ve:
                        logging.warning(f"Could not convert measurement value: {ve}")

                if measurement_id and display_values:
                    for idx, val in enumerate(display_values):
                        results.append((measurement_id, measurement_name, idx, val))

                # Recursive call with additional error handling
                results.extend(extract_measurements(item))

            except Exception as item_error:
                logging.error(f"Error processing content item: {item_error}")

    except Exception as dataset_error:
        logging.error(f"Error extracting measurements: {dataset_error}")

    return results

def is_valid_dicom(file_path):
    try:
        pydicom.dcmread(file_path, stop_before_pixels=True)
        return True
    except Exception:
        return False

# In your file handler
def on_created(self, event):
    if (not event.is_directory and 
        event.src_path.lower().endswith(".dcm") and 
        is_valid_dicom(event.src_path)):
        # Process file
        parse_and_store_sr_data(event.src_path)

# -----------------------------------------------------
# Parse & store SR data into DB
# -----------------------------------------------------
def parse_and_store_sr_data(file_path):
    conn = sqlite3.connect(DB_FILE)
    cursor = conn.cursor()
    try:
        # Add more robust parsing options
        ds = pydicom.dcmread(file_path, force=True, stop_before_pixels=True)
        
        # Additional error checking
        if not ds:
            print(f"[!] Unable to read DICOM file: {file_path}")
            return

        study_uid_elem = ds.get((0x0020, 0x000d))
        if study_uid_elem:
            study_uid = study_uid_elem.value
        else:
            study_uid = None
        print("StudyInstanceUID:", study_uid)
        if not study_uid:
            print(f"[!] No StudyInstanceUID for {file_path}, skipping")
            return

        measurements = extract_measurements(ds)
        if not measurements:
            print(f"[!] No measurements found in {file_path}")
            return

        count = 0
        for measurement_id, measurement_name, result_no, display_value in measurements:
            cursor.execute('''
                INSERT OR REPLACE INTO measurements 
                (id, study_uid, measurement_name, measurement_id, result_no, display_value)
                VALUES (
                    COALESCE((SELECT id FROM measurements 
                              WHERE study_uid = ? AND measurement_id = ? AND result_no = ?),
                             NULL),
                    ?, ?, ?, ?, ?
                )
            ''', (study_uid, measurement_id, result_no,
                  study_uid, measurement_name, measurement_id, result_no, display_value))
            count += 1

        conn.commit()
        print(f"[+] Stored/updated {count} measurements for StudyUID {study_uid}")

    except pydicom.errors.InvalidDicomError as e:
        print(f"[!] Invalid DICOM file {file_path}: {e}")
    except Exception as e:
        print(f"[!] Unexpected error parsing {file_path}: {e}")
    finally:
        conn.close()
# -----------------------------------------------------
# Move file to archive
# -----------------------------------------------------
def archive_file(file_path):
    os.makedirs(ARCHIVE_FOLDER, exist_ok=True)
    dest_path = os.path.join(ARCHIVE_FOLDER, os.path.basename(file_path))
    shutil.move(file_path, dest_path)
    print(f"📦 Archived file: {dest_path}")

# -----------------------------------------------------
# Cleanup old files from archive
# -----------------------------------------------------
def cleanup_archive():
    now = datetime.now()
    cutoff = now - timedelta(days=ARCHIVE_RETENTION_DAYS)
    if not os.path.exists(ARCHIVE_FOLDER):
        return
    for filename in os.listdir(ARCHIVE_FOLDER):
        file_path = os.path.join(ARCHIVE_FOLDER, filename)
        if os.path.isfile(file_path):
            mtime = datetime.fromtimestamp(os.path.getmtime(file_path))
            if mtime < cutoff:
                os.remove(file_path)
                print(f"🗑️ Deleted old archive file: {filename}")

# -----------------------------------------------------
# Watchdog event handler
# -----------------------------------------------------
class SRFileHandler(FileSystemEventHandler):
    def on_created(self, event):
        if not event.is_directory and event.src_path.lower().endswith(".dcm"):
            cleanup_archive()  # Run cleanup each time we get a new file
            print(f"🔄 New SR file detected: {event.src_path}")
            parse_and_store_sr_data(event.src_path)
            archive_file(event.src_path)

# -----------------------------------------------------
# Main
# -----------------------------------------------------
if __name__ == "__main__":
    print(f"👀 Watching folder: {SR_FOLDER}")
    print("Use Ctrl + c to stop the script!")
    os.makedirs(SR_FOLDER, exist_ok=True)
    setup_database()  # Just create the DB structure here, no connection needed later

    event_handler = SRFileHandler()
    observer = Observer()
    observer.schedule(event_handler, SR_FOLDER, recursive=False)
    observer.start()

    try:
        while True:
            time.sleep(1)  # Keep process alive
    except KeyboardInterrupt:
        observer.stop()
    observer.join()
