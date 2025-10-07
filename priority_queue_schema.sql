-- Queue Status Lookup Table
CREATE TABLE QueueStatus (
    Id TINYINT PRIMARY KEY,
    StatusName NVARCHAR(50) NOT NULL,
    Description NVARCHAR(255) NULL
);

-- Insert status values
INSERT INTO QueueStatus (Id, StatusName, Description) VALUES
(0, 'Pending', 'Item is waiting to be processed'),
(1, 'Processing', 'Item is currently being processed'),
(2, 'Completed', 'Item has been successfully processed'),
(3, 'Failed', 'Item has permanently failed after max retries');

GO

-- Priority Queue Table
CREATE TABLE PriorityQueue (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    Priority INT NOT NULL,
    Payload NVARCHAR(MAX) NOT NULL,
    CreatedDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ProcessedDate DATETIME2 NULL,
    StatusId TINYINT NOT NULL DEFAULT 0,
    RetryCount INT NOT NULL DEFAULT 0,
    LastError NVARCHAR(MAX) NULL,
    LockedBy NVARCHAR(255) NULL,
    LockedUntil DATETIME2 NULL,
    CONSTRAINT FK_PriorityQueue_Status FOREIGN KEY (StatusId) REFERENCES QueueStatus(Id)
);

-- Indexes for performance
CREATE INDEX IX_PriorityQueue_Status_Priority_Created 
    ON PriorityQueue(StatusId, Priority DESC, CreatedDate ASC)
    INCLUDE (Id, Payload);

CREATE INDEX IX_PriorityQueue_LockedUntil 
    ON PriorityQueue(LockedUntil)
    WHERE StatusId = 1;

GO

-- Stored Procedure: Enqueue item
CREATE PROCEDURE sp_PriorityQueue_Enqueue
    @Priority INT,
    @Payload NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    
    INSERT INTO PriorityQueue (Priority, Payload, StatusId)
    VALUES (@Priority, @Payload, 0);
    
    SELECT SCOPE_IDENTITY() AS Id;
END
GO

-- Stored Procedure: Dequeue item (with locking)
CREATE PROCEDURE sp_PriorityQueue_Dequeue
    @WorkerId NVARCHAR(255),
    @LockDurationSeconds INT = 300
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @Now DATETIME2 = GETUTCDATE();
    DECLARE @LockUntil DATETIME2 = DATEADD(SECOND, @LockDurationSeconds, @Now);
    DECLARE @Id BIGINT;
    
    -- Get highest priority pending item or stale locked items
    UPDATE TOP(1) PriorityQueue
    SET 
        StatusId = 1,
        LockedBy = @WorkerId,
        LockedUntil = @LockUntil,
        @Id = Id
    WHERE StatusId = 0 
       OR (StatusId = 1 AND LockedUntil < @Now)
    ORDER BY Priority DESC, CreatedDate ASC;
    
    -- Return the dequeued item
    SELECT 
        Id,
        Priority,
        Payload,
        CreatedDate,
        RetryCount
    FROM PriorityQueue
    WHERE Id = @Id;
END
GO

-- Stored Procedure: Complete item
CREATE PROCEDURE sp_PriorityQueue_Complete
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    
    UPDATE PriorityQueue
    SET 
        StatusId = 2,
        ProcessedDate = GETUTCDATE(),
        LockedBy = NULL,
        LockedUntil = NULL
    WHERE Id = @Id;
END
GO

-- Stored Procedure: Fail item
CREATE PROCEDURE sp_PriorityQueue_Fail
    @Id BIGINT,
    @ErrorMessage NVARCHAR(MAX),
    @MaxRetries INT = 3
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @NewRetryCount INT;
    
    UPDATE PriorityQueue
    SET 
        RetryCount = RetryCount + 1,
        LastError = @ErrorMessage,
        StatusId = CASE 
            WHEN RetryCount + 1 >= @MaxRetries THEN 3 -- Failed permanently
            ELSE 0 -- Retry
        END,
        LockedBy = NULL,
        LockedUntil = NULL,
        @NewRetryCount = RetryCount + 1
    WHERE Id = @Id;
    
    SELECT @NewRetryCount AS RetryCount;
END
GO

-- Stored Procedure: Get queue statistics
CREATE PROCEDURE sp_PriorityQueue_GetStats
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        SUM(CASE WHEN StatusId = 0 THEN 1 ELSE 0 END) AS PendingCount,
        SUM(CASE WHEN StatusId = 1 THEN 1 ELSE 0 END) AS ProcessingCount,
        SUM(CASE WHEN StatusId = 2 THEN 1 ELSE 0 END) AS CompletedCount,
        SUM(CASE WHEN StatusId = 3 THEN 1 ELSE 0 END) AS FailedCount,
        COUNT(*) AS TotalCount
    FROM PriorityQueue;
END
GO

-- Stored Procedure: Cleanup old completed items
CREATE PROCEDURE sp_PriorityQueue_Cleanup
    @RetentionDays INT = 7
AS
BEGIN
    SET NOCOUNT ON;
    
    DELETE FROM PriorityQueue
    WHERE StatusId = 2 
      AND ProcessedDate < DATEADD(DAY, -@RetentionDays, GETUTCDATE());
      
    SELECT @@ROWCOUNT AS DeletedCount;
END
GO
