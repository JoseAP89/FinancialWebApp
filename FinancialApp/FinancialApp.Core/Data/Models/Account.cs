using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FinancialApp.Data.Models
{
    public class Account
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;
        // Description for the account (string stored as TEXT in SQLite)
        [Required]
        public string Description { get; set; } = string.Empty;

        // Which financial statement this account belongs to
        [Required]
        public FinancialStatement FinancialStatement { get; set; } = FinancialStatement.ASSET;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public int? ParentId { get; set; }

        // Indicates this is a system/internal account and should not be shown to users
        public bool IsSystem { get; set; } = false;

        // Self-referencing navigation for hierarchical accounts
        public Account? Parent { get; set; }

        public ICollection<Account> Children { get; set; } = new List<Account>();

        // Transaction lines referencing this account
        public ICollection<TransactionLine> TransactionLines { get; set; } = new List<TransactionLine>();
    }
}
