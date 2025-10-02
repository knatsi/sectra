IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'MyDatabase')
BEGIN
    CREATE DATABASE MyDatabase;
END
GO

USE MyDatabase;
GO

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Exams' AND xtype='U')
BEGIN
    CREATE TABLE Exams (
        ExamID INT IDENTITY PRIMARY KEY,
        PersonalID NVARCHAR(20) NOT NULL,
        ExamName NVARCHAR(100),
        ExamDate DATETIME,
        PdfPath NVARCHAR(500)
    );
END
GO