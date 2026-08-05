-- Monthly partitioning for Messages and DeliveryEvents (docs/02-database-schema.md §"Scale-out plan").
--
-- NOT applied automatically by EF migrations or Database:MigrateOnStartup — partitioning an
-- existing table requires rebuilding its clustered index, which briefly locks the table and is
-- an operational decision (timing, downtime window, monitoring) rather than a schema change to
-- roll out silently on app startup. Run this manually against a target environment once volume
-- warrants it (see docs/11-scalability.md), during a maintenance window, ideally after a full
-- backup. Requires SQL Server 2016 SP1+ Standard/Enterprise/Developer (Developer edition,
-- used in local dev, already supports this).
--
-- Adjust the boundary range (@startMonth/@numberOfMonths) to your retention policy before running.

SET NOCOUNT ON;

DECLARE @startMonth date = '2026-01-01';
DECLARE @numberOfMonths int = 36;   -- 3 years of monthly boundaries; extend as needed

-- 1. Partition function: one boundary per month start (RANGE RIGHT — the boundary date begins
--    the new partition, so rows are grouped by month of QueuedAtUtc / OccurredAtUtc).
IF NOT EXISTS (SELECT 1 FROM sys.partition_functions WHERE name = 'pf_MonthlyRange')
BEGIN
    DECLARE @sql nvarchar(max) = N'CREATE PARTITION FUNCTION pf_MonthlyRange (datetime2) AS RANGE RIGHT FOR VALUES (';
    DECLARE @i int = 0;
    WHILE @i < @numberOfMonths
    BEGIN
        SET @sql += CASE WHEN @i = 0 THEN '' ELSE ', ' END
            + '''' + CONVERT(varchar(10), DATEADD(MONTH, @i, @startMonth), 120) + '''';
        SET @i += 1;
    END
    SET @sql += ')';
    EXEC sp_executesql @sql, N'@startMonth date', @startMonth = @startMonth;
END

-- 2. Partition scheme: all partitions on PRIMARY for simplicity; assign dedicated filegroups
--    per partition in production for independent backup/compression/archival.
IF NOT EXISTS (SELECT 1 FROM sys.partition_schemes WHERE name = 'ps_Monthly')
BEGIN
    DECLARE @schemeSql nvarchar(max) = N'CREATE PARTITION SCHEME ps_Monthly AS PARTITION pf_MonthlyRange ALL TO ([PRIMARY])';
    EXEC sp_executesql @schemeSql;
END

-- 3. Rebuild each table's clustered index onto the partition scheme. This is the expensive,
--    lock-taking step — run during a maintenance window. ONLINE=ON requires Enterprise edition;
--    drop that option on Standard/Developer (brief blocking rebuild instead).
ALTER TABLE dbo.Messages
    DROP CONSTRAINT [PK_Messages];  -- adjust to the actual PK constraint name in your migration history
ALTER TABLE dbo.Messages
    ADD CONSTRAINT [PK_Messages] PRIMARY KEY CLUSTERED (Id, QueuedAtUtc)
    ON ps_Monthly(QueuedAtUtc);
    -- WITH (ONLINE = ON) -- Enterprise only

ALTER TABLE dbo.DeliveryEvents
    DROP CONSTRAINT [PK_DeliveryEvents];
ALTER TABLE dbo.DeliveryEvents
    ADD CONSTRAINT [PK_DeliveryEvents] PRIMARY KEY CLUSTERED (Id, OccurredAtUtc)
    ON ps_Monthly(OccurredAtUtc);

-- 4. Retention: once partitioned, archive/purge old data by SWITCHing a partition out to a
--    staging table and truncating it — near-instant compared to a row-by-row DELETE. Example
--    for dropping data older than 12 months (run monthly via a Hangfire recurring job):
--
--   ALTER TABLE dbo.Messages SWITCH PARTITION <oldest_partition_number> TO staging.Messages_Archive;
--   TRUNCATE TABLE staging.Messages_Archive;   -- or bcp it out first if archival is required
--   ALTER PARTITION FUNCTION pf_MonthlyRange() MERGE RANGE ('<oldest_boundary>');
--
-- Verify partition distribution after rebuilding:
--   SELECT p.partition_number, p.rows, prv.value AS boundary
--   FROM sys.partitions p
--   JOIN sys.indexes i ON p.object_id = i.object_id AND p.index_id = i.index_id
--   JOIN sys.partition_schemes ps ON i.data_space_id = ps.data_space_id
--   JOIN sys.partition_functions pf ON ps.function_id = pf.function_id
--   LEFT JOIN sys.partition_range_values prv ON pf.function_id = prv.function_id AND p.partition_number = prv.boundary_id + 1
--   WHERE OBJECT_NAME(p.object_id) = 'Messages' AND i.index_id = 1
--   ORDER BY p.partition_number;
