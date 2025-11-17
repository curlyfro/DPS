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
                entity.ToTable("documents", "dps_dbo");

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.FileName).IsRequired().HasMaxLength(500).HasColumnName("filename");
                entity.Property(e => e.OriginalFileName).IsRequired().HasMaxLength(500).HasColumnName("originalfilename");
                entity.Property(e => e.FileExtension).HasMaxLength(50).HasColumnName("fileextension");
                entity.Property(e => e.FileSize).HasColumnName("filesize");
                entity.Property(e => e.ContentType).HasMaxLength(100).HasColumnName("contenttype");
                entity.Property(e => e.StoragePath).HasMaxLength(1000).HasColumnName("storagepath");
                entity.Property(e => e.S3Key).HasMaxLength(500).HasColumnName("s3key");
                entity.Property(e => e.S3Bucket).HasMaxLength(255).HasColumnName("s3bucket");
                entity.Property(e => e.Source).HasColumnName("source");
                entity.Property(e => e.Status).HasColumnName("status");
                entity.Property(e => e.UploadedBy).IsRequired().HasMaxLength(255).HasColumnName("uploadedby");
                entity.Property(e => e.DocumentTypeName).HasMaxLength(255).HasColumnName("documenttypename");
                entity.Property(e => e.DocumentTypeCategory).HasMaxLength(100).HasColumnName("documenttypecategory");
                entity.Property(e => e.ProcessingStatus).HasMaxLength(50).HasColumnName("processingstatus");
                entity.Property(e => e.ProcessingRetryCount).HasColumnName("processingretrycount");
                entity.Property(e => e.ProcessingErrorMessage).HasMaxLength(1000).HasColumnName("processingerrormessage");
                entity.Property(e => e.ProcessingStartedAt).HasColumnName("processingstartedat");
                entity.Property(e => e.ProcessingCompletedAt).HasColumnName("processingcompletedat");
                entity.Property(e => e.ExtractedText).HasColumnName("extractedtext");
                entity.Property(e => e.Summary).HasColumnName("summary");
                entity.Property(e => e.UploadedAt).HasColumnName("uploadedat");
                entity.Property(e => e.ProcessedAt).HasColumnName("processedat");
                entity.Property(e => e.CreatedAt).HasColumnName("createdat");
                entity.Property(e => e.UpdatedAt).HasColumnName("updatedat");
                entity.Property(e => e.IsDeleted).HasColumnName("isdeleted").HasConversion<int>();
                entity.Property(e => e.DeletedAt).HasColumnName("deletedat");

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