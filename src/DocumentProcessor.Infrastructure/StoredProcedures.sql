-- Stored Procedures for Document Processing System
-- These stored procedures replace Entity Framework DbSet queries in DocumentRepository
-- PostgreSQL version

-- 1. Get Document by ID with all related data
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
    deletedat TIMESTAMP,
    -- Document Type
    documenttype_id UUID,
    documenttype_name TEXT,
    documenttype_description TEXT,
    documenttype_isactive BOOLEAN,
    documenttype_createdat TIMESTAMP,
    documenttype_updatedat TIMESTAMP,
    -- Metadata
    metadata_id UUID,
    metadata_documentid UUID,
    metadata_author TEXT,
    metadata_title TEXT,
    metadata_subject TEXT,
    metadata_keywords TEXT,
    metadata_creationdate TIMESTAMP,
    metadata_modificationdate TIMESTAMP,
    metadata_pagecount INT,
    metadata_wordcount INT,
    metadata_language TEXT,
    metadata_custommetadata JSONB,
    metadata_tags TEXT,
    metadata_createdat TIMESTAMP,
    metadata_updatedat TIMESTAMP,
    -- Classifications
    classification_id UUID,
    classification_documentid UUID,
    classification_documenttypeid UUID,
    classification_confidencescore FLOAT,
    classification_method TEXT,
    classification_aimodelused TEXT,
    classification_airesponse TEXT,
    classification_extractedintents TEXT,
    classification_extractedentities TEXT,
    classification_ismanuallyverified BOOLEAN,
    classification_verifiedby TEXT,
    classification_verifiedat TIMESTAMP,
    classification_classifiedat TIMESTAMP,
    classification_createdat TIMESTAMP,
    classification_updatedat TIMESTAMP,
    -- Classification Document Type
    classificationdocumenttype_id UUID,
    classificationdocumenttype_name TEXT,
    classificationdocumenttype_description TEXT,
    classificationdocumenttype_isactive BOOLEAN,
    classificationdocumenttype_createdat TIMESTAMP,
    classificationdocumenttype_updatedat TIMESTAMP,
    -- Processing Queue Items
    processingqueue_id UUID,
    processingqueue_documentid UUID,
    processingqueue_processingtype TEXT,
    processingqueue_status INT,
    processingqueue_priority INT,
    processingqueue_retrycount INT,
    processingqueue_maxretries INT,
    processingqueue_startedat TIMESTAMP,
    processingqueue_completedat TIMESTAMP,
    processingqueue_errormessage TEXT,
    processingqueue_errordetails TEXT,
    processingqueue_processorid TEXT,
    processingqueue_resultdata TEXT,
    processingqueue_nextretryat TIMESTAMP,
    processingqueue_createdat TIMESTAMP,
    processingqueue_updatedat TIMESTAMP
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
        d.deletedat,
        
        -- Document Type
        dt.id,
        dt.name,
        dt.description,
        dt.isactive,
        dt.createdat,
        dt.updatedat,
        
        -- Metadata
        dm.id,
        dm.documentid,
        dm.author,
        dm.title,
        dm.subject,
        dm.keywords,
        dm.creationdate,
        dm.modificationdate,
        dm.pagecount,
        dm.wordcount,
        dm.language,
        dm.custommetadata::jsonb,
        dm.tags,
        dm.createdat,
        dm.updatedat,
        
        -- Classifications
        c.id,
        c.documentid,
        c.documenttypeid,
        c.confidencescore,
        c.method,
        c.aimodelused,
        c.airesponse,
        c.extractedintents,
        c.extractedentities,
        c.ismanuallyverified,
        c.verifiedby,
        c.verifiedat,
        c.classifiedat,
        c.createdat,
        c.updatedat,
        
        -- Classification Document Type
        cdt.id,
        cdt.name,
        cdt.description,
        cdt.isactive,
        cdt.createdat,
        cdt.updatedat,
        
        -- Processing Queue Items
        pq.id,
        pq.documentid,
        pq.processingtype,
        pq.status,
        pq.priority,
        pq.retrycount,
        pq.maxretries,
        pq.startedat,
        pq.completedat,
        pq.errormessage,
        pq.errordetails,
        pq.processorid,
        pq.resultdata,
        pq.nextretryat,
        pq.createdat,
        pq.updatedat
        
    FROM dps_dbo.documents d
    LEFT JOIN dps_dbo.documenttypes dt ON d.documenttypeid = dt.id
    LEFT JOIN dps_dbo.documentmetadata dm ON d.id = dm.documentid
    LEFT JOIN dps_dbo.classifications c ON d.id = c.documentid
    LEFT JOIN dps_dbo.documenttypes cdt ON c.documenttypeid = cdt.id
    LEFT JOIN dps_dbo.processingqueues pq ON d.id = pq.documentid
    WHERE d.id = document_id;
END;
$$ LANGUAGE plpgsql;

-- 2. Get All Documents with related data
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
    deletedat TIMESTAMP,
    -- Document Type
    documenttype_id UUID,
    documenttype_name TEXT,
    documenttype_description TEXT,
    documenttype_isactive BOOLEAN,
    documenttype_createdat TIMESTAMP,
    documenttype_updatedat TIMESTAMP,
    -- Metadata
    metadata_id UUID,
    metadata_documentid UUID,
    metadata_author TEXT,
    metadata_title TEXT,
    metadata_subject TEXT,
    metadata_keywords TEXT,
    metadata_creationdate TIMESTAMP,
    metadata_modificationdate TIMESTAMP,
    metadata_pagecount INT,
    metadata_wordcount INT,
    metadata_language TEXT,
    metadata_custommetadata JSONB,
    metadata_tags TEXT,
    metadata_createdat TIMESTAMP,
    metadata_updatedat TIMESTAMP,
    -- Classifications
    classification_id UUID,
    classification_documentid UUID,
    classification_documenttypeid UUID,
    classification_confidencescore FLOAT,
    classification_method TEXT,
    classification_aimodelused TEXT,
    classification_airesponse TEXT,
    classification_extractedintents TEXT,
    classification_extractedentities TEXT,
    classification_ismanuallyverified BOOLEAN,
    classification_verifiedby TEXT,
    classification_verifiedat TIMESTAMP,
    classification_classifiedat TIMESTAMP,
    classification_createdat TIMESTAMP,
    classification_updatedat TIMESTAMP,
    -- Classification Document Type
    classificationdocumenttype_id UUID,
    classificationdocumenttype_name TEXT,
    classificationdocumenttype_description TEXT,
    classificationdocumenttype_isactive BOOLEAN,
    classificationdocumenttype_createdat TIMESTAMP,
    classificationdocumenttype_updatedat TIMESTAMP,
    -- Processing Queue Items
    processingqueue_id UUID,
    processingqueue_documentid UUID,
    processingqueue_processingtype TEXT,
    processingqueue_status INT,
    processingqueue_priority INT,
    processingqueue_retrycount INT,
    processingqueue_maxretries INT,
    processingqueue_startedat TIMESTAMP,
    processingqueue_completedat TIMESTAMP,
    processingqueue_errormessage TEXT,
    processingqueue_errordetails TEXT,
    processingqueue_processorid TEXT,
    processingqueue_resultdata TEXT,
    processingqueue_nextretryat TIMESTAMP,
    processingqueue_createdat TIMESTAMP,
    processingqueue_updatedat TIMESTAMP
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
        d.deletedat,
        
        -- Document Type
        dt.id,
        dt.name,
        dt.description,
        dt.isactive,
        dt.createdat,
        dt.updatedat,
        
        -- Metadata
        dm.id,
        dm.documentid,
        dm.author,
        dm.title,
        dm.subject,
        dm.keywords,
        dm.creationdate,
        dm.modificationdate,
        dm.pagecount,
        dm.wordcount,
        dm.language,
        dm.custommetadata::jsonb,
        dm.tags,
        dm.createdat,
        dm.updatedat,
        
        -- Classifications
        c.id,
        c.documentid,
        c.documenttypeid,
        c.confidencescore,
        c.method,
        c.aimodelused,
        c.airesponse,
        c.extractedintents,
        c.extractedentities,
        c.ismanuallyverified,
        c.verifiedby,
        c.verifiedat,
        c.classifiedat,
        c.createdat,
        c.updatedat,
        
        -- Classification Document Type
        cdt.id,
        cdt.name,
        cdt.description,
        cdt.isactive,
        cdt.createdat,
        cdt.updatedat,
        
        -- Processing Queue Items
        pq.id,
        pq.documentid,
        pq.processingtype,
        pq.status,
        pq.priority,
        pq.retrycount,
        pq.maxretries,
        pq.startedat,
        pq.completedat,
        pq.errormessage,
        pq.errordetails,
        pq.processorid,
        pq.resultdata,
        pq.nextretryat,
        pq.createdat,
        pq.updatedat
        
    FROM dps_dbo.documents d
    LEFT JOIN dps_dbo.documenttypes dt ON d.documenttypeid = dt.id
    LEFT JOIN dps_dbo.documentmetadata dm ON d.id = dm.documentid
    LEFT JOIN dps_dbo.classifications c ON d.id = c.documentid
    LEFT JOIN dps_dbo.documenttypes cdt ON c.documenttypeid = cdt.id
    LEFT JOIN dps_dbo.processingqueues pq ON d.id = pq.documentid
    ORDER BY d.uploadedat DESC;
END;
$$ LANGUAGE plpgsql;

-- 3. Get Documents by User with related data
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
    deletedat TIMESTAMP,
    -- Document Type
    documenttype_id UUID,
    documenttype_name TEXT,
    documenttype_description TEXT,
    documenttype_isactive BOOLEAN,
    documenttype_createdat TIMESTAMP,
    documenttype_updatedat TIMESTAMP,
    -- Metadata
    metadata_id UUID,
    metadata_documentid UUID,
    metadata_author TEXT,
    metadata_title TEXT,
    metadata_subject TEXT,
    metadata_keywords TEXT,
    metadata_creationdate TIMESTAMP,
    metadata_modificationdate TIMESTAMP,
    metadata_pagecount INT,
    metadata_wordcount INT,
    metadata_language TEXT,
    metadata_custommetadata JSONB,
    metadata_tags TEXT,
    metadata_createdat TIMESTAMP,
    metadata_updatedat TIMESTAMP
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
        d.deletedat,
        
        -- Document Type
        dt.id,
        dt.name,
        dt.description,
        dt.isactive,
        dt.createdat,
        dt.updatedat,
        
        -- Metadata
        dm.id,
        dm.documentid,
        dm.author,
        dm.title,
        dm.subject,
        dm.keywords,
        dm.creationdate,
        dm.modificationdate,
        dm.pagecount,
        dm.wordcount,
        dm.language,
        dm.custommetadata::jsonb,
        dm.tags,
        dm.createdat,
        dm.updatedat
    FROM dps_dbo.documents d
    LEFT JOIN dps_dbo.documenttypes dt ON d.documenttypeid = dt.id
    LEFT JOIN dps_dbo.documentmetadata dm ON d.id = dm.documentid
    WHERE d.uploadedby = user_id
    ORDER BY d.uploadedat DESC;
END;
$$ LANGUAGE plpgsql;

-- 4. Get Recent Documents with related data
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
    updatedat TIMESTAMP,
    -- Document Type
    documenttype_id UUID,
    documenttype_name TEXT,
    documenttype_description TEXT,
    documenttype_isactive BOOLEAN,
    documenttype_createdat TIMESTAMP,
    documenttype_updatedat TIMESTAMP,
    -- Metadata
    metadata_id UUID,
    metadata_documentid UUID,
    metadata_author TEXT,
    metadata_title TEXT,
    metadata_subject TEXT,
    metadata_keywords TEXT,
    metadata_creationdate TIMESTAMP,
    metadata_modificationdate TIMESTAMP,
    metadata_pagecount INT,
    metadata_wordcount INT,
    metadata_language TEXT,
    metadata_custommetadata JSONB,
    metadata_tags TEXT,
    metadata_createdat TIMESTAMP,
    metadata_updatedat TIMESTAMP
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
        d.updatedat,
        
        -- Document Type
        dt.id,
        dt.name,
        dt.description,
        dt.isactive,
        dt.createdat,
        dt.updatedat,
        
        -- Metadata
        dm.id,
        dm.documentid,
        dm.author,
        dm.title,
        dm.subject,
        dm.keywords,
        dm.creationdate,
        dm.modificationdate,
        dm.pagecount,
        dm.wordcount,
        dm.language,
        dm.custommetadata::jsonb,
        dm.tags,
        dm.createdat,
        dm.updatedat
    FROM dps_dbo.documents d
    LEFT JOIN dps_dbo.documenttypes dt ON d.documenttypeid = dt.id
    LEFT JOIN dps_dbo.documentmetadata dm ON d.id = dm.documentid
    WHERE d.uploadedat >= cutoff_date
    ORDER BY d.uploadedat DESC
    LIMIT limit_count;
END;
$$ LANGUAGE plpgsql;

-- 5. Get Paged Documents with related data
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
    deletedat TIMESTAMP,
    -- Document Type
    documenttype_id UUID,
    documenttype_name TEXT,
    documenttype_description TEXT,
    documenttype_isactive BOOLEAN,
    documenttype_createdat TIMESTAMP,
    documenttype_updatedat TIMESTAMP,
    -- Metadata
    metadata_id UUID,
    metadata_documentid UUID,
    metadata_author TEXT,
    metadata_title TEXT,
    metadata_subject TEXT,
    metadata_keywords TEXT,
    metadata_creationdate TIMESTAMP,
    metadata_modificationdate TIMESTAMP,
    metadata_pagecount INT,
    metadata_wordcount INT,
    metadata_language TEXT,
    metadata_custommetadata JSONB,
    metadata_tags TEXT,
    metadata_createdat TIMESTAMP,
    metadata_updatedat TIMESTAMP
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
        d.deletedat,
        
        -- Document Type
        dt.id,
        dt.name,
        dt.description,
        dt.isactive,
        dt.createdat,
        dt.updatedat,
        
        -- Metadata
        dm.id,
        dm.documentid,
        dm.author,
        dm.title,
        dm.subject,
        dm.keywords,
        dm.creationdate,
        dm.modificationdate,
        dm.pagecount,
        dm.wordcount,
        dm.language,
        dm.custommetadata::jsonb,
        dm.tags,
        dm.createdat,
        dm.updatedat
    FROM dps_dbo.documents d
    LEFT JOIN dps_dbo.documenttypes dt ON d.documenttypeid = dt.id
    LEFT JOIN dps_dbo.documentmetadata dm ON d.id = dm.documentid
    ORDER BY d.uploadedat DESC
    OFFSET offset_val
    LIMIT page_size;
END;
$$ LANGUAGE plpgsql;

-- 6. Get All Document Types
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