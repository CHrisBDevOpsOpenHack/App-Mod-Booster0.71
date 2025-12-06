-- Stored Procedures for Expense Management System
-- Uses CREATE OR ALTER to allow repeated execution
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- =============================================
-- Get all expenses with user and category details
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[GetAllExpenses]
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        e.ExpenseId,
        e.AmountMinor,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedAt,
        e.CreatedAt,
        u.UserName,
        u.Email,
        ec.CategoryName,
        es.StatusName,
        reviewer.UserName AS ReviewerName
    FROM dbo.Expenses e
    INNER JOIN dbo.Users u ON e.UserId = u.UserId
    INNER JOIN dbo.ExpenseCategories ec ON e.CategoryId = ec.CategoryId
    INNER JOIN dbo.ExpenseStatus es ON e.StatusId = es.StatusId
    LEFT JOIN dbo.Users reviewer ON e.ReviewedBy = reviewer.UserId
    ORDER BY e.ExpenseDate DESC, e.CreatedAt DESC;
END
GO

-- =============================================
-- Get expenses by status
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[GetExpensesByStatus]
    @StatusName NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        e.ExpenseId,
        e.AmountMinor,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedAt,
        e.CreatedAt,
        u.UserName,
        u.Email,
        ec.CategoryName,
        es.StatusName,
        reviewer.UserName AS ReviewerName
    FROM dbo.Expenses e
    INNER JOIN dbo.Users u ON e.UserId = u.UserId
    INNER JOIN dbo.ExpenseCategories ec ON e.CategoryId = ec.CategoryId
    INNER JOIN dbo.ExpenseStatus es ON e.StatusId = es.StatusId
    LEFT JOIN dbo.Users reviewer ON e.ReviewedBy = reviewer.UserId
    WHERE es.StatusName = @StatusName
    ORDER BY e.ExpenseDate DESC, e.CreatedAt DESC;
END
GO

-- =============================================
-- Get a single expense by ID
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[GetExpenseById]
    @ExpenseId INT
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        e.ExpenseId,
        e.AmountMinor,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedAt,
        e.CreatedAt,
        e.UserId,
        u.UserName,
        u.Email,
        e.CategoryId,
        ec.CategoryName,
        e.StatusId,
        es.StatusName,
        e.ReviewedBy,
        reviewer.UserName AS ReviewerName
    FROM dbo.Expenses e
    INNER JOIN dbo.Users u ON e.UserId = u.UserId
    INNER JOIN dbo.ExpenseCategories ec ON e.CategoryId = ec.CategoryId
    INNER JOIN dbo.ExpenseStatus es ON e.StatusId = es.StatusId
    LEFT JOIN dbo.Users reviewer ON e.ReviewedBy = reviewer.UserId
    WHERE e.ExpenseId = @ExpenseId;
END
GO

-- =============================================
-- Create a new expense
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[CreateExpense]
    @UserId INT,
    @CategoryId INT,
    @AmountMinor INT,
    @Currency NVARCHAR(3),
    @ExpenseDate DATE,
    @Description NVARCHAR(1000) = NULL,
    @ReceiptFile NVARCHAR(500) = NULL,
    @StatusName NVARCHAR(50) = 'Draft'
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @StatusId INT;
    DECLARE @NewExpenseId INT;
    
    -- Get the status ID
    SELECT @StatusId = StatusId 
    FROM dbo.ExpenseStatus 
    WHERE StatusName = @StatusName;
    
    -- Insert the expense
    INSERT INTO dbo.Expenses (
        UserId, CategoryId, StatusId, AmountMinor, Currency, 
        ExpenseDate, Description, ReceiptFile, CreatedAt
    )
    VALUES (
        @UserId, @CategoryId, @StatusId, @AmountMinor, @Currency,
        @ExpenseDate, @Description, @ReceiptFile, SYSUTCDATETIME()
    );
    
    SET @NewExpenseId = SCOPE_IDENTITY();
    
    -- Return the newly created expense
    EXEC [dbo].[GetExpenseById] @ExpenseId = @NewExpenseId;
END
GO

-- =============================================
-- Update an expense
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[UpdateExpense]
    @ExpenseId INT,
    @CategoryId INT,
    @AmountMinor INT,
    @Currency NVARCHAR(3),
    @ExpenseDate DATE,
    @Description NVARCHAR(1000) = NULL,
    @ReceiptFile NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    UPDATE dbo.Expenses
    SET 
        CategoryId = @CategoryId,
        AmountMinor = @AmountMinor,
        Currency = @Currency,
        ExpenseDate = @ExpenseDate,
        Description = @Description,
        ReceiptFile = @ReceiptFile
    WHERE ExpenseId = @ExpenseId;
    
    -- Return the updated expense
    EXEC [dbo].[GetExpenseById] @ExpenseId = @ExpenseId;
END
GO

-- =============================================
-- Submit an expense for approval
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[SubmitExpense]
    @ExpenseId INT
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @StatusId INT;
    
    -- Get the 'Submitted' status ID
    SELECT @StatusId = StatusId 
    FROM dbo.ExpenseStatus 
    WHERE StatusName = 'Submitted';
    
    -- Update the expense status
    UPDATE dbo.Expenses
    SET 
        StatusId = @StatusId,
        SubmittedAt = SYSUTCDATETIME()
    WHERE ExpenseId = @ExpenseId;
    
    -- Return the updated expense
    EXEC [dbo].[GetExpenseById] @ExpenseId = @ExpenseId;
END
GO

-- =============================================
-- Approve an expense
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[ApproveExpense]
    @ExpenseId INT,
    @ReviewedBy INT
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @StatusId INT;
    
    -- Get the 'Approved' status ID
    SELECT @StatusId = StatusId 
    FROM dbo.ExpenseStatus 
    WHERE StatusName = 'Approved';
    
    -- Update the expense status
    UPDATE dbo.Expenses
    SET 
        StatusId = @StatusId,
        ReviewedBy = @ReviewedBy,
        ReviewedAt = SYSUTCDATETIME()
    WHERE ExpenseId = @ExpenseId;
    
    -- Return the updated expense
    EXEC [dbo].[GetExpenseById] @ExpenseId = @ExpenseId;
END
GO

-- =============================================
-- Reject an expense
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[RejectExpense]
    @ExpenseId INT,
    @ReviewedBy INT
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @StatusId INT;
    
    -- Get the 'Rejected' status ID
    SELECT @StatusId = StatusId 
    FROM dbo.ExpenseStatus 
    WHERE StatusName = 'Rejected';
    
    -- Update the expense status
    UPDATE dbo.Expenses
    SET 
        StatusId = @StatusId,
        ReviewedBy = @ReviewedBy,
        ReviewedAt = SYSUTCDATETIME()
    WHERE ExpenseId = @ExpenseId;
    
    -- Return the updated expense
    EXEC [dbo].[GetExpenseById] @ExpenseId = @ExpenseId;
END
GO

-- =============================================
-- Get all categories
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[GetAllCategories]
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT CategoryId, CategoryName, IsActive
    FROM dbo.ExpenseCategories
    WHERE IsActive = 1
    ORDER BY CategoryName;
END
GO

-- =============================================
-- Get all users
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[GetAllUsers]
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        u.UserId,
        u.UserName,
        u.Email,
        r.RoleName,
        u.IsActive,
        u.CreatedAt,
        manager.UserName AS ManagerName
    FROM dbo.Users u
    INNER JOIN dbo.Roles r ON u.RoleId = r.RoleId
    LEFT JOIN dbo.Users manager ON u.ManagerId = manager.UserId
    WHERE u.IsActive = 1
    ORDER BY u.UserName;
END
GO

-- =============================================
-- Get all statuses
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[GetAllStatuses]
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT StatusId, StatusName
    FROM dbo.ExpenseStatus
    ORDER BY StatusId;
END
GO

-- =============================================
-- Delete an expense
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[DeleteExpense]
    @ExpenseId INT
AS
BEGIN
    SET NOCOUNT ON;
    
    DELETE FROM dbo.Expenses
    WHERE ExpenseId = @ExpenseId;
    
    SELECT @@ROWCOUNT AS RowsAffected;
END
GO

-- =============================================
-- Search expenses by filter text
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[SearchExpenses]
    @FilterText NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        e.ExpenseId,
        e.AmountMinor,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedAt,
        e.CreatedAt,
        u.UserName,
        u.Email,
        ec.CategoryName,
        es.StatusName,
        reviewer.UserName AS ReviewerName
    FROM dbo.Expenses e
    INNER JOIN dbo.Users u ON e.UserId = u.UserId
    INNER JOIN dbo.ExpenseCategories ec ON e.CategoryId = ec.CategoryId
    INNER JOIN dbo.ExpenseStatus es ON e.StatusId = es.StatusId
    LEFT JOIN dbo.Users reviewer ON e.ReviewedBy = reviewer.UserId
    WHERE 
        e.Description LIKE '%' + @FilterText + '%'
        OR ec.CategoryName LIKE '%' + @FilterText + '%'
        OR u.UserName LIKE '%' + @FilterText + '%'
        OR es.StatusName LIKE '%' + @FilterText + '%'
    ORDER BY e.ExpenseDate DESC, e.CreatedAt DESC;
END
GO
