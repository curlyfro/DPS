using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentProcessor.Core.Entities
{
    [Table("classifications", Schema = "dps_dbo")]
    public class Classification
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }
        [Column("documentid")]
        public Guid DocumentId { get; set; }
        public Document Document { get; set; } = null!;
        [Column("documenttypeid")]
        public Guid DocumentTypeId { get; set; }
        public DocumentType DocumentType { get; set; } = null!;
        [Column("confidencescore")]
        public double ConfidenceScore { get; set; }
        public ClassificationMethod Method { get; set; }
        [Column("aimodelused")]
        public string? AIModelUsed { get; set; }
        [Column("airesponse")]
        public string? AIResponse { get; set; }
        [Column("ismanuallyverified")]
        public bool IsManuallyVerified { get; set; }
        [Column("classifiedat")]
        public DateTime ClassifiedAt { get; set; }
        [Column("createdat")]
        public DateTime CreatedAt { get; set; }
        [Column("updatedat")]
        public DateTime UpdatedAt { get; set; }
    }

    public enum ClassificationMethod
    {
        AI
    }
}