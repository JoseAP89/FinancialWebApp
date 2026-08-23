using FinancialApp.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace FinancialApp.Core.Data
{
    public class FinancialDbContext : DbContext
    {
        public FinancialDbContext(DbContextOptions<FinancialDbContext> options) : base(options)
        {
        }

        public DbSet<Account> Accounts { get; set; } = null!;
        public DbSet<Transaction> Transactions { get; set; } = null!;
        public DbSet<TransactionLine> TransactionLines { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ===== SPECIFY TABLE NAMES IN LOWERCASE FOR POSTGRESQL =====
            modelBuilder.Entity<Account>().ToTable("accounts");
            modelBuilder.Entity<Transaction>().ToTable("transactions");
            modelBuilder.Entity<TransactionLine>().ToTable("transactionlines");

            // ACCOUNT ENTITY CONFIGURATION
            modelBuilder.Entity<Account>(entity =>
            {
                entity.Property(a => a.Name).IsRequired().HasMaxLength(200);
                entity.Property(a => a.Description).HasColumnType("TEXT");
                entity.Property(a => a.FinancialStatement)
                      .HasConversion<string>()
                      .HasColumnType("TEXT");

                // Map properties to lowercase column names
                entity.Property(a => a.Id).HasColumnName("id");
                entity.Property(a => a.Name).HasColumnName("name");
                entity.Property(a => a.Description).HasColumnName("description");
                entity.Property(a => a.FinancialStatement).HasColumnName("financialstatement");
                entity.Property(a => a.CreatedAt).HasColumnName("createdat");
                entity.Property(a => a.ParentId).HasColumnName("parentid");
                entity.Property(a => a.IsSystem).HasColumnName("issystem");

                entity.HasOne(a => a.Parent)
                      .WithMany(a => a.Children)
                      .HasForeignKey(a => a.ParentId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // TRANSACTION ENTITY CONFIGURATION
            modelBuilder.Entity<Transaction>(entity =>
            {
                entity.Property(t => t.Id).HasColumnName("id");
                entity.Property(t => t.Description).HasColumnName("description").HasMaxLength(500);
                entity.Property(t => t.Date)
                    .HasColumnName("date")
                    .HasColumnType("timestamp with time zone");
            });

            // TRANSACTION LINE ENTITY CONFIGURATION
            modelBuilder.Entity<TransactionLine>(entity =>
            {
                entity.Property(tl => tl.Id).HasColumnName("id");
                entity.Property(tl => tl.TransactionId).HasColumnName("transactionid");
                entity.Property(tl => tl.AccountId).HasColumnName("accountid");
                entity.Property(tl => tl.Amount).HasColumnName("amount").HasColumnType("decimal(18,2)");
                entity.Property(tl => tl.Description).HasColumnName("description").HasColumnType("TEXT");
                entity.Property(tl => tl.Quantity).HasColumnName("quantity").IsRequired().HasDefaultValue(1);
                entity.Property(tl => tl.IsAutoBalanced).HasColumnName("isautobalanced").IsRequired().HasDefaultValue(false);

                // Check constraint with quoted column name
                entity.ToTable(t => t.HasCheckConstraint("CK_TransactionLines_Quantity", "\"quantity\" >= 1"));

                entity.HasOne(tl => tl.Transaction)
                      .WithMany(t => t.TransactionLines)
                      .HasForeignKey(tl => tl.TransactionId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(tl => tl.Account)
                      .WithMany(a => a.TransactionLines)
                      .HasForeignKey(tl => tl.AccountId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
