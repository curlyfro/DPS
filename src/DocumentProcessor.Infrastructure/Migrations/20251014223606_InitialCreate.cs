using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DocumentProcessor.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dps_dbo");
                
            migrationBuilder.CreateTable(
                name: "documenttypes",
                schema: "dps_dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    isactive = table.Column<bool>(type: "boolean", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    fileextensions = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    keywords = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    processingrules = table.Column<string>(type: "text", nullable: true),
                    createdat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documenttypes", x => x.id);
                });}

            migrationBuilder.CreateTable(
                name: "documents",
                schema: "dps_dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    filename = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    originalfilename = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    fileextension = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    filesize = table.Column<long>(type: "bigint", nullable: false),
                    contenttype = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    storagepath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    s3key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    s3bucket = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    source = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    documenttypeid = table.Column<Guid>(type: "uuid", nullable: true),
                    extractedtext = table.Column<string>(type: "text", nullable: true),
                    summary = table.Column<string>(type: "text", nullable: true),
                    uploadedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    uploadedby = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    createdat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    isdeleted = table.Column<bool>(type: "boolean", nullable: false),
                    deletedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documents", x => x.id);
                    table.ForeignKey(
                        name: "FK_documents_documenttypes_documenttypeid",
                        column: x => x.documenttypeid,
                        principalSchema: "dps_dbo",
                        principalTable: "documenttypes",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "classifications",
                schema: "dps_dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    documentid = table.Column<Guid>(type: "uuid", nullable: false),
                    documenttypeid = table.Column<Guid>(type: "uuid", nullable: false),
                    confidencescore = table.Column<double>(type: "double precision", nullable: false),
                    method = table.Column<int>(type: "integer", nullable: false),
                    aimodelused = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    airesponse = table.Column<string>(type: "text", nullable: true),
                    ismanuallyverified = table.Column<bool>(type: "boolean", nullable: false),
                    classifiedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    createdat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_classifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_classifications_documenttypes_documenttypeid",
                        column: x => x.documenttypeid,
                        principalSchema: "dps_dbo",
                        principalTable: "documenttypes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_classifications_documents_documentid",
                        column: x => x.documentid,
                        principalSchema: "dps_dbo",
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "documentmetadata",
                schema: "dps_dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    documentid = table.Column<Guid>(type: "uuid", nullable: false),
                    author = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    keywords = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    creationdate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modificationdate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    pagecount = table.Column<int>(type: "integer", nullable: true),
                    wordcount = table.Column<int>(type: "integer", nullable: true),
                    language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    tags = table.Column<string>(type: "text", nullable: true),
                    createdat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documentmetadata", x => x.id);
                    table.ForeignKey(
                        name: "FK_documentmetadata_documents_documentid",
                        column: x => x.documentid,
                        principalSchema: "dps_dbo",
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "processingqueues",
                schema: "dps_dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    documentid = table.Column<Guid>(type: "uuid", nullable: false),
                    processingtype = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    retrycount = table.Column<int>(type: "integer", nullable: false),
                    maxretries = table.Column<int>(type: "integer", nullable: false),
                    startedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    errormessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    errordetails = table.Column<string>(type: "text", nullable: true),
                    processorid = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    resultdata = table.Column<string>(type: "text", nullable: true),
                    createdat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    nextretryat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processingqueues", x => x.id);
                    table.ForeignKey(
                        name: "FK_processingqueues_documents_documentid",
                        column: x => x.documentid,
                        principalSchema: "dps_dbo",
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "dps_dbo",
                table: "documenttypes",
                columns: new[] { "id", "category", "createdat", "description", "fileextensions", "isactive", "keywords", "name", "priority", "processingrules", "updatedat" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), "Financial", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Commercial invoice documents", ".pdf,.doc,.docx", true, null, "Invoice", 1, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("22222222-2222-2222-2222-222222222222"), "Legal", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Legal contract documents", ".pdf,.doc,.docx", true, null, "Contract", 1, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("33333333-3333-3333-3333-333333333333"), "Business", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Business reports and analytics", ".pdf,.xlsx,.docx", true, null, "Report", 2, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("44444444-4444-4444-4444-444444444444"), "HR", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Resume and CV documents", ".pdf,.doc,.docx", true, null, "Resume", 3, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("55555555-5555-5555-5555-555555555555"), "Communication", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Email correspondence", ".eml,.msg,.txt", true, null, "Email", 3, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_classifications_classifiedat",
                schema: "dps_dbo",
                table: "classifications",
                column: "classifiedat");

            migrationBuilder.CreateIndex(
                name: "IX_classifications_confidencescore",
                schema: "dps_dbo",
                table: "classifications",
                column: "confidencescore");

            migrationBuilder.CreateIndex(
                name: "IX_classifications_documentid_documenttypeid",
                schema: "dps_dbo",
                table: "classifications",
                columns: new[] { "documentid", "documenttypeid" });

            migrationBuilder.CreateIndex(
                name: "IX_classifications_documenttypeid",
                schema: "dps_dbo",
                table: "classifications",
                column: "documenttypeid");

            migrationBuilder.CreateIndex(
                name: "IX_documentmetadata_documentid",
                schema: "dps_dbo",
                table: "documentmetadata",
                column: "documentid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_documents_documenttypeid",
                schema: "dps_dbo",
                table: "documents",
                column: "documenttypeid");

            migrationBuilder.CreateIndex(
                name: "IX_documents_isdeleted",
                schema: "dps_dbo",
                table: "documents",
                column: "isdeleted");

            migrationBuilder.CreateIndex(
                name: "IX_documents_status",
                schema: "dps_dbo",
                table: "documents",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_documents_uploadedat",
                schema: "dps_dbo",
                table: "documents",
                column: "uploadedat");

            migrationBuilder.CreateIndex(
                name: "IX_documenttypes_category",
                schema: "dps_dbo",
                table: "documenttypes",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "IX_documenttypes_isactive",
                schema: "dps_dbo",
                table: "documenttypes",
                column: "isactive");

            migrationBuilder.CreateIndex(
                name: "IX_documenttypes_name",
                schema: "dps_dbo",
                table: "documenttypes",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_processingqueues_createdat",
                schema: "dps_dbo",
                table: "processingqueues",
                column: "createdat");

            migrationBuilder.CreateIndex(
                name: "IX_processingqueues_documentid",
                schema: "dps_dbo",
                table: "processingqueues",
                column: "documentid");

            migrationBuilder.CreateIndex(
                name: "IX_processingqueues_priority",
                schema: "dps_dbo",
                table: "processingqueues",
                column: "priority");

            migrationBuilder.CreateIndex(
                name: "IX_processingqueues_status",
                schema: "dps_dbo",
                table: "processingqueues",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_processingqueues_status_priority",
                schema: "dps_dbo",
                table: "processingqueues",
                columns: new[] { "status", "priority" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "classifications",
                schema: "dps_dbo");

            migrationBuilder.DropTable(
                name: "documentmetadata",
                schema: "dps_dbo");

            migrationBuilder.DropTable(
                name: "processingqueues",
                schema: "dps_dbo");

            migrationBuilder.DropTable(
                name: "documents",
                schema: "dps_dbo");

            migrationBuilder.DropTable(
                name: "documenttypes",
                schema: "dps_dbo");
                
            migrationBuilder.DropSchema(
                name: "dps_dbo");
        }
    }
}
