-- Stored Procedures for Expense Management System
-- These procedures provide data access layer for the application
-- Use CREATE OR ALTER syntax for idempotency

-- =============================================
-- Get all expenses with related information
-- =============================================
CREATE OR ALTER PROCEDURE dbo.GetExpenses
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        e.ExpenseId,
        e.UserId,
        e.CategoryId,
        e.StatusId,
        e.AmountMinor,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        e.ReviewedAt,
        e.CreatedAt,
        u.UserName,
        c.CategoryName,
        s.StatusName,
        reviewer.UserName AS ReviewerName
    FROM dbo.Expenses e
    INNER JOIN dbo.Users u ON e.UserId = u.UserId
    INNER JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    LEFT JOIN dbo.Users reviewer ON e.ReviewedBy = reviewer.UserId
    ORDER BY e.CreatedAt DESC;
END
GO

-- =============================================
-- Get expenses by status
-- =============================================
CREATE OR ALTER PROCEDURE dbo.GetExpensesByStatus
    @StatusName NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        e.ExpenseId,
        e.UserId,
        e.CategoryId,
        e.StatusId,
        e.AmountMinor,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        e.ReviewedAt,
        e.CreatedAt,
        u.UserName,
        c.CategoryName,
        s.StatusName,
        reviewer.UserName AS ReviewerName
    FROM dbo.Expenses e
    INNER JOIN dbo.Users u ON e.UserId = u.UserId
    INNER JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    LEFT JOIN dbo.Users reviewer ON e.ReviewedBy = reviewer.UserId
    WHERE s.StatusName = @StatusName
    ORDER BY e.CreatedAt DESC;
END
GO

-- =============================================
-- Get expense by ID
-- =============================================
CREATE OR ALTER PROCEDURE dbo.GetExpenseById
    @ExpenseId INT
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        e.ExpenseId,
        e.UserId,
        e.CategoryId,
        e.StatusId,
        e.AmountMinor,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        e.ReviewedAt,
        e.CreatedAt,
        u.UserName,
        c.CategoryName,
        s.StatusName,
        reviewer.UserName AS ReviewerName
    FROM dbo.Expenses e
    INNER JOIN dbo.Users u ON e.UserId = u.UserId
    INNER JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    LEFT JOIN dbo.Users reviewer ON e.ReviewedBy = reviewer.UserId
    WHERE e.ExpenseId = @ExpenseId;
END
GO

-- =============================================
-- Create a new expense
-- =============================================
CREATE OR ALTER PROCEDURE dbo.CreateExpense
    @UserId INT,
    @CategoryId INT,
    @StatusId INT,
    @AmountMinor INT,
    @Currency NVARCHAR(3),
    @ExpenseDate DATE,
    @Description NVARCHAR(1000),
    @ReceiptFile NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Validate currency code
    IF @Currency NOT IN ('GBP', 'USD', 'EUR')
    BEGIN
        RAISERROR('Invalid currency code. Supported currencies: GBP, USD, EUR', 16, 1);
        RETURN;
    END
    
    DECLARE @ExpenseId INT;
    
    INSERT INTO dbo.Expenses (
        UserId,
        CategoryId,
        StatusId,
        AmountMinor,
        Currency,
        ExpenseDate,
        Description,
        ReceiptFile,
        CreatedAt
    )
    VALUES (
        @UserId,
        @CategoryId,
        @StatusId,
        @AmountMinor,
        @Currency,
        @ExpenseDate,
        @Description,
        @ReceiptFile,
        SYSUTCDATETIME()
    );
    
    SET @ExpenseId = SCOPE_IDENTITY();
    
    -- Return the created expense
    EXEC dbo.GetExpenseById @ExpenseId;
END
GO

-- =============================================
-- Update expense status (for approval/rejection)
-- =============================================
CREATE OR ALTER PROCEDURE dbo.UpdateExpenseStatus
    @ExpenseId INT,
    @StatusName NVARCHAR(50),
    @ReviewedBy INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @StatusId INT;
    
    SELECT @StatusId = StatusId 
    FROM dbo.ExpenseStatus 
    WHERE StatusName = @StatusName;
    
    IF @StatusId IS NULL
    BEGIN
        DECLARE @ErrorMessage NVARCHAR(200);
        SET @ErrorMessage = 'Invalid status name: ' + @StatusName + '. Valid statuses: Draft, Submitted, Approved, Rejected';
        RAISERROR(@ErrorMessage, 16, 1);
        RETURN;
    END
    
    UPDATE dbo.Expenses
    SET 
        StatusId = @StatusId,
        ReviewedBy = @ReviewedBy,
        ReviewedAt = CASE 
            WHEN @StatusName IN ('Approved', 'Rejected') THEN SYSUTCDATETIME()
            ELSE ReviewedAt 
        END,
        SubmittedAt = CASE 
            WHEN @StatusName = 'Submitted' AND SubmittedAt IS NULL THEN SYSUTCDATETIME()
            ELSE SubmittedAt 
        END
    WHERE ExpenseId = @ExpenseId;
    
    -- Return the updated expense
    EXEC dbo.GetExpenseById @ExpenseId;
END
GO

-- =============================================
-- Get all categories
-- =============================================
CREATE OR ALTER PROCEDURE dbo.GetCategories
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        CategoryId,
        CategoryName,
        IsActive
    FROM dbo.ExpenseCategories
    WHERE IsActive = 1
    ORDER BY CategoryName;
END
GO

-- =============================================
-- Get all statuses
-- =============================================
CREATE OR ALTER PROCEDURE dbo.GetStatuses
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        StatusId,
        StatusName
    FROM dbo.ExpenseStatus
    ORDER BY StatusId;
END
GO

-- =============================================
-- Get all users
-- =============================================
CREATE OR ALTER PROCEDURE dbo.GetUsers
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        u.UserId,
        u.UserName,
        u.Email,
        u.RoleId,
        u.ManagerId,
        u.IsActive,
        u.CreatedAt,
        r.RoleName,
        m.UserName AS ManagerName
    FROM dbo.Users u
    INNER JOIN dbo.Roles r ON u.RoleId = r.RoleId
    LEFT JOIN dbo.Users m ON u.ManagerId = m.UserId
    WHERE u.IsActive = 1
    ORDER BY u.UserName;
END
GO

-- =============================================
-- Search expenses by description or user name
-- =============================================
CREATE OR ALTER PROCEDURE dbo.SearchExpenses
    @SearchTerm NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        e.ExpenseId,
        e.UserId,
        e.CategoryId,
        e.StatusId,
        e.AmountMinor,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        e.ReviewedAt,
        e.CreatedAt,
        u.UserName,
        c.CategoryName,
        s.StatusName,
        reviewer.UserName AS ReviewerName
    FROM dbo.Expenses e
    INNER JOIN dbo.Users u ON e.UserId = u.UserId
    INNER JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    LEFT JOIN dbo.Users reviewer ON e.ReviewedBy = reviewer.UserId
    WHERE 
        e.Description LIKE '%' + @SearchTerm + '%'
        OR u.UserName LIKE '%' + @SearchTerm + '%'
        OR c.CategoryName LIKE '%' + @SearchTerm + '%'
    ORDER BY e.CreatedAt DESC;
END
GO

-- =============================================
-- Delete expense (soft delete by setting to Draft or hard delete)
-- =============================================
CREATE OR ALTER PROCEDURE dbo.DeleteExpense
    @ExpenseId INT
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Only allow deletion of Draft or Rejected expenses
    IF EXISTS (
        SELECT 1 
        FROM dbo.Expenses e
        INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
        WHERE e.ExpenseId = @ExpenseId 
        AND s.StatusName IN ('Draft', 'Rejected')
    )
    BEGIN
        DELETE FROM dbo.Expenses WHERE ExpenseId = @ExpenseId;
        SELECT 1 AS Success, 'Expense deleted successfully' AS Message;
    END
    ELSE
    BEGIN
        SELECT 0 AS Success, 'Cannot delete expense with current status' AS Message;
    END
END
GO
