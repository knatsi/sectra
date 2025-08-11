from fastapi import FastAPI
from pydantic import BaseModel
from typing import List
import sqlite3
import uvicorn

app = FastAPI(title="Measurement API", version="1.0")

DB_FILE = "dicomsrmeasurements.db"

# --- Compatibility info (static) ---
COMPATIBILITY_INFO = {
    "uid": "DataproviderHeartEcho",
    "version": 1
}

# --- Request models ---
class UserInfo(BaseModel):
    login: str
    domain: str
    name: str

class PatientInfo(BaseModel):
    ids: List[str]

class ExamInfo(BaseModel):
    examNo: str
    accNo: str
    studyUid: str
    date: str  # keep as string

class StudyRequest(BaseModel):
    user: UserInfo
    patient: PatientInfo
    exam: ExamInfo

# --- DB utility ---
def get_db_connection():
    conn = sqlite3.connect(DB_FILE)
    conn.row_factory = sqlite3.Row
    return conn

# --- Endpoints ---
@app.get("/v1/GetStructuredDataCompatibility")
def get_structured_data_compatibility():
    return COMPATIBILITY_INFO

@app.post("/v1/GetStructuredData")
def get_structured_data(req: StudyRequest):
    conn = get_db_connection()
    cursor = conn.cursor()

    cursor.execute("""
        SELECT measurement_id, result_no, display_value
        FROM measurements
        WHERE study_uid = ?
    """, (req.exam.studyUid,))
    rows = cursor.fetchall()
    conn.close()

    if not rows:
        raise HTTPException(status_code=404, detail="No measurements found for this StudyUID")

    # Build propValues as { "<measurement_id>_<result_no>": display_value }
    prop_values: Dict[str, Any] = {}
    for row in rows:
        key = f"{row['measurement_id']}_{row['result_no']}"
        val = row["display_value"]
        try:
            num_val = float(val)
            if num_val.is_integer():
                num_val = int(num_val)
            prop_values[key] = num_val
        except (ValueError, TypeError):
            prop_values[key] = val

    return {
        "compatibility": COMPATIBILITY_INFO,
        "propValues": prop_values
    }

# ---------------------------
# Auto-start when run directly
# ---------------------------
if __name__ == "__main__":
    uvicorn.run("WebAPI:app", host="0.0.0.0", port=8000, reload=True)