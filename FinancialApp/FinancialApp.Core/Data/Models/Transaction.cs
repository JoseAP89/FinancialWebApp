using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace FinancialApp.Data.Models
{
    public class Transaction
    {
        [Key]
        public int Id { get; set; }

        // Account reference removed: Transaction has no direct relationship to Account

        [MaxLength(500)]
        public string? Description { get; set; }

        public DateTime Date { get; set; }

        // Transaction lines for split transactions
        public ICollection<TransactionLine> TransactionLines { get; set; } = new List<TransactionLine>();
    }
}
