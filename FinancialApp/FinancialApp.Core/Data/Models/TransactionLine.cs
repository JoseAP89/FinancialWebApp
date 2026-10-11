using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinancialApp.Data.Models
{
    public class TransactionLine
    {
        [Key]
        public int Id { get; set; }

        // Foreign key to Transaction
        public int TransactionId { get; set; }
        public Transaction? Transaction { get; set; }

        // Foreign key to Account
        public int AccountId { get; set; }
        public Account? Account { get; set; }

        // Amount for this line
        public decimal Amount { get; set; }

        // Quantity for this line (new column)
        // Default is 1 and must be >= 1
        public int Quantity { get; set; } = 1;

        // Optional description for the transaction line
        public string? Description { get; set; }

        // Points to the auto-balance entry line that was generated to compensate this line.
        // A NULL value means this line IS an auto-balance entry.
        public int? AutoBalancedByLineId { get; set; }
        public TransactionLine? AutoBalancedByLine { get; set; }

        // The lines that reference this line as their auto-balance entry.
        public ICollection<TransactionLine> AutoBalancedByLines { get; set; } = new List<TransactionLine>();
    }
}
