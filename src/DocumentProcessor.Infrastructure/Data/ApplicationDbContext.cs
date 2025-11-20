using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using DocumentProcessor.Core.Entities;

namespace DocumentProcessor.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext
    {
        static ApplicationDbContext()
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Document> Documents { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure Document entity
            modelBuilder.Entity<Document>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.ToTable("documents", schema: "dps_dbo");

                // Configure column mappings
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.FileName).HasColumnName("filename").IsRequired().HasMaxLength(500);
                entity.Property(e => e.OriginalFileName).HasColumnName("originalfilename").IsRequired().HasMaxLength(500);
                entity.Property(e => e.FileExtension).HasColumnName("fileextension").HasMaxLength(50);
                entity.Property(e => e.FileSize).HasColumnName("filesize");
                entity.Property(e => e.ContentType).HasColumnName("contenttype").HasMaxLength(100);
                entity.Property(e => e.StoragePath).HasColumnName("storagepath").HasMaxLength(1000);
                entity.Property(e => e.S3Key).HasColumnName("s3key").HasMaxLength(500);
                entity.Property(e => e.S3Bucket).HasColumnName("s3bucket").HasMaxLength(255);
                entity.Property(e => e.Source).HasColumnName("source");
                entity.Property(e => e.Status).HasColumnName("status");
                entity.Property(e => e.UploadedBy).HasColumnName("uploadedby").IsRequired().HasMaxLength(255);
                entity.Property(e => e.DocumentTypeName).HasColumnName("documenttypename").HasMaxLength(255);
                entity.Property(e => e.DocumentTypeCategory).HasColumnName("documenttypecategory").HasMaxLength(100);
                entity.Property(e => e.ProcessingStatus).HasColumnName("processingstatus").HasMaxLength(50);
                entity.Property(e => e.ProcessingRetryCount).HasColumnName("processingretrycount");
                entity.Property(e => e.ProcessingErrorMessage).HasColumnName("processingerrormessage").HasMaxLength(1000);
                entity.Property(e => e.ProcessingStartedAt).HasColumnName("processingstartedat");
                entity.Property(e => e.ProcessingCompletedAt).HasColumnName("processingcompletedat");
                entity.Property(e => e.ExtractedText).HasColumnName("extractedtext");
                entity.Property(e => e.Summary).HasColumnName("summary");
                entity.Property(e => e.UploadedAt).HasColumnName("uploadedat");
                entity.Property(e => e.ProcessedAt).HasColumnName("processedat");
                entity.Property(e => e.CreatedAt).HasColumnName("createdat");
                entity.Property(e => e.UpdatedAt).HasColumnName("updatedat");
                entity.Property(e => e.DeletedAt).HasColumnName("deletedat");
                
                // Configure boolean property conversion for PostgreSQL
                entity.Property(e => e.IsDeleted).HasColumnName("isdeleted").HasConversion<int>();

                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.UploadedAt);
                entity.HasIndex(e => e.IsDeleted);
                entity.HasIndex(e => e.ProcessingStatus);

                // Configure soft delete filter
                entity.HasQueryFilter(e => !e.IsDeleted);
            });
        }

        public override int SaveChanges()
        {
            UpdateTimestamps();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateTimestamps();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateTimestamps()
        {
            var entries = ChangeTracker.Entries<Document>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                }
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }
    }
}