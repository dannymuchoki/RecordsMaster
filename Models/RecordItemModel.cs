using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;

namespace RecordsMaster.Models
{

    public class SeedHistory
    {
        public int Id { get; set; }
        public string SeedType { get; set; }
        public DateTime AppliedOn { get; set; }
    }
    // foreign key property named CheckedOutTo.
    public class RecordItemModel
    {
        [Key]
        public Guid ID { get; set; }

        // case numbers are no longer known as 'CIS' numbers but this convention remains. 
        [Required]
        public string CIS { get; set; }

        [Required]
        [StringLength(100)]
        public string? BarCode { get; set; }

        [Required]
        [StringLength(50)]
        public string RecordType { get; set; }

        [Required]
        [StringLength(128)]
        public string Location { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Box Number must be a positive integer.")]
        public int? BoxNumber { get; set; }

        public bool Digitized { get; set; }

        public DateTime? ClosingDate { get; set; }

        public DateTime? DestroyDate { get; set; }

        public bool CheckedOut { get; set; }

        public bool Requested { get; set; }

        public bool ReadyForPickup { get; set; }

        // The foreign key property:
        public string? CheckedOutToId { get; set; }

        // The corresponding navigation property.
        [ForeignKey(nameof(CheckedOutToId))]
        public ApplicationUser? CheckedOutTo { get; set; }

        [Required]
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

    }

    // This class merges the ASP.NET Identity user with your custom properties, so just build the user with Identity user. No need to make a custom model. 
    public class ApplicationUser : IdentityUser
    {
        // Navigation property: one ApplicationUser can have many RecordItemModel records checked out.
        public ICollection<RecordItemModel> CheckedOutRecords { get; set; } = new List<RecordItemModel>();
    }
}