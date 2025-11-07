using Microsoft.EntityFrameworkCore.Migrations;

namespace DocumentProcessor.Infrastructure.Migrations
{
    /// <summary>
    /// Migration to create stored procedures for document repository operations
    /// </summary>
    public partial class StoredProcedureMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create function: get_document_by_id
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION dps_dbo.get_document_by_id(
                    document_id UUID
                ) RETURNS TABLE (
                    id UUID,
                    filename TEXT,
                    originalfilename TEXT,
                    fileextension TEXT,
                    filesize BIGINT,
                    contenttype TEXT,
                    storagepath TEXT,
                    s3key TEXT,
                    s3bucket TEXT,
                    source TEXT,
                    status INT,
                    documenttypeid UUID,
                    extractedtext TEXT,
                    summary TEXT,
                    uploadedat TIMESTAMP,
                    processedat TIMESTAMP,
                    uploadedby TEXT,
                    createdat TIMESTAMP,
                    updatedat TIMESTAMP,
                    isdeleted BOOLEAN,
                    deletedat TIMESTAMP
                ) AS $$
                BEGIN
                    RETURN QUERY
                    SELECT 
                        d.id,
                        d.filename,
                        d.originalfilename,
                        d.fileextension,
                        d.filesize,
                        d.contenttype,
                        d.storagepath,
                        d.s3key,
                        d.s3bucket,
                        d.source,
                        d.status,
                        d.documenttypeid,
                        d.extractedtext,
                        d.summary,
                        d.uploadedat,
                        d.processedat,
                        d.uploadedby,
                        d.createdat,
                        d.updatedat,
                        d.isdeleted,
                        d.deletedat
                    FROM dps_dbo.documents d
                    WHERE d.id = document_id;
                END;
                $$ LANGUAGE plpgsql;
            ");

            // Create function: get_all_documents
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION dps_dbo.get_all_documents()
                RETURNS TABLE (
                    id UUID,
                    filename TEXT,
                    originalfilename TEXT,
                    fileextension TEXT,
                    filesize BIGINT,
                    contenttype TEXT,
                    storagepath TEXT,
                    s3key TEXT,
                    s3bucket TEXT,
                    source TEXT,
                    status INT,
                    documenttypeid UUID,
                    extractedtext TEXT,
                    summary TEXT,
                    uploadedat TIMESTAMP,
                    processedat TIMESTAMP,
                    uploadedby TEXT,
                    createdat TIMESTAMP,
                    updatedat TIMESTAMP,
                    isdeleted BOOLEAN,
                    deletedat TIMESTAMP
                ) AS $$
                BEGIN
                    RETURN QUERY
                    SELECT 
                        d.id,
                        d.filename,
                        d.originalfilename,
                        d.fileextension,
                        d.filesize,
                        d.contenttype,
                        d.storagepath,
                        d.s3key,
                        d.s3bucket,
                        d.source,
                        d.status,
                        d.documenttypeid,
                        d.extractedtext,
                        d.summary,
                        d.uploadedat,
                        d.processedat,
                        d.uploadedby,
                        d.createdat,
                        d.updatedat,
                        d.isdeleted,
                        d.deletedat
                    FROM dps_dbo.documents d
                    ORDER BY d.uploadedat DESC;
                END;
                $$ LANGUAGE plpgsql;
            ");

            // Create function: get_documents_by_user
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION dps_dbo.get_documents_by_user(
                    user_id TEXT
                ) RETURNS TABLE (
                    id UUID,
                    filename TEXT,
                    originalfilename TEXT,
                    fileextension TEXT,
                    filesize BIGINT,
                    contenttype TEXT,
                    storagepath TEXT,
                    s3key TEXT,
                    s3bucket TEXT,
                    source TEXT,
                    status INT,
                    documenttypeid UUID,
                    extractedtext TEXT,
                    summary TEXT,
                    uploadedat TIMESTAMP,
                    processedat TIMESTAMP,
                    uploadedby TEXT,
                    createdat TIMESTAMP,
                    updatedat TIMESTAMP,
                    isdeleted BOOLEAN,
                    deletedat TIMESTAMP
                ) AS $$
                BEGIN
                    RETURN QUERY
                    SELECT 
                        d.id,
                        d.filename,
                        d.originalfilename,
                        d.fileextension,
                        d.filesize,
                        d.contenttype,
                        d.storagepath,
                        d.s3key,
                        d.s3bucket,
                        d.source,
                        d.status,
                        d.documenttypeid,
                        d.extractedtext,
                        d.summary,
                        d.uploadedat,
                        d.processedat,
                        d.uploadedby,
                        d.createdat,
                        d.updatedat,
                        d.isdeleted,
                        d.deletedat
                    FROM dps_dbo.documents d
                    WHERE d.uploadedby = user_id
                    ORDER BY d.uploadedat DESC;
                END;
                $$ LANGUAGE plpgsql;
            ");

