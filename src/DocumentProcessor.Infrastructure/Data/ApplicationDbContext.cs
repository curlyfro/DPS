using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using DocumentProcessor.Core.Entities;
using Npgsql;

namespace DocumentProcessor.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Document> Documents { get; set; }
        public DbSet<DocumentType> DocumentTypes { get; set; }
        public DbSet<Classification> Classifications { get; set; }
        public DbSet<ProcessingQueue> ProcessingQueues { get; set; }
        public DbSet<DocumentMetadata> DocumentMetadata { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Set the default schema for all entities
            modelBuilder.HasDefaultSchema("dps_dbo");

            // Configure Document entity
            modelBuilder.Entity<Document>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FileName).IsRequired().HasMaxLength(500);
                entity.Property(e => e.OriginalFileName).IsRequired().HasMaxLength(500);
                entity.Property(e => e.FileExtension).HasMaxLength(50);
                entity.Property(e => e.ContentType).HasMaxLength(100);
                entity.Property(e => e.StoragePath).HasMaxLength(1000);
                entity.Property(e => e.S3Key).HasMaxLength(500);
                entity.Property(e => e.S3Bucket).HasMaxLength(255);
                entity.Property(e => e.UploadedBy).IsRequired().HasMaxLength(255);
                
                // Convert bool to int for IsDeleted if needed (PostgreSQL uses native boolean)
                entity.Property(e => e.IsDeleted).HasConversion<bool>();
                
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.DocumentTypeId);
                entity.HasIndex(e => e.UploadedAt);
                entity.HasIndex(e => e.IsDeleted);
                
                // Configure soft delete filter
                entity.HasQueryFilter(e => !e.IsDeleted);
                
                // Configure one-to-one relationship with metadata
                entity.HasOne(e => e.Metadata)
                    .WithOne(m => m.Document)
                    .HasForeignKey<DocumentMetadata>(m => m.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure DocumentType entity
            modelBuilder.Entity<DocumentType>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.Property(e => e.Category).HasMaxLength(100);
                entity.Property(e => e.FileExtensions).HasMaxLength(500);
                entity.Property(e => e.Keywords).HasMaxLength(2000);
                
                // Convert bool to int for IsActive if needed (PostgreSQL uses native boolean)
                entity.Property(e => e.IsActive).HasConversion<bool>();
                
                entity.HasIndex(e => e.Name).IsUnique();
                entity.HasIndex(e => e.Category);
                entity.HasIndex(e => e.IsActive);
            });

            // Configure Classification entity
            modelBuilder.Entity<Classification>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.AIModelUsed).HasMaxLength(100);
                
                // Convert bool to int for IsManuallyVerified if needed (PostgreSQL uses native boolean)
                entity.Property(e => e.IsManuallyVerified).HasConversion<bool>();

                entity.HasIndex(e => new { e.DocumentId, e.DocumentTypeId });
                entity.HasIndex(e => e.ConfidenceScore);
                entity.HasIndex(e => e.ClassifiedAt);
                
                entity.HasOne(e => e.Document)
                    .WithMany(d => d.Classifications)
                    .HasForeignKey(e => e.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);
                    
                entity.HasOne(e => e.DocumentType)
                    .WithMany(dt => dt.Classifications)
                    .HasForeignKey(e => e.DocumentTypeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure ProcessingQueue entity
            modelBuilder.Entity<ProcessingQueue>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ProcessorId).HasMaxLength(100);
                entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
                
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.Priority);
                entity.HasIndex(e => new { e.Status, e.Priority });
                entity.HasIndex(e => e.CreatedAt);
                
                entity.HasOne(e => e.Document)
                    .WithMany(d => d.ProcessingQueueItems)
                    .HasForeignKey(e => e.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure DocumentMetadata entity
            modelBuilder.Entity<DocumentMetadata>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Author).HasMaxLength(255);
                entity.Property(e => e.Title).HasMaxLength(500);
                entity.Property(e => e.Subject).HasMaxLength(500);
                entity.Property(e => e.Keywords).HasMaxLength(2000);
                entity.Property(e => e.Language).HasMaxLength(10);
                
                // Use PostgreSQL's JSONB type for CustomMetadata
                entity.Property(e => e.CustomMetadata).HasColumnType("jsonb");
                
                entity.HasIndex(e => e.DocumentId).IsUnique();
            });

            // Configure table names with PostgreSQL schema and lowercase column names
            modelBuilder.Entity<Document>().ToTable("documents", "dps_dbo");
            modelBuilder.Entity<DocumentType>().ToTable("documenttypes", "dps_dbo");
            modelBuilder.Entity<Classification>().ToTable("classifications", "dps_dbo");
            modelBuilder.Entity<ProcessingQueue>().ToTable("processingqueues", "dps_dbo");
            modelBuilder.Entity<DocumentMetadata>().ToTable("documentmetadata", "dps_dbo");

            // Use snake_case naming convention for PostgreSQL
            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                // Replace table names
                entity.SetTableName(entity.GetTableName().ToLower());
                
                // Replace column names
                foreach (var property in entity.GetProperties())
                {
                    property.SetColumnName(property.GetColumnName().ToLower());
                }

                // Replace keys names
                foreach (var key in entity.GetKeys())
                {
                    key.SetName(key.GetName().ToLower());
                }

                // Replace foreign keys names
                foreach (var key in entity.GetForeignKeys())
                {
                    key.SetConstraintName(key.GetConstraintName().ToLower());
                }

                // Replace index names
                foreach (var index in entity.GetIndexes())
                {
                    index.SetDatabaseName(index.GetDatabaseName().ToLower());
                }
            }

            // Seed initial document types
            var seedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            modelBuilder.Entity<DocumentType>().HasData(
                new DocumentType
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Name = "Invoice",
                    Description = "Commercial invoice documents",
                    Category = "Financial",
                    IsActive = true,
                    Priority = 1,
                    FileExtensions = ".pdf,.doc,.docx",
                    CreatedAt = seedDate,
                    UpdatedAt = seedDate
                },
                new DocumentType
                {
                    Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    Name = "Contract",
                    Description = "Legal contract documents",
                    Category = "Legal",
                    IsActive = true,
                    Priority = 1,
                    FileExtensions = ".pdf,.doc,.docx",
                    CreatedAt = seedDate,
                    UpdatedAt = seedDate
                },
                new DocumentType
                {
                    Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                    Name = "Report",
                    Description = "Business reports and analytics",
                    Category = "Business",
                    IsActive = true,
                    Priority = 2,
                    FileExtensions = ".pdf,.xlsx,.docx",
                    CreatedAt = seedDate,
                    UpdatedAt = seedDate
                },
                new DocumentType
                {
                    Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                    Name = "Resume",
                    Description = "Resume and CV documents",
                    Category = "HR",
                    IsActive = true,
                    Priority = 3,
                    FileExtensions = ".pdf,.doc,.docx",
                    CreatedAt = seedDate,
                    UpdatedAt = seedDate
                },
                new DocumentType
                {
                    Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                    Name = "Email",
                    Description = "Email correspondence",
                    Category = "Communication",
                    IsActive = true,
                    Priority = 3,
                    FileExtensions = ".eml,.msg,.txt",
                    CreatedAt = seedDate,
                    UpdatedAt = seedDate
                }
            );
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
            var entries = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            var utcNow = DateTime.UtcNow;
            
            foreach (var entry in entries)
            {
                if (entry.Entity is Document document)
                {
                    if (entry.State == EntityState.Added)
                    {
                        document.CreatedAt = utcNow;
                    }
                    document.UpdatedAt = utcNow;
                }
                else if (entry.Entity is DocumentType documentType)
                {
                    if (entry.State == EntityState.Added)
                    {
                        documentType.CreatedAt = utcNow;
                    }
                    documentType.UpdatedAt = utcNow;
                }
                else if (entry.Entity is Classification classification)
                {
                    if (entry.State == EntityState.Added)
                    {
                        classification.CreatedAt = utcNow;
                    }
                    classification.UpdatedAt = utcNow;
                }
                else if (entry.Entity is ProcessingQueue queue)
                {
                    if (entry.State == EntityState.Added)
                    {
                        queue.CreatedAt = utcNow;
                    }
                    queue.UpdatedAt = utcNow;
                }
                else if (entry.Entity is DocumentMetadata metadata)
                {
                    if (entry.State == EntityState.Added)
                    {
                        metadata.CreatedAt = utcNow;
                    }
                    metadata.UpdatedAt = utcNow;
                }
            }
        }
    }
}