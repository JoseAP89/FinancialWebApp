using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using FinancialApp.Data.Models;
using FinancialApp.Core.Data.Repositories;
using FinancialApp.Core.DTOs;

namespace FinancialApp.Components.Pages
{
    public class FinancialHistoryBase : ComponentBase
    {
        [Inject]
        protected ITransactionRepository TransactionRepository { get; set; } = null!;

        [Inject]
        protected ILogger<FinancialHistoryBase> Logger { get; set; } = null!;

        protected IEnumerable<Transaction> FilteredTransactions { get; set; } = Enumerable.Empty<Transaction>();

        protected bool IsLoading { get; set; }
        protected decimal PeriodTransactionValue { get; set; }
        protected decimal PeriodExpenses { get; set; }
        protected decimal PeriodLiabilities { get; set; }
        protected decimal PeriodRevenue { get; set; }

        // Track visibility state for each transaction's expense list (initially hidden)
        protected HashSet<int> ExpandedTransactions { get; set; } = new HashSet<int>();

        // Bind to input type=date which uses yyyy-MM-dd format
        protected string? StartDateString { get; set; }
        protected string? EndDateString { get; set; }

        protected DateTime? StartDate => ParseDate(StartDateString);
        protected DateTime? EndDate => ParseDate(EndDateString);

        protected override async Task OnInitializedAsync()
        {
            IsLoading = true;
            try
            {
                // default dates
                StartDateString = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).ToString("yyyy-MM-dd");
                var end = DateTime.Today;
                // include entire day for end
                EndDateString = end.ToString("yyyy-MM-dd");
                await ApplyFilter();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Failed to load transactions for FinancialHistory");
                FilteredTransactions = [];
            }
            finally
            {
                IsLoading = false;
            }
        }

        protected async Task ApplyFilter()
        {
            if (StartDate is null && EndDate is null)
            {
                FilteredTransactions = [];
                return;
            }

            var start = StartDate?.Date ?? DateTime.MinValue.Date;
            var end = EndDate?.Date ?? DateTime.Today;

            var items = await TransactionRepository.ListWithNoSystemLinesByDateRangeAsync(DateOnly.FromDateTime(start), DateOnly.FromDateTime(end));
            FilteredTransactions = items
                 ?.Select(t => new Transaction
                 {
                     Id = t.Id,
                     Description = t.Description,
                     Date = t.Date,
                     // Filter only lines where account amount > 500
                     TransactionLines = t.TransactionLines?
                        .Where(line => line.Description != null && !line.Description.Contains("Auto-balance entry"))
                        .ToList() ?? []
                 })
                .Where(t => t.TransactionLines.Any())
                .ToList() ?? [];
            GetSubTotalTransactionValues(FilteredTransactions);
        }

        protected void ResetFilter()
        {
            StartDateString = null;
            EndDateString = null;
            FilteredTransactions = [];
            PeriodTransactionValue = 0;
            PeriodExpenses = 0;
            PeriodLiabilities = 0;
            PeriodRevenue = 0;
        }

        protected void GetSubTotalTransactionValues(IEnumerable<Transaction> transactions)
        {
            decimal subtotal = 0;
            decimal subexpenses = 0;
            decimal subliabilities = 0;
            decimal subrevenue = 0;
            foreach (var transaction in transactions)
            {
                foreach (var line in transaction.TransactionLines)
                {
                    var qty = line.Quantity <= 0 ? 1 : line.Quantity;
                    subtotal += line.Amount * qty;
                    if (line?.Account?.FinancialStatement == FinancialStatement.EXPENSE)
                    {
                        subexpenses += line.Amount * qty;
                    }
                    if (line?.Account?.FinancialStatement == FinancialStatement.LIABILITY)
                    {
                        subliabilities += line.Amount * qty;
                    }
                    if (line?.Account?.FinancialStatement == FinancialStatement.REVENUE)
                    {
                        subrevenue += line.Amount * qty;
                    }
                }
            }
            PeriodTransactionValue = subtotal;
            PeriodExpenses = subexpenses;
            PeriodLiabilities = subliabilities;
            PeriodRevenue = subrevenue;
        }

        protected void OnStartDateChanged(ChangeEventArgs e)
        {
            StartDateString = Convert.ToString(e.Value);
        }

        protected void OnEndDateChanged(ChangeEventArgs e)
        {
            EndDateString = Convert.ToString(e.Value);
        }

        // Toggle visibility of the expense list for a transaction
        protected void ToggleExpenseList(int transactionId)
        {
            if (ExpandedTransactions.Contains(transactionId))
                ExpandedTransactions.Remove(transactionId);
            else
                ExpandedTransactions.Add(transactionId);
        }

        protected bool IsExpenseListVisible(int transactionId)
        {
            return ExpandedTransactions.Contains(transactionId);
        }

        private DateTime? ParseDate(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (DateTime.TryParse(s, out var d)) return d;
            return null;
        }

    }
}
