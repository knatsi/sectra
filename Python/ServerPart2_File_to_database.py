import sqlite3
import pydicom
import os
import shutil
import time
from datetime import datetime, timedelta
from watchdog.observers import Observer
from watchdog.events import FileSystemEventHandler

# Configuration
SR_FOLDER = "dicom_sr_in"
ARCHIVE_FOLDER = "archive"
DB_FILE = "dicomsrmeasuremntdatabase.db"
ARCHIVE_RETENTION_DAYS = 10
CLEANUP_INTERVAL_SECONDS = 3600  # Once per hour

last_cleanup_time = 0  # Global tracker

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
            dataprovider TEXT,
            patient_id TEXT,
            exam_id TEXT,
            acquisition_time TEXT,
            measurement_name TEXT,
            measurement_id TEXT,
            result_no INTEGER,
            display_value REAL,
            UNIQUE(study_uid, measurement_id, result_no)
        )
    ''')
    conn.commit()
    conn.close()

# -----------------------------------------------------
# Measurement extraction from SR
# -----------------------------------------------------
def extract_measurements(dataset):
    results = []
    if 'ContentSequence' not in dataset:
        return results

    for item in dataset.ContentSequence:
        measurement_id = None
        measurement_name = None
        display_values = []

        if 'ConceptNameCodeSequence' in item:
            cncs = item.ConceptNameCodeSequence[0]
            measurement_id = f"{cncs.CodeValue}"
            measurement_name = cncs.CodeMeaning

        if 'MeasuredValueSequence' in item:
            mvs = item.MeasuredValueSequence[0]
            try:
                val = float(mvs.NumericValue)
                display_values.append(val)
            except Exception:
                pass

        if measurement_id and display_values:
            for idx, val in enumerate(display_values):
                results.append((measurement_id, measurement_name, idx, val))

        if 'ContentSequence' in item:
            results.extend(extract_measurements(item))

    return results

# -----------------------------------------------------
# Parse & store SR data into DB
# -----------------------------------------------------
def parse_and_store_sr_data(file_path):
    # Retry reading to avoid partially written files
    for attempt in range(5):
        try:
            ds = pydicom.dcmread(file_path)
            break
        except Exception as e:
            if attempt < 4:
                time.sleep(0.5)
            else:
                print(f"[!] Could not read file {file_path}: {e}")
                return

    study_uid = getattr(ds, "StudyInstanceUID", None)
    patient_id = getattr(ds, "PatientID", None)
    acquisition_time = getattr(ds, "ContentDate", None)
    dataprovider = "DataproviderHeartEcho"
    exam_id = getattr(ds, "StudyID", None)
    if not study_uid:
        print(f"[!] No StudyInstanceUID for {file_path}, skipping")
        return

    measurements = extract_measurements(ds)
    if not measurements:
        print(f"[!] No measurements found in {file_path}")
        return

    conn = sqlite3.connect(DB_FILE)
    cursor = conn.cursor()
    count = 0
    for measurement_id, measurement_name, result_no, display_value in measurements:
        cursor.execute('''
            INSERT INTO measurements (study_uid, dataprovider, patient_id, exam_id, acquisition_time, measurement_name, measurement_id, result_no, display_value)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
            ON CONFLICT(study_uid, measurement_id, result_no)
            DO UPDATE SET
                dataprovider = excluded.dataprovider,
                patient_id = excluded.patient_id,
                exam_id = excluded.exam_id,
                acquisition_time = excluded.acquisition_time,
                measurement_name = excluded.measurement_name,
                display_value = excluded.display_value
        ''', (study_uid, dataprovider, patient_id, exam_id, acquisition_time, measurement_name, measurement_id, result_no, display_value))
        count += 1
    conn.commit()
    conn.close()

    print(f"[+] Stored/updated {count} measurements for StudyUID {study_uid}")

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
        global last_cleanup_time
        if not event.is_directory and event.src_path.lower().endswith(".dcm"):

            now = time.time()
            if now - last_cleanup_time > CLEANUP_INTERVAL_SECONDS:
                cleanup_archive()
                last_cleanup_time = now

            print(f"🔄 New SR file detected: {event.src_path}")
            parse_and_store_sr_data(event.src_path)
            archive_file(event.src_path)

# -----------------------------------------------------
# Main
# -----------------------------------------------------
if __name__ == "__main__":
    print(f"👀 Watching folder: {SR_FOLDER}")
    os.makedirs(SR_FOLDER, exist_ok=True)
    setup_database()

    event_handler = SRFileHandler()
    observer = Observer()
    observer.schedule(event_handler, SR_FOLDER, recursive=False)
    observer.start()

    try:
        while True:
            time.sleep(1)
    except KeyboardInterrupt:
        observer.stop()
    observer.join()
