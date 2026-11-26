IF OBJECT_ID(N'dbo.Patients', N'U') IS NULL
CREATE TABLE [dbo].[Patients] (
    [Patient ID]     INT            IDENTITY (1, 1) NOT NULL,
    [FirstName]     NCHAR (10)     NOT NULL,
    [LastName]      NCHAR (10)     NOT NULL,
    [Gender]         NCHAR (10)     NOT NULL,
    [DOB]            DATE           NOT NULL,
    [Address]        NVARCHAR (MAX) NOT NULL,
    [Phone]          NCHAR (10)     NOT NULL,
    [AdmissionDate] DATE           NOT NULL,
    [DischargeDate] DATE           NULL,
    [Status]         INT            NOT NULL,
    CONSTRAINT [PK_Table] PRIMARY KEY CLUSTERED ([Patient ID] ASC)
);