            // Create function: get_recent_documents
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION dps_dbo.get_recent_documents(
                    days INT DEFAULT 7,
                    limit_count INT DEFAULT 100
                ) RETURNS TABLE (
                    id UUID,
                    filename TEXT,
                    filepath TEXT,
                    filesize BIGINT,
                    mimetype TEXT,
                    uploadedat TIMESTAMP,
                    uploadedby TEXT,
                    status INT,
                    documenttypeid UUID,
                    isdeleted BOOLEAN,
                    deletedat TIMESTAMP,
                    createdat TIMESTAMP,
                    updatedat TIMESTAMP
                ) AS $$
                DECLARE
                    cutoff_date TIMESTAMP;
                BEGIN
                    cutoff_date := NOW() - (days * INTERVAL '1 day');
                    
                    RETURN QUERY
                    SELECT 
                        d.id,
                        d.filename,
                        d.storagepath AS filepath,
                        d.filesize,
                        d.contenttype AS mimetype,
                        d.uploadedat,
                        d.uploadedby,
                        d.status,
                        d.documenttypeid,
                        d.isdeleted,
                        d.deletedat,
                        d.createdat,
                        d.updatedat
                    FROM dps_dbo.documents d
                    WHERE d.uploadedat >= cutoff_date
                    ORDER BY d.uploadedat DESC
                    LIMIT limit_count;
                END;
                $$ LANGUAGE plpgsql;
            ");

            // Create function: get_paged_documents
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION dps_dbo.get_paged_documents(
                    page_number INT DEFAULT 1,
                    page_size INT DEFAULT 10
                ) RETURNS TABLE (
                    id UUID,
                    filename TEXT,
                    originalfilename TEXT,
                    fileextension TEXT,
                    filesize BIGINT,
                    contenttype TEXT,
                    storagepath TEXT,
                    s3key TEXT,
                    s3bucket TEXT,
                    source TEXT,
                    status INT,
                    documenttypeid UUID,
                    extractedtext TEXT,
                    summary TEXT,
                    uploadedat TIMESTAMP,
                    processedat TIMESTAMP,
                    uploadedby TEXT,
                    createdat TIMESTAMP,
                    updatedat TIMESTAMP,
                    isdeleted BOOLEAN,
                    deletedat TIMESTAMP
                ) AS $$
                DECLARE
                    offset_val INT;
                BEGIN
                    offset_val := (page_number - 1) * page_size;
                    
                    RETURN QUERY
                    SELECT 
                        d.id,
                        d.filename,
                        d.originalfilename,
                        d.fileextension,
                        d.filesize,
                        d.contenttype,
                        d.storagepath,
                        d.s3key,
                        d.s3bucket,
                        d.source,
                        d.status,
                        d.documenttypeid,
                        d.extractedtext,
                        d.summary,
                        d.uploadedat,
                        d.processedat,
                        d.uploadedby,
                        d.createdat,
                        d.updatedat,
                        d.isdeleted,
                        d.deletedat
                    FROM dps_dbo.documents d
                    ORDER BY d.uploadedat DESC
                    OFFSET offset_val
                    LIMIT page_size;
                END;
                $$ LANGUAGE plpgsql;
            ");

            // Create function: get_document_types
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION dps_dbo.get_document_types()
                RETURNS TABLE (
                    id UUID,
                    name TEXT,
                    description TEXT,
                    category TEXT,
                    isactive BOOLEAN,
                    priority INT,
                    fileextensions TEXT,
                    keywords TEXT,
                    processingrules TEXT,
                    createdat TIMESTAMP,
                    updatedat TIMESTAMP
                ) AS $$
                BEGIN
                    RETURN QUERY
                    SELECT 
                        dt.id,
                        dt.name,
                        dt.description,
                        dt.category,
                        dt.isactive,
                        dt.priority,
                        dt.fileextensions,
                        dt.keywords,
                        dt.processingrules,
                        dt.createdat,
                        dt.updatedat
                    FROM dps_dbo.documenttypes dt
                    WHERE dt.isactive = TRUE
                    ORDER BY dt.name;
                END;
                $$ LANGUAGE plpgsql;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS dps_dbo.get_document_by_id(UUID);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS dps_dbo.get_all_documents();");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS dps_dbo.get_documents_by_user(TEXT);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS dps_dbo.get_recent_documents(INT, INT);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS dps_dbo.get_paged_documents(INT, INT);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS dps_dbo.get_document_types();");
        }
    }
}