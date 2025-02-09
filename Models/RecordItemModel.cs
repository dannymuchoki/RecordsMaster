  using System;
  using System.ComponentModel.DataAnnotations;

  namespace RecordsMaster.Models
  {
      public class RecordItemModel
      {
          [Key]
          public Guid ID {get; set;}

          [Required]
          public int CIS { get; set; }

          [Required]
          [StringLength(100)]
          public string? BarCode { get; set; }

          [Required]
          [StringLength(50)]
          public string RecordType { get; set; }

          [Range(1, int.MaxValue, ErrorMessage = "Box Number must be a positive integer.")]
          public int? BoxNumber { get; set; }

          public bool Digitized { get; set; }

          public DateTime? ClosingDate { get; set; }

          public DateTime? DestroyDate { get; set; }

          public bool CheckedOut {get; set; }


          public string? CheckedOutBy {get; set; }
      }
  }