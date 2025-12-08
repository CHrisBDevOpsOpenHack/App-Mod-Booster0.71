SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER PROCEDURE dbo.GetExpenses
    @StatusName NVARCHAR(50) = NULL,
    @UserId INT = NULL,
    @SearchTerm NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        e.ExpenseId,
        e.UserId,
        submitter.UserName AS UserName,
        e.CategoryId,
        c.CategoryName,
        e.StatusId,
        s.StatusName,
        e.AmountMinor,
        CAST(e.AmountMinor / 100.0 AS DECIMAL(10,2)) AS AmountDecimal,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        reviewer.UserName AS ReviewedByName,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    INNER JOIN dbo.Users submitter ON e.UserId = submitter.UserId
    INNER JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    LEFT JOIN dbo.Users reviewer ON e.ReviewedBy = reviewer.UserId
    WHERE (@StatusName IS NULL OR s.StatusName = @StatusName)
      AND (@UserId IS NULL OR e.UserId = @UserId)
      AND (
            @SearchTerm IS NULL
            OR submitter.UserName LIKE '%' + @SearchTerm + '%'
            OR c.CategoryName LIKE '%' + @SearchTerm + '%'
            OR e.Description LIKE '%' + @SearchTerm + '%'
         )
    ORDER BY e.CreatedAt DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.GetExpenseById
    @ExpenseId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        e.ExpenseId,
        e.UserId,
        submitter.UserName AS UserName,
        e.CategoryId,
        c.CategoryName,
        e.StatusId,
        s.StatusName,
        e.AmountMinor,
        CAST(e.AmountMinor / 100.0 AS DECIMAL(10,2)) AS AmountDecimal,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        reviewer.UserName AS ReviewedByName,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    INNER JOIN dbo.Users submitter ON e.UserId = submitter.UserId
    INNER JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    LEFT JOIN dbo.Users reviewer ON e.ReviewedBy = reviewer.UserId
    WHERE e.ExpenseId = @ExpenseId;
END
GO

CREATE OR ALTER PROCEDURE dbo.GetExpenseSummary
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        s.StatusName,
        COUNT(e.ExpenseId) AS ExpenseCount,
        CAST(ISNULL(SUM(e.AmountMinor) / 100.0, 0) AS DECIMAL(18,2)) AS TotalAmount
    FROM dbo.ExpenseStatus s
    LEFT JOIN dbo.Expenses e ON s.StatusId = e.StatusId
    GROUP BY s.StatusName;
END
GO

CREATE OR ALTER PROCEDURE dbo.CreateExpense
    @UserId INT,
    @CategoryId INT,
    @AmountMinor INT,
    @Currency NVARCHAR(3),
    @ExpenseDate DATE,
    @Description NVARCHAR(1000),
    @ReceiptFile NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @StatusId INT = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Submitted');
    IF @StatusId IS NULL
    BEGIN
        RAISERROR('Submitted status not configured', 16, 1);
        RETURN;
    END

    INSERT INTO dbo.Expenses
    (
        UserId, CategoryId, StatusId, AmountMinor, Currency, ExpenseDate,
        Description, ReceiptFile, SubmittedAt, CreatedAt
    )
    VALUES
    (
        @UserId, @CategoryId, @StatusId, @AmountMinor, @Currency, @ExpenseDate,
        @Description, @ReceiptFile, SYSUTCDATETIME(), SYSUTCDATETIME()
    );

    SELECT SCOPE_IDENTITY() AS ExpenseId;
END
GO

CREATE OR ALTER PROCEDURE dbo.UpdateExpenseStatus
    @ExpenseId INT,
    @StatusName NVARCHAR(50),
    @ReviewerId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @StatusId INT = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = @StatusName);
    IF @StatusId IS NULL
    BEGIN
        RAISERROR('Status not found', 16, 1);
        RETURN;
    END

    UPDATE dbo.Expenses
    SET
        StatusId = @StatusId,
        ReviewedBy = @ReviewerId,
        ReviewedAt = CASE WHEN @ReviewerId IS NULL THEN NULL ELSE SYSUTCDATETIME() END
    WHERE ExpenseId = @ExpenseId;
END
GO

CREATE OR ALTER PROCEDURE dbo.GetExpenseCategories
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CategoryId, CategoryName, IsActive FROM dbo.ExpenseCategories WHERE IsActive = 1 ORDER BY CategoryName;
END
GO

CREATE OR ALTER PROCEDURE dbo.GetExpenseStatuses
AS
BEGIN
    SET NOCOUNT ON;
    SELECT StatusId, StatusName FROM dbo.ExpenseStatus ORDER BY StatusName;
END
GO
