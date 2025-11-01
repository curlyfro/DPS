-- =====================================================================================
-- Script: Fix Stuck ProcessingQueue Items
-- Description: Synchronizes ProcessingQueue status with Document status for items
--              that are stuck in Pending or InProgress state
-- =====================================================================================

USE DocumentProcessorDB;
GO

-- Show current status counts before fix
PRINT '======================================';
PRINT 'BEFORE FIX - Current Status Counts:';
PRINT '======================================';

SELECT
    'ProcessingQueue' AS TableName,
    Status,
    COUNT(*) AS Count
FROM ProcessingQueues
GROUP BY Status
ORDER BY Status;

SELECT
    'Documents' AS TableName,
    Status,
    COUNT(*) AS Count
FROM Documents
GROUP BY Status
ORDER BY Status;

PRINT '';
PRINT '======================================';
PRINT 'Processing Mismatched Items...';
PRINT '======================================';

-- Start a transaction for safety
BEGIN TRANSACTION;

BEGIN TRY
    -- Update ProcessingQueue items to Completed where Document is Processed
    -- but queue is still Pending, InProgress, or Retrying
    UPDATE pq
    SET
        pq.Status = 3, -- ProcessingStatus.Completed = 3
        pq.CompletedAt = GETUTCDATE(),
        pq.UpdatedAt = GETUTCDATE()
    FROM ProcessingQueues pq
    INNER JOIN Documents d ON pq.DocumentId = d.Id
    WHERE
        d.Status = 2 -- DocumentStatus.Processed = 2
        AND pq.Status IN (0, 1, 4) -- Pending = 0, InProgress = 1, Retrying = 4
        AND pq.CompletedAt IS NULL;

    PRINT 'Updated ' + CAST(@@ROWCOUNT AS VARCHAR) + ' queue items to Completed status';

    -- Update ProcessingQueue items to Failed where Document is Failed
    -- but queue is still Pending, InProgress, or Retrying
    UPDATE pq
    SET
        pq.Status = 2, -- ProcessingStatus.Failed = 2
        pq.ErrorMessage = COALESCE(pq.ErrorMessage, 'Document processing failed'),
        pq.UpdatedAt = GETUTCDATE()
    FROM ProcessingQueues pq
    INNER JOIN Documents d ON pq.DocumentId = d.Id
    WHERE
        d.Status = 3 -- DocumentStatus.Failed = 3
        AND pq.Status IN (0, 1, 4) -- Pending = 0, InProgress = 1, Retrying = 4
        AND pq.CompletedAt IS NULL;

    PRINT 'Updated ' + CAST(@@ROWCOUNT AS VARCHAR) + ' queue items to Failed status';

    -- Commit the transaction
    COMMIT TRANSACTION;
    PRINT 'Transaction committed successfully';

END TRY
BEGIN CATCH
    -- Rollback on error
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    PRINT 'ERROR: Transaction rolled back';
    PRINT 'Error Message: ' + ERROR_MESSAGE();

    -- Re-throw the error
    THROW;
END CATCH;

PRINT '';
PRINT '======================================';
PRINT 'AFTER FIX - Updated Status Counts:';
PRINT '======================================';

-- Show status counts after fix
SELECT
    'ProcessingQueue' AS TableName,
    Status,
    COUNT(*) AS Count
FROM ProcessingQueues
GROUP BY Status
ORDER BY Status;

SELECT
    'Documents' AS TableName,
    Status,
    COUNT(*) AS Count
FROM Documents
GROUP BY Status
ORDER BY Status;

PRINT '';
PRINT '======================================';
PRINT 'Verification - Check for Remaining Mismatches:';
PRINT '======================================';

-- Show any remaining mismatches
SELECT
    pq.Id AS QueueItemId,
    pq.DocumentId,
    d.FileName,
    pq.Status AS QueueStatus,
    d.Status AS DocumentStatus,
    pq.CreatedAt AS QueueCreatedAt,
    pq.UpdatedAt AS QueueUpdatedAt,
    d.ProcessedAt AS DocumentProcessedAt
FROM ProcessingQueues pq
INNER JOIN Documents d ON pq.DocumentId = d.Id
WHERE
    (d.Status = 2 AND pq.Status IN (0, 1, 4)) -- Doc Processed but Queue not Completed
    OR (d.Status = 3 AND pq.Status IN (0, 1, 4)) -- Doc Failed but Queue not Failed
ORDER BY pq.CreatedAt;

IF @@ROWCOUNT = 0
    PRINT 'No remaining mismatches found. All items are synchronized!';
ELSE
    PRINT 'WARNING: Some items still have mismatched statuses';

GO
