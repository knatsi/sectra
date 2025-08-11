import pydicom

ds = pydicom.dcmread("archive/1.2.840.113619.2.391.151813.1754416748.3008.223.dcm")
study_uid_elem = ds.get((0x0020, 0x000d))
if study_uid_elem:
    print("StudyInstanceUID:", study_uid_elem.value)
else:
    print("StudyInstanceUID tag not found or empty")
