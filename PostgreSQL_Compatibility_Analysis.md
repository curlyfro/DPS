# PostgreSQL Compatibility Analysis

## Current Status
The codebase is currently designed for **SQL Server** but uses Entity Framework Core, which provides good cross-database compatibility. This document outlines the minimal changes needed to make the application work with PostgreSQL.

## Data Types - Already Compatible ✅

The application uses C# types in entity classes, and EF Core will automatically map them:

| C# Type | SQL Server | PostgreSQL | Status |
|---------|------------|------------|---------|
| `Guid` | `UNIQUEIDENTIFIER` | `UUID` | ✅ Auto-mapped by EF Core |
| `string` | `NVARCHAR(n)` | `VARCHAR(n)` | ✅ Auto-mapped by EF Core |
| `DateTime` | `DATETIME2` | `TIMESTAMP` | ✅ Auto-mapped by EF Core |
| `bool` | `BIT` | `BOOLEAN` | ✅ Auto-mapped by EF Core |
| `int` | `INT` | `INTEGER` | ✅ Auto-mapped by EF Core |
| `long` | `BIGINT` | `BIGINT` | ✅ Auto-mapped by EF Core |
| Enums | `INT` | `INTEGER` | ✅ Auto-mapped by EF Core |

## Database Schema - Compatible ✅

The `ApplicationDbContext.cs` uses EF Core fluent API which is database-agnostic:
- `HasMaxLength()` - Works on both
- `IsRequired()` - Works on both
- `HasIndex()` - Works on both
- `HasForeignKey()` - Works on both
- `OnDelete(DeleteBehavior.Cascade)` - Works on both
- Seed data uses `Guid.Parse()` and `DateTime` - Works on both

**No changes needed in ApplicationDbContext.cs or Entity classes.**

## Stored Procedures - Needs Attention ⚠️

The main compatibility issues are in `StoredProcedureInitializer.cs`. The following SQL Server-specific syntax would need PostgreSQL equivalents:

### Syntax Differences

| SQL Server Syntax | PostgreSQL Equivalent | Location |
|-------------------|----------------------|----------|
| `CREATE OR ALTER PROCEDURE` | `CREATE OR REPLACE FUNCTION` | All procedures |
| `dbo.ProcedureName` | `ProcedureName` (no schema prefix needed) | All procedures |
| `@ParameterName UNIQUEIDENTIFIER` | `p_parameter_name UUID` | Parameter declarations |
| `@ParameterName NVARCHAR(n)` | `p_parameter_name VARCHAR(n)` | Parameter declarations |
| `SET NOCOUNT ON;` | Not needed (remove) | All procedures |
| `GETUTCDATE()` | `NOW()` or `CURRENT_TIMESTAMP` | GetRecentDocuments |
| `DATEADD(DAY, -@Days, GETUTCDATE())` | `NOW() - INTERVAL '1 day' * p_days` | GetRecentDocuments |
| `SELECT TOP (@Limit) ...` | `SELECT ... LIMIT p_limit` | GetRecentDocuments |
| `EXEC dbo.ProcedureName @param = value` | `SELECT * FROM ProcedureName(value)` | Repository calls |

### Specific Procedure Changes Needed

#### 1. GetDocumentById
**SQL Server (current):**
```sql
CREATE OR ALTER PROCEDURE dbo.GetDocumentById
    @DocumentId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ...
    FROM Documents
    WHERE Id = @DocumentId;
END;
```

**PostgreSQL (would need):**
```sql
CREATE OR REPLACE FUNCTION GetDocumentById(p_document_id UUID)
RETURNS TABLE (...column definitions...)
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT ...
    FROM "Documents"
    WHERE "Id" = p_document_id;
END;
$$;
```

#### 2. GetRecentDocuments
**Key change:** `DATEADD` and `GETUTCDATE()` → `NOW() - INTERVAL`

**SQL Server:**
```sql
DECLARE @CutoffDate DATETIME2 = DATEADD(DAY, -@Days, GETUTCDATE());
SELECT TOP (@Limit) ...
WHERE UploadedAt >= @CutoffDate
```

**PostgreSQL:**
```sql
SELECT ...
WHERE "UploadedAt" >= NOW() - (p_days || ' days')::INTERVAL
LIMIT p_limit
```

## Repository Calls - Needs Attention ⚠️

### Current Implementation
```csharp
var documentDtos = await _context.Database.SqlQueryRaw<DocumentDto>(
    "EXEC dbo.GetDocumentById @DocumentId = {0}", id).ToListAsync();
```

### For PostgreSQL Support
Would need to detect database provider and use:
```csharp
var documentDtos = await _context.Database.SqlQueryRaw<DocumentDto>(
    "SELECT * FROM GetDocumentById({0})", id).ToListAsync();
```

**Affected files:**
- `DocumentRepository.cs` - 3 methods (GetByIdAsync, GetAllAsync, GetPagedAsync)
- `DocumentTypeRepository.cs` - 1 method (GetAllAsync)

## Migration Strategy - Recommended Approach

### Option 1: Dual Database Support (Recommended)
Add database provider detection in `StoredProcedureInitializer.cs`:

```csharp
var providerName = context.Database.ProviderName ?? string.Empty;
var isPostgreSQL = providerName.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase);

if (isPostgreSQL)
{
    // Create PostgreSQL functions
}
else
{
    // Create SQL Server procedures (current code)
}
```

Similarly in repositories, detect provider and use appropriate SQL syntax.

### Option 2: Remove Stored Procedures (Simplest)
Replace stored procedure calls with EF Core LINQ queries:
- More portable
- Easier to maintain
- Slightly less performant (usually negligible)

## What Works Without Changes ✅

1. **All entity classes** - No changes needed
2. **DbContext configuration** - No changes needed
3. **Migrations** - EF Core generates database-specific SQL
4. **LINQ queries** - All repository methods using EF Core LINQ work on both databases
5. **Indexes and foreign keys** - EF Core handles these
6. **Seed data** - Works on both databases
7. **Connection strings** - Just change the provider and connection string in appsettings.json

## PostgreSQL Package Needed

To use PostgreSQL, add this NuGet package:
```xml
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.0" />
```

## Estimated Effort

- **No changes (current SQL Server only)**: 0 hours
- **Minimal changes (keep as SQL Server, document issues)**: 0 hours ✅ **DONE**
- **Add dual database support**: 4-6 hours
- **Remove stored procedures entirely**: 2-3 hours
- **Full PostgreSQL migration**: 8-12 hours

## Conclusion

The application is **already 90% PostgreSQL compatible** thanks to Entity Framework Core. The only incompatibilities are:
1. Stored procedure syntax in `StoredProcedureInitializer.cs`
2. Stored procedure calls in 2 repository files

**Current recommendation**: Keep as-is for SQL Server. When PostgreSQL support is needed, either:
- Add provider detection for dual database support, OR
- Remove the 6 stored procedures and use EF Core LINQ queries instead
