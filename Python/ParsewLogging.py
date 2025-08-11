import os
import sys
import shutil
import logging
import sqlite3
import time
from datetime import datetime, timedelta

import pydicom
from pydicom.errors import InvalidDicomError
from watchdog.observers import Observer
from watchdog.events import FileSystemEventHandler

# Configure comprehensive logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s: %(message)s',
    handlers=[
        logging.FileHandler('dicom_parser.log'),
        logging.StreamHandler(sys.stdout)
    ]
)
logger = logging.getLogger(__name__)

# Configuration Constants
SR_FOLDER = "dicom_sr_in"
ARCHIVE_FOLDER = "archive"
INVALID_FOLDER = "invalid_dicoms"
DB_FILE = "dicomsrmeasurements.db"
ARCHIVE_RETENTION_DAYS = 10

def advanced_dicom_read(file_path):
    """
    Advanced DICOM reading with multiple strategies
    """
    read_strategies = [
        # Standard read
        lambda: pydicom.dcmread(file_path),
        
        # Force read with specific options
        lambda: pydicom.dcmread(file_path, force=True),
        
        # Force read with additional parameters
        lambda: pydicom.dcmread(file_path, force=True, specific_tags=None),
        
        # Stop before pixels with force
        lambda: pydicom.dcmread(file_path, stop_before_pixels=True, force=True)
    ]

    last_error = None
    for strategy in read_strategies:
        try:
            dataset = strategy()
            
            # Additional validation
            if dataset is None:
                raise ValueError("Dataset is None")
            
            return dataset
        
        except Exception as e:
            logging.warning(f"DICOM read strategy failed: {e}")
            last_error = e
    
    # Detailed file header investigation
    try:
        with open(file_path, 'rb') as f:
            file_header = f.read(256)
            logging.info(f"File Header (Hex): {file_header.hex()}")
    except Exception as read_error:
        logging.error(f"Could not read file header: {read_error}")
    
    raise last_error

def comprehensive_dicom_parsing(file_path):
    """
    Comprehensive DICOM file parsing with multiple fallback strategies
    """
    try:
        # Advanced reading strategy
        ds = advanced_dicom_read(file_path)
        
        # Extract Study UID with multiple fallback methods
        study_uid = None
        uid_tags = [
            (0x0020, 0x000d),  # Standard StudyInstanceUID
            (0x0008, 0x0018),  # SOPInstanceUID as fallback
            (0x0020, 0x0010)   # Study ID as last resort
        ]
        
        for tag in uid_tags:
            try:
                uid_elem = ds.get(tag)
                if uid_elem and uid_elem.value:
                    study_uid = str(uid_elem.value)
                    break
            except Exception as tag_error:
                logging.warning(f"Could not extract UID from tag {tag}: {tag_error}")
        
        if not study_uid:
            # Generate a unique identifier if no standard UID found
            study_uid = f"GENERATED_{os.path.basename(file_path)}"
            logging.warning(f"Generated study UID: {study_uid}")
        
        # Rest of your parsing logic...
        measurements = extract_measurements(ds)
        
        return study_uid, measurements
    
    except Exception as parsing_error:
        logging.error(f"Comprehensive parsing failed for {file_path}: {parsing_error}")
        raise

def safe_dicom_read(file_path):
    """
    Safely read DICOM file with multiple strategies
    """
    read_strategies = [
        lambda: pydicom.dcmread(file_path),
        lambda: pydicom.dcmread(file_path, force=True),
        lambda: pydicom.dcmread(file_path, stop_before_pixels=True)
    ]

    for strategy in read_strategies:
        try:
            return strategy()
        except Exception as e:
            logger.warning(f"DICOM read strategy failed: {e}")
    
    raise ValueError("All DICOM read strategies failed")

def extract_measurements(dataset):
    """
    Robust measurement extraction with comprehensive error handling
    """
    results = []
    
    try:
        # Check if ContentSequence exists with multiple validation methods
        content_sequence = getattr(dataset, 'ContentSequence', None)
        
        if not content_sequence:
            logging.warning("No ContentSequence found in dataset")
            return results
        
        for item in content_sequence:
            try:
                # Robust extraction with multiple fallback methods
                measurement_id = 'Unknown'
                measurement_name = 'Unknown'
                display_values = []
                
                # Extract concept name
                if hasattr(item, 'ConceptNameCodeSequence'):
                    concept_name = item.ConceptNameCodeSequence[0]
                    measurement_id = str(getattr(concept_name, 'CodeValue', 'Unknown'))
                    measurement_name = str(getattr(concept_name, 'CodeMeaning', 'Unknown'))
                
                # Extract measured value
                if hasattr(item, 'MeasuredValueSequence'):
                    measured_value = item.MeasuredValueSequence[0]
                    try:
                        val = float(getattr(measured_value, 'NumericValue', 0))
                        display_values.append(val)
                    except (ValueError, TypeError) as ve:
                        logging.warning(f"Measurement conversion error: {ve}")
                
                # Add measurements
                if measurement_id and display_values:
                    for idx, val in enumerate(display_values):
                        results.append((measurement_id, measurement_name, idx, val))
                
                # Recursive extraction
                results.extend(extract_measurements(item))
            
            except Exception as item_error:
                logging.error(f"Content item processing error: {item_error}")
    
    except Exception as extraction_error:
        logging.error(f"Measurement extraction error: {extraction_error}")
    
    return results

def parse_and_store_sr_data(file_path):
    """
    Enhanced DICOM SR data parsing with comprehensive error handling
    """
    conn = sqlite3.connect(DB_FILE)
    cursor = conn.cursor()
    
    try:
        # Comprehensive parsing
        study_uid, measurements = comprehensive_dicom_parsing(file_path)
        
        if not measurements:
            logging.warning(f"No measurements found in {file_path}")
            return False
        
        count = 0
        for measurement_id, measurement_name, result_no, display_value in measurements:
            try:
                cursor.execute('''
                    INSERT OR REPLACE INTO measurements 
                    (study_uid, measurement_name, measurement_id, result_no, display_value)
                    VALUES (?, ?, ?, ?, ?)
                ''', (study_uid, measurement_name, measurement_id, result_no, display_value))
                count += 1
            except Exception as insert_error:
                logging.error(f"Failed to insert measurement: {insert_error}")
        
        conn.commit()
        logging.info(f"Stored/updated {count} measurements for StudyUID {study_uid}")
        return True
    
    except Exception as e:
        logging.error(f"Error parsing {file_path}: {e}")
        return False
    finally:
        conn.close()

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

class SRFileHandler(FileSystemEventHandler):
    def on_created(self, event):
        if not event.is_directory and event.src_path.lower().endswith(".dcm"):
            try:
                diagnostics = comprehensive_dicom_diagnostics(event.src_path)
                
                if any(attempt['success'] for attempt in diagnostics['read_attempts']):
                    logger.info(f"Processing DICOM file: {event.src_path}")
                    if parse_and_store_sr_data(event.src_path):
                        archive_file(event.src_path)
                    else:
                        move_to_invalid(event.src_path)
                else:
                    logger.error(f"All read strategies failed for {event.src_path}")
                    move_to_invalid(event.src_path)
                    
                    # Log detailed diagnostics
                    logger.error(f"Diagnostics: {diagnostics}")
            
            except Exception as e:
                logger.error(f"Unexpected error processing {event.src_path}: {e}")
                move_to_invalid(event.src_path)

def move_to_invalid(file_path):
    """Move problematic files to invalid folder"""
    os.makedirs(INVALID_FOLDER, exist_ok=True)
    invalid_path = os.path.join(INVALID_FOLDER, os.path.basename(file_path))
    shutil.move(file_path, invalid_path)
    logger.warning(f"Moved to invalid folder: {invalid_path}")

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