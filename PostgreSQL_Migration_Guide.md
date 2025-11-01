# Amazon Aurora PostgreSQL Migration Guide
## Document Processor System - Database Migration Plan

**Target Database:** Amazon Aurora PostgreSQL
**Current Database:** SQL Server 2022
**Date:** October 17, 2025
**Status:** Planning Document - NO CODE CHANGES MADE

---

## Table of Contents
1. [NuGet Package Changes](#1-nuget-package-changes)
2. [Connection String Changes](#2-connection-string-changes)
3. [Code Changes Required](#3-code-changes-required)
4. [Migration Script Changes](#4-migration-script-changes)
5. [Stored Procedures](#5-stored-procedures)
6. [Data Type Mappings](#6-data-type-mappings)
7. [Configuration Changes](#7-configuration-changes)
8. [Testing Requirements](#8-testing-requirements)

---

## 1. NuGet Package Changes

### A. Remove SQL Server Packages

**File:** `src/DocumentProcessor.Infrastructure/DocumentProcessor.Infrastructure.csproj`

**Packages to Remove:**
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.10" />
```

### B. Add PostgreSQL Packages

**File:** `src/DocumentProcessor.Infrastructure/DocumentProcessor.Infrastructure.csproj`

**Packages to Add:**
```xml
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.10" />
<PackageReference Include="Npgsql" Version="8.0.5" />
```

**Note:** Version numbers should match your current EF Core version (8.0.10)

---

## 2. Connection String Changes

### A. appsettings.json

**File:** `src/DocumentProcessor.Web/appsettings.json`

**Current SQL Server Connection String:**
```json
"ConnectionStrings": {
    "LocalSqlServer": "Server=(localdb)\\MSSQLLocalDB;Database=DocumentProcessorDB_Clean;Integrated Security=true;MultipleActiveResultSets=true;Encrypt=false",
    "DefaultConnection": "Server=dps.crxydkntj70g.us-east-1.rds.amazonaws.com,1433;Database=DPS;User Id=admin;Password=I8Gl5?k3|7cBUsNa|EFFB4s_(>NJ;TrustServerCertificate=true;MultipleActiveResultSets=true"
}
```

**Change To PostgreSQL Connection String:**
```json
"ConnectionStrings": {
    "DefaultConnection": "Host=your-aurora-cluster.cluster-xxxxx.us-east-1.rds.amazonaws.com;Port=5432;Database=documentprocessordb;Username=postgres;Password=YourSecurePassword;SSL Mode=Require;Trust Server Certificate=true;Pooling=true;Minimum Pool Size=5;Maximum Pool Size=100"
}
```

**PostgreSQL Connection String Parameters Explained:**
- `Host` - Aurora PostgreSQL cluster endpoint
- `Port` - Default PostgreSQL port (5432)
- `Database` - Database name (use lowercase for PostgreSQL convention)
- `Username` - PostgreSQL user (typically 'postgres' for Aurora)
- `Password` - Your secure password
- `SSL Mode=Require` - Forces SSL/TLS for Aurora
- `Trust Server Certificate=true` - For AWS RDS/Aurora certificates
- `Pooling=true` - Connection pooling (recommended)
- `Minimum Pool Size=5` - Minimum connections
- `Maximum Pool Size=100` - Maximum connections

**Note:** Remove `LocalSqlServer` connection string as it's SQL Server specific

---

## 3. Code Changes Required

### A. InfrastructureServiceCollectionExtensions.cs

**File:** `src/DocumentProcessor.Infrastructure/InfrastructureServiceCollectionExtensions.cs`

**Line 24 - Current Code:**
```csharp
services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));
```

**Change To:**
```csharp
services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));
```

**Using Statement Required at Top of File:**
```csharp
using Npgsql.EntityFrameworkCore.PostgreSQL;
```

**Remove This Line (Line 25):**
```csharp
//services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(configuration.GetConnectionString("LocalSqlServer")));
```

---

### B. ApplicationDbContext.cs

**File:** `src/DocumentProcessor.Infrastructure/Data/ApplicationDbContext.cs`

**NO CHANGES REQUIRED TO MAIN CODE**

However, be aware of these PostgreSQL-specific behaviors:

**1. String Columns:**
- Current: `nvarchar(255)` → PostgreSQL: `character varying(255)` (automatic)
- Current: `nvarchar(max)` → PostgreSQL: `text` (automatic)

**2. Unique Identifiers:**
- Current: `uniqueidentifier` → PostgreSQL: `uuid`
- EF Core handles this automatically with Npgsql provider

**3. DateTime:**
- Current: `datetime2` → PostgreSQL: `timestamp without time zone`
- All your `DateTime.UtcNow` calls will work correctly

**4. Indexes:**
- All your current indexes will work
- PostgreSQL automatically creates btree indexes
- Composite indexes work identically

---

### C. DocumentRepository.cs

**File:** `src/DocumentProcessor.Infrastructure/Repositories/DocumentRepository.cs`

**Lines 34-35, 62-63, 263-264 - SqlQueryRaw Calls**

**Current Code (Line 34-35):**
```csharp
var documentDtos = await _context.Database.SqlQueryRaw<DocumentDto>(
    "EXEC dbo.GetDocumentById @DocumentId = {0}", id).ToListAsync();
```

**Change To (PostgreSQL Function Call):**
```csharp
var documentDtos = await _context.Database.SqlQueryRaw<DocumentDto>(
    "SELECT * FROM get_document_by_id({0})", id).ToListAsync();
```

**Current Code (Line 62-63):**
```csharp
var documentDtos = await _context.Database.SqlQueryRaw<DocumentDto>(
    "EXEC dbo.GetAllDocuments").ToListAsync();
```

**Change To:**
```csharp
var documentDtos = await _context.Database.SqlQueryRaw<DocumentDto>(
    "SELECT * FROM get_all_documents()").ToListAsync();
```

**Current Code (Line 263-264):**
```csharp
var documentDtos = await _context.Database.SqlQueryRaw<DocumentDto>(
    "EXEC dbo.GetPagedDocuments @PageNumber = {0}, @PageSize = {1}", pageNumber, pageSize).ToListAsync();
```

**Change To:**
```csharp
var documentDtos = await _context.Database.SqlQueryRaw<DocumentDto>(
    "SELECT * FROM get_paged_documents({0}, {1})", pageNumber, pageSize).ToListAsync();
```

**Summary:** SQL Server stored procedures (`EXEC dbo.ProcName`) become PostgreSQL functions (`SELECT * FROM function_name()`)

---

## 4. Migration Script Changes

### A. Delete All Existing Migrations

**Action:** Delete the entire Migrations folder

**Location:** `src/DocumentProcessor.Infrastructure/Migrations/`

**Reason:** SQL Server migrations won't work with PostgreSQL. You'll regenerate migrations after configuring Npgsql.

---

### B. Regenerate InitialCreate Migration

**After making all code changes above, run:**

```bash
dotnet ef migrations add InitialCreate --project src/DocumentProcessor.Infrastructure --startup-project src/DocumentProcessor.Web
```

**This will generate PostgreSQL-specific migration code:**
- `CREATE TABLE` statements with PostgreSQL data types
- `uuid` instead of `uniqueidentifier`
- `timestamp` instead of `datetime2`
- `character varying` instead of `nvarchar`
- `text` instead of `nvarchar(max)`
- `double precision` instead of `float`
- `bigint` for file sizes
- `boolean` instead of `bit`

---

### C. Migration Differences to Expect

**SQL Server Migration (Current):**
```csharp
Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
FileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
FileSize = table.Column<long>(type: "bigint", nullable: false),
IsDeleted = table.Column<bool>(type: "bit", nullable: false),
ConfidenceScore = table.Column<double>(type: "float", nullable: false),
```

**PostgreSQL Migration (Expected):**
```csharp
Id = table.Column<Guid>(type: "uuid", nullable: false),
FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
FileSize = table.Column<long>(type: "bigint", nullable: false),
IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
ConfidenceScore = table.Column<double>(type: "double precision", nullable: false),
```

---

## 5. Stored Procedures

### A. Remove Stored Procedure Initializer Call

**File:** `src/DocumentProcessor.Web/Program.cs`

**Lines 69-75 - Current Code:**
```csharp
// Initialize stored procedures
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await DocumentProcessor.Infrastructure.Data.StoredProcedureInitializer.InitializeStoredProceduresAsync(context, logger);
}
```

**Change To:**
```csharp
// Initialize PostgreSQL functions
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await DocumentProcessor.Infrastructure.Data.PostgreSQLFunctionInitializer.InitializeFunctionsAsync(context, logger);
}
```

---

### B. Create PostgreSQL Function Replacements

**New File:** `src/DocumentProcessor.Infrastructure/Data/PostgreSQLFunctionInitializer.cs`

**Content:**
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace DocumentProcessor.Infrastructure.Data
{
    public static class PostgreSQLFunctionInitializer
    {
        public static async Task InitializeFunctionsAsync(ApplicationDbContext context, ILogger logger)
        {
            logger.LogInformation("Creating PostgreSQL functions...");

            try
            {
                // 1. GetDocumentById Function
                await context.Database.ExecuteSqlRawAsync(@"
                    CREATE OR REPLACE FUNCTION get_document_by_id(doc_id uuid)
                    RETURNS TABLE (
                        ""Id"" uuid,
                        ""FileName"" character varying(500),
                        ""OriginalFileName"" character varying(500),
                        ""FileExtension"" character varying(50),
                        ""FileSize"" bigint,
                        ""ContentType"" character varying(100),
                        ""StoragePath"" character varying(1000),
                        ""S3Key"" character varying(500),
                        ""S3Bucket"" character varying(255),
                        ""Source"" integer,
                        ""Status"" integer,
                        ""DocumentTypeId"" uuid,
                        ""ExtractedText"" text,
                        ""Summary"" text,
                        ""UploadedAt"" timestamp without time zone,
                        ""ProcessedAt"" timestamp without time zone,
                        ""UploadedBy"" character varying(255),
                        ""CreatedAt"" timestamp without time zone,
                        ""UpdatedAt"" timestamp without time zone,
                        ""IsDeleted"" boolean,
                        ""DeletedAt"" timestamp without time zone
                    ) AS $$
                    BEGIN
                        RETURN QUERY
                        SELECT
                            d.""Id"",
                            d.""FileName"",
                            d.""OriginalFileName"",
                            d.""FileExtension"",
                            d.""FileSize"",
                            d.""ContentType"",
                            d.""StoragePath"",
                            d.""S3Key"",
                            d.""S3Bucket"",
                            d.""Source"",
                            d.""Status"",
                            d.""DocumentTypeId"",
                            d.""ExtractedText"",
                            d.""Summary"",
                            d.""UploadedAt"",
                            d.""ProcessedAt"",
                            d.""UploadedBy"",
                            d.""CreatedAt"",
                            d.""UpdatedAt"",
                            d.""IsDeleted"",
                            d.""DeletedAt""
                        FROM ""Documents"" d
                        WHERE d.""Id"" = doc_id
                            AND d.""IsDeleted"" = false;
                    END;
                    $$ LANGUAGE plpgsql;
                ");

                // 2. GetAllDocuments Function
                await context.Database.ExecuteSqlRawAsync(@"
                    CREATE OR REPLACE FUNCTION get_all_documents()
                    RETURNS TABLE (
                        ""Id"" uuid,
                        ""FileName"" character varying(500),
                        ""OriginalFileName"" character varying(500),
                        ""FileExtension"" character varying(50),
                        ""FileSize"" bigint,
                        ""ContentType"" character varying(100),
                        ""StoragePath"" character varying(1000),
                        ""S3Key"" character varying(500),
                        ""S3Bucket"" character varying(255),
                        ""Source"" integer,
                        ""Status"" integer,
                        ""DocumentTypeId"" uuid,
                        ""ExtractedText"" text,
                        ""Summary"" text,
                        ""UploadedAt"" timestamp without time zone,
                        ""ProcessedAt"" timestamp without time zone,
                        ""UploadedBy"" character varying(255),
                        ""CreatedAt"" timestamp without time zone,
                        ""UpdatedAt"" timestamp without time zone,
                        ""IsDeleted"" boolean,
                        ""DeletedAt"" timestamp without time zone
                    ) AS $$
                    BEGIN
                        RETURN QUERY
                        SELECT
                            d.""Id"",
                            d.""FileName"",
                            d.""OriginalFileName"",
                            d.""FileExtension"",
                            d.""FileSize"",
                            d.""ContentType"",
                            d.""StoragePath"",
                            d.""S3Key"",
                            d.""S3Bucket"",
                            d.""Source"",
                            d.""Status"",
                            d.""DocumentTypeId"",
                            d.""ExtractedText"",
                            d.""Summary"",
                            d.""UploadedAt"",
                            d.""ProcessedAt"",
                            d.""UploadedBy"",
                            d.""CreatedAt"",
                            d.""UpdatedAt"",
                            d.""IsDeleted"",
                            d.""DeletedAt""
                        FROM ""Documents"" d
                        WHERE d.""IsDeleted"" = false
                        ORDER BY d.""UploadedAt"" DESC;
                    END;
                    $$ LANGUAGE plpgsql;
                ");

                // 3. GetPagedDocuments Function
                await context.Database.ExecuteSqlRawAsync(@"
                    CREATE OR REPLACE FUNCTION get_paged_documents(page_number integer, page_size integer)
                    RETURNS TABLE (
                        ""Id"" uuid,
                        ""FileName"" character varying(500),
                        ""OriginalFileName"" character varying(500),
                        ""FileExtension"" character varying(50),
                        ""FileSize"" bigint,
                        ""ContentType"" character varying(100),
                        ""StoragePath"" character varying(1000),
                        ""S3Key"" character varying(500),
                        ""S3Bucket"" character varying(255),
                        ""Source"" integer,
                        ""Status"" integer,
                        ""DocumentTypeId"" uuid,
                        ""ExtractedText"" text,
                        ""Summary"" text,
                        ""UploadedAt"" timestamp without time zone,
                        ""ProcessedAt"" timestamp without time zone,
                        ""UploadedBy"" character varying(255),
                        ""CreatedAt"" timestamp without time zone,
                        ""UpdatedAt"" timestamp without time zone,
                        ""IsDeleted"" boolean,
                        ""DeletedAt"" timestamp without time zone
                    ) AS $$
                    DECLARE
                        offset_value integer;
                    BEGIN
                        -- Calculate offset for pagination
                        offset_value := (page_number - 1) * page_size;

                        RETURN QUERY
                        SELECT
                            d.""Id"",
                            d.""FileName"",
                            d.""OriginalFileName"",
                            d.""FileExtension"",
                            d.""FileSize"",
                            d.""ContentType"",
                            d.""StoragePath"",
                            d.""S3Key"",
                            d.""S3Bucket"",
                            d.""Source"",
                            d.""Status"",
                            d.""DocumentTypeId"",
                            d.""ExtractedText"",
                            d.""Summary"",
                            d.""UploadedAt"",
                            d.""ProcessedAt"",
                            d.""UploadedBy"",
                            d.""CreatedAt"",
                            d.""UpdatedAt"",
                            d.""IsDeleted"",
                            d.""DeletedAt""
                        FROM ""Documents"" d
                        WHERE d.""IsDeleted"" = false
                        ORDER BY d.""UploadedAt"" DESC
                        LIMIT page_size
                        OFFSET offset_value;
                    END;
                    $$ LANGUAGE plpgsql;
                ");

                logger.LogInformation("PostgreSQL functions created successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating PostgreSQL functions");
                throw;
            }
        }
    }
}
```

**Key Differences from SQL Server:**
- Functions instead of stored procedures
- `RETURNS TABLE` instead of output parameters
- `$$ $$ LANGUAGE plpgsql` syntax
- `LIMIT/OFFSET` instead of SQL Server `TOP/OFFSET FETCH`
- All identifiers are quoted with double quotes `"ColumnName"` for case sensitivity

---

### C. Delete SQL Server Stored Procedure Files

**Files to Delete:**
- `src/DocumentProcessor.Infrastructure/Data/StoredProcedureInitializer.cs`
- `src/DocumentProcessor.Infrastructure/Data/StoredProcedureMigration.cs`

**Reason:** These contain SQL Server-specific T-SQL syntax that won't work with PostgreSQL

---

## 6. Data Type Mappings

### Complete SQL Server to PostgreSQL Data Type Mapping

| SQL Server Type | PostgreSQL Type | EF Core Mapping | Notes |
|----------------|-----------------|-----------------|-------|
| `uniqueidentifier` | `uuid` | Automatic | Npgsql handles Guid ↔ UUID |
| `nvarchar(n)` | `character varying(n)` | Automatic | Unicode support in both |
| `nvarchar(max)` | `text` | Automatic | Unlimited length text |
| `varchar(n)` | `character varying(n)` | Automatic | Same as nvarchar |
| `bit` | `boolean` | Automatic | true/false |
| `int` | `integer` | Automatic | 32-bit integer |
| `bigint` | `bigint` | Automatic | 64-bit integer |
| `float` | `double precision` | Automatic | 64-bit floating point |
| `real` | `real` | Automatic | 32-bit floating point |
| `datetime2` | `timestamp without time zone` | Automatic | UTC timestamps work correctly |
| `datetime2(7)` | `timestamp(6)` | Automatic | PostgreSQL max precision is 6 |
| `decimal(p,s)` | `numeric(p,s)` | Automatic | Exact numeric |
| `varbinary(max)` | `bytea` | Automatic | Binary data |

### Entity Properties Mapping

Your current entity properties will map automatically:

```csharp
// Document Entity
public Guid Id { get; set; }                          // uuid
public string FileName { get; set; }                  // character varying(500)
public string OriginalFileName { get; set; }          // character varying(500)
public string FileExtension { get; set; }             // character varying(50)
public long FileSize { get; set; }                    // bigint
public string ContentType { get; set; }               // character varying(100)
public string StoragePath { get; set; }               // character varying(1000)
public string? S3Key { get; set; }                    // character varying(500) nullable
public string? S3Bucket { get; set; }                 // character varying(255) nullable
public int Source { get; set; }                       // integer (enum)
public int Status { get; set; }                       // integer (enum)
public Guid? DocumentTypeId { get; set; }             // uuid nullable
public string? ExtractedText { get; set; }            // text nullable
public string? Summary { get; set; }                  // text nullable
public DateTime UploadedAt { get; set; }              // timestamp without time zone
public DateTime? ProcessedAt { get; set; }            // timestamp without time zone nullable
public string UploadedBy { get; set; }                // character varying(255)
public DateTime CreatedAt { get; set; }               // timestamp without time zone
public DateTime UpdatedAt { get; set; }               // timestamp without time zone
public bool IsDeleted { get; set; }                   // boolean
public DateTime? DeletedAt { get; set; }              // timestamp without time zone nullable

// Classification Entity
public double ConfidenceScore { get; set; }           // double precision
public int Method { get; set; }                       // integer (enum)
public bool IsManuallyVerified { get; set; }          // boolean

// ProcessingQueue Entity
public int ProcessingType { get; set; }               // integer (enum)
public int Priority { get; set; }                     // integer
public int RetryCount { get; set; }                   // integer
public int MaxRetries { get; set; }                   // integer

// DocumentMetadata Entity
public int? PageCount { get; set; }                   // integer nullable
public int? WordCount { get; set; }                   // integer nullable
```

**All mappings are automatic - NO CODE CHANGES NEEDED in entity classes**

---

## 7. Configuration Changes

### A. Amazon Aurora PostgreSQL Specifics

**Aurora PostgreSQL Connection String with IAM Authentication (Optional):**
```json
"ConnectionStrings": {
    "DefaultConnection": "Host=your-cluster.cluster-xxxxx.us-east-1.rds.amazonaws.com;Port=5432;Database=documentprocessordb;Username=iamuser;Password=;SSL Mode=Require;Trust Server Certificate=true;Pooling=true;Minimum Pool Size=5;Maximum Pool Size=100"
}
```

**With IAM, you generate authentication tokens via AWS SDK instead of static passwords.**

---

### B. appsettings.Development.json

**File:** `src/DocumentProcessor.Web/appsettings.Development.json`

**Add for local PostgreSQL development:**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=documentprocessordb_dev;Username=postgres;Password=postgres;Pooling=true"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

**Note:** `Microsoft.EntityFrameworkCore.Database.Command` logging will show PostgreSQL queries in console

---

### C. Docker Compose for Local PostgreSQL

**New File:** `docker-compose.yml` (in project root)

**Content:**
```yaml
version: '3.8'

services:
  postgres:
    image: postgres:16-alpine
    container_name: dps-postgres
    environment:
      POSTGRES_DB: documentprocessordb_dev
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 10s
      timeout: 5s
      retries: 5

volumes:
  postgres_data:
```

**Start local PostgreSQL:**
```bash
docker-compose up -d
```

**Note:** This replaces your current SQL Server Docker container

---

### D. .csproj File Changes Summary

**File:** `src/DocumentProcessor.Infrastructure/DocumentProcessor.Infrastructure.csproj`

**Complete Package Reference Section After Changes:**
```xml
<ItemGroup>
    <PackageReference Include="Amazon.Bedrock" Version="3.7.400.55" />
    <PackageReference Include="Amazon.BedrockRuntime" Version="3.7.400.55" />
    <PackageReference Include="Amazon.Extensions.Configuration.SystemsManager" Version="7.1.0" />
    <PackageReference Include="Amazon.Lambda.Core" Version="2.3.0" />
    <PackageReference Include="Amazon.Lambda.Serialization.SystemTextJson" Version="2.4.3" />
    <PackageReference Include="AWSSDK.Core" Version="3.7.400.55" />
    <PackageReference Include="AWSSDK.S3" Version="3.7.403.10" />
    <PackageReference Include="AWSSDK.TranscribeService" Version="3.7.402.8" />
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.10" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.10">
        <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
        <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="8.0.10">
        <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
        <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore" Version="8.0.10" />

    <!-- REMOVED: <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.10" /> -->

    <!-- ADDED: -->
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.10" />
    <PackageReference Include="Npgsql" Version="8.0.5" />

    <PackageReference Include="UglyToad.PdfPig" Version="0.1.9" />
</ItemGroup>
```

---

## 8. Testing Requirements

### A. Migration Verification Checklist

After making all changes, verify:

1. **Build Success**
   ```bash
   dotnet build
   ```
   - No compilation errors
   - Npgsql packages restored correctly

2. **Generate New Migration**
   ```bash
   dotnet ef migrations add InitialCreate --project src/DocumentProcessor.Infrastructure --startup-project src/DocumentProcessor.Web
   ```
   - Migration generates successfully
   - Check migration file uses PostgreSQL types (uuid, timestamp, etc.)

3. **Apply Migration to Local PostgreSQL**
   ```bash
   dotnet ef database update --project src/DocumentProcessor.Infrastructure --startup-project src/DocumentProcessor.Web
   ```
   - Database created successfully
   - All 5 tables created: Documents, DocumentTypes, Classifications, DocumentMetadata, ProcessingQueues
   - Seed data inserted (5 document types)
   - All indexes created
   - Foreign keys configured correctly

4. **Run Application Locally**
   ```bash
   dotnet run --project src/DocumentProcessor.Web
   ```
   - Application starts without errors
   - Health checks pass (`/health` endpoint)
   - Database connection successful
   - PostgreSQL functions created successfully

5. **Test Basic Operations**
   - Upload a document → Verify saves to Documents table
   - View documents list → Verify query works
   - Check background tasks → Verify ProcessingQueue table interaction
   - Delete a document → Verify cascade delete works
   - Check logs → Verify no EF/database errors

6. **Test Stored Procedure Replacements**
   - DocumentRepository.GetByIdAsync() → Uses `get_document_by_id()` function
   - DocumentRepository.GetAllAsync() → Uses `get_all_documents()` function
   - DocumentRepository.GetPagedAsync() → Uses `get_paged_documents()` function
   - Verify all return correct data

---

### B. Performance Testing Checklist

1. **Connection Pooling**
   - Monitor connection count during load
   - Verify pool sizes are appropriate (min: 5, max: 100)
   - Check for connection leaks

2. **Query Performance**
   - Enable EF query logging
   - Compare query execution times SQL Server vs PostgreSQL
   - Check for missing indexes (pgAdmin EXPLAIN ANALYZE)

3. **Concurrent Operations**
   - Test multiple simultaneous document uploads
   - Verify background processing queue handles concurrency
   - Check for deadlocks or locking issues

4. **Large Document Handling**
   - Upload documents with large ExtractedText/Summary fields
   - Verify TOAST storage handles large text fields efficiently
   - Test documents > 1MB file size metadata

---

### C. Aurora-Specific Testing

1. **SSL/TLS Connection**
   - Verify `SSL Mode=Require` works
   - Test connection with Aurora's SSL certificate
   - Check `Trust Server Certificate=true` behavior

2. **Aurora Features**
   - Test read replica endpoints (if using)
   - Verify auto-scaling if configured
   - Test failover behavior (optional - staging environment)

3. **AWS IAM Authentication** (If implementing)
   - Test token generation
   - Verify token refresh before expiration
   - Check IAM policy permissions

---

### D. Database Comparison Queries

**After Migration, Verify Data Integrity:**

```sql
-- PostgreSQL queries to verify migration

-- 1. Check all tables exist
SELECT table_name
FROM information_schema.tables
WHERE table_schema = 'public'
    AND table_type = 'BASE TABLE'
ORDER BY table_name;

-- Expected: Classifications, Documents, DocumentMetadata, DocumentTypes, ProcessingQueues, __EFMigrationsHistory

-- 2. Check seed data loaded
SELECT "Id", "Name", "Category", "IsActive"
FROM "DocumentTypes"
ORDER BY "Name";

-- Expected: 5 rows (Contract, Email, Invoice, Report, Resume)

-- 3. Check all indexes
SELECT
    tablename,
    indexname,
    indexdef
FROM pg_indexes
WHERE schemaname = 'public'
    AND tablename NOT LIKE '%Migration%'
ORDER BY tablename, indexname;

-- Expected: All indexes from ApplicationDbContext

-- 4. Check foreign keys
SELECT
    tc.table_name,
    tc.constraint_name,
    tc.constraint_type,
    kcu.column_name,
    ccu.table_name AS foreign_table_name,
    ccu.column_name AS foreign_column_name
FROM information_schema.table_constraints AS tc
JOIN information_schema.key_column_usage AS kcu
    ON tc.constraint_name = kcu.constraint_name
    AND tc.table_schema = kcu.table_schema
JOIN information_schema.constraint_column_usage AS ccu
    ON ccu.constraint_name = tc.constraint_name
    AND ccu.table_schema = tc.table_schema
WHERE tc.constraint_type = 'FOREIGN KEY'
ORDER BY tc.table_name;

-- Expected: 5 foreign key relationships

-- 5. Check functions exist
SELECT routine_name, routine_type
FROM information_schema.routines
WHERE routine_schema = 'public'
    AND routine_type = 'FUNCTION'
ORDER BY routine_name;

-- Expected: get_all_documents, get_document_by_id, get_paged_documents

-- 6. Test a function
SELECT * FROM get_all_documents();

-- Should return all non-deleted documents
```

---

## 9. Migration Steps Order

**Perform changes in this exact order:**

### Step 1: Backup Current Database
- Export SQL Server database
- Save to secure location
- Verify backup is restorable

### Step 2: Update NuGet Packages
1. Remove `Microsoft.EntityFrameworkCore.SqlServer`
2. Add `Npgsql.EntityFrameworkCore.PostgreSQL`
3. Add `Npgsql`
4. Run `dotnet restore`

### Step 3: Update Connection Strings
1. Update `appsettings.json`
2. Add `appsettings.Development.json` for local development
3. Remove SQL Server connection strings

### Step 4: Update Code Files
1. `InfrastructureServiceCollectionExtensions.cs` - Change to `UseNpgsql()`
2. `DocumentRepository.cs` - Update stored procedure calls to function calls
3. `Program.cs` - Update stored procedure initializer call

### Step 5: Create PostgreSQL Functions
1. Create `PostgreSQLFunctionInitializer.cs`
2. Add 3 PostgreSQL functions (get_document_by_id, get_all_documents, get_paged_documents)

### Step 6: Delete Old Files
1. Delete SQL Server stored procedure files
2. Delete entire Migrations folder

### Step 7: Generate New Migrations
1. Run `dotnet ef migrations add InitialCreate`
2. Verify migration file looks correct (PostgreSQL types)

### Step 8: Setup Local PostgreSQL
1. Start PostgreSQL Docker container or install locally
2. Create database `documentprocessordb_dev`

### Step 9: Apply Migrations Locally
1. Run `dotnet ef database update`
2. Verify tables created
3. Verify seed data inserted
4. Verify functions created

### Step 10: Test Locally
1. Run application
2. Test all CRUD operations
3. Verify background processing
4. Check logs for errors

### Step 11: Deploy to Aurora PostgreSQL
1. Create Aurora PostgreSQL cluster in AWS
2. Configure security groups
3. Update production connection string
4. Run migrations on Aurora: `dotnet ef database update`
5. Deploy application

### Step 12: Verify Production
1. Health check endpoints
2. Test document upload
3. Monitor logs
4. Verify performance

---

## 10. Rollback Plan

If migration fails, rollback procedure:

### Immediate Rollback (Code)
1. Revert Git commits
2. Restore SQL Server NuGet packages
3. Restore SQL Server connection strings
4. Redeploy previous version

### Database Rollback
1. Drop PostgreSQL database
2. Restore SQL Server from backup
3. Point application back to SQL Server

### Partial Rollback (Dual Database)
**Option:** Run both databases simultaneously during transition
- Point read operations to PostgreSQL
- Keep SQL Server as backup
- Gradually transition write operations

---

## 11. Additional Notes

### PostgreSQL Naming Conventions
- PostgreSQL is case-sensitive with identifiers
- EF Core Npgsql provider quotes all identifiers: `"TableName"`, `"ColumnName"`
- This matches C# property names exactly
- Schema name is `public` by default (vs `dbo` in SQL Server)

### Connection String Security
- **NEVER commit connection strings with passwords to Git**
- Use AWS Secrets Manager or Azure Key Vault in production
- Use environment variables for local development
- Consider AWS IAM authentication for Aurora

### Performance Differences
- PostgreSQL MVCC (Multi-Version Concurrency Control) differs from SQL Server locking
- VACUUM operations needed for PostgreSQL (Aurora handles automatically)
- ANALYZE statistics updated differently
- Monitor with pgAdmin or CloudWatch (for Aurora)

### Compatibility Notes
- All your current LINQ queries will work unchanged
- EF Core translates to appropriate PostgreSQL SQL
- DateTime handling is identical (UTC recommended)
- Guid/UUID mapping is automatic
- Enums map to integers in both databases

---

## 12. Cost Considerations

### Aurora PostgreSQL Pricing
- **Serverless v2:** Pay per ACU (Aurora Capacity Unit) per second
  - Recommended for variable workloads
  - Auto-scales 0.5 ACU to 128 ACU

- **Provisioned:** Pay per instance hour
  - Recommended for steady workloads
  - Choose instance size (db.r6g.large, etc.)

- **Storage:** $0.10 per GB-month (same for both Serverless and Provisioned)
- **I/O:** $0.20 per 1 million requests
- **Backup Storage:** Free up to 100% of database size

### Cost Optimization Tips
1. Use Serverless v2 for development/staging
2. Enable query caching
3. Optimize indexes to reduce I/O
4. Use connection pooling (already configured)
5. Monitor CloudWatch metrics for right-sizing

---

## Summary of All File Changes

### Files to Modify:
1. ✏️ `src/DocumentProcessor.Infrastructure/DocumentProcessor.Infrastructure.csproj` - Update NuGet packages
2. ✏️ `src/DocumentProcessor.Web/appsettings.json` - Update connection string
3. ✏️ `src/DocumentProcessor.Infrastructure/InfrastructureServiceCollectionExtensions.cs` - Change to UseNpgsql
4. ✏️ `src/DocumentProcessor.Infrastructure/Repositories/DocumentRepository.cs` - Update stored procedure calls
5. ✏️ `src/DocumentProcessor.Web/Program.cs` - Update stored procedure initializer

### Files to Create:
1. ➕ `src/DocumentProcessor.Infrastructure/Data/PostgreSQLFunctionInitializer.cs` - PostgreSQL functions
2. ➕ `src/DocumentProcessor.Web/appsettings.Development.json` - Local PostgreSQL settings
3. ➕ `docker-compose.yml` - Local PostgreSQL Docker container

### Files/Folders to Delete:
1. ❌ `src/DocumentProcessor.Infrastructure/Migrations/` - Entire folder (will regenerate)
2. ❌ `src/DocumentProcessor.Infrastructure/Data/StoredProcedureInitializer.cs` - SQL Server specific
3. ❌ `src/DocumentProcessor.Infrastructure/Data/StoredProcedureMigration.cs` - SQL Server specific

### Files Requiring NO Changes:
- ✅ All entity classes in `src/DocumentProcessor.Core/Entities/`
- ✅ `src/DocumentProcessor.Infrastructure/Data/ApplicationDbContext.cs` (EF Core handles differences)
- ✅ All service classes in `src/DocumentProcessor.Application/`
- ✅ All Razor components in `src/DocumentProcessor.Web/Components/`
- ✅ All repository interfaces in `src/DocumentProcessor.Core/Interfaces/`

---

## Questions & Answers

**Q: Will my existing data migrate automatically?**
A: No. This guide covers schema migration. For data migration, you'll need to export from SQL Server and import to PostgreSQL using tools like AWS DMS (Database Migration Service) or pg_loader.

**Q: Can I run SQL Server and PostgreSQL side-by-side?**
A: Yes. Create a separate connection string and DbContext instance. Useful for gradual migration or A/B testing.

**Q: What about Entity Framework migrations history?**
A: The `__EFMigrationsHistory` table will be recreated in PostgreSQL. Previous SQL Server migration history won't transfer.

**Q: Are there any breaking changes for users?**
A: No. The API and UI remain identical. Database change is transparent to end users.

**Q: What about performance?**
A: Generally comparable. PostgreSQL may be faster for complex queries, read-heavy workloads. SQL Server may be faster for write-heavy workloads. Test with your specific workload.

**Q: Do I need to change my LINQ queries?**
A: No. EF Core abstracts the database. LINQ queries work identically.

**Q: What about stored procedures?**
A: PostgreSQL uses functions instead. This guide includes replacements for all 3 stored procedures (GetDocumentById, GetAllDocuments, GetPagedDocuments).

**Q: Is Amazon Aurora PostgreSQL compatible with standard PostgreSQL?**
A: Yes. Aurora PostgreSQL is PostgreSQL-compatible. Code works with both.

---

## Document Control

**Version:** 1.0
**Last Updated:** October 17, 2025
**Author:** Database Migration Planning Team
**Review Status:** Ready for Implementation
**Approval Required:** Yes - Senior Developer & DevOps Lead

---

## Next Steps

1. **Review this document** with the development team
2. **Get approval** from stakeholders
3. **Schedule migration window** (recommend non-business hours)
4. **Create test environment** with Aurora PostgreSQL
5. **Test migration steps** 1-12 in test environment
6. **Document any issues** encountered during testing
7. **Update this guide** based on test results
8. **Schedule production migration**
9. **Execute migration** following Step 1-12
10. **Monitor production** for 48 hours post-migration

---

**END OF DOCUMENT**
