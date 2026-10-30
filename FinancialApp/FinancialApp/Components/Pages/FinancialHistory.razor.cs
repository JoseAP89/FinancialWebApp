using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using FinancialApp.Data.Models;
using FinancialApp.Core.Data.Repositories;
using FinancialApp.Core.DTOs;
using FinancialApp.Components.Transactions;
using FinancialApp.Infrastructure.Services;
using MudBlazor;

namespace FinancialApp.Components.Pages
{
    public class FinancialHistoryBase : ComponentBase
    {
        [Inject]
        protected ITransactionRepository TransactionRepository { get; set; } = null!;

        [Inject]
        protected ILogger<FinancialHistoryBase> Logger { get; set; } = null!;

        [Inject]
        protected IDialogService DialogService { get; set; } = null!;

        [Inject]
        protected IToastService ToastService { get; set; } = null!;

        protected IEnumerable<Transaction> FilteredTransactions { get; set; } = Enumerable.Empty<Transaction>();

        protected bool _tableView { get; set; } = true;
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

        // Opens the dialog that lets the user edit a transaction and its lines and, when the user accepts
        // the edit, persists it through the repositories. The repositories also realign the auto-balance
        // entries compensating the edited lines, so the range is reloaded afterwards to show what was
        // actually stored.
        protected async Task OpenEditTransactionDialogAsync(Transaction transaction)
        {
            var parameters = new DialogParameters
            {
                [nameof(EditTransactionBase.Transaction)] = transaction
            };
            var options = new DialogOptions
            {
                MaxWidth = MaxWidth.Small,
                FullWidth = true
            };

            var dialog = await DialogService.ShowAsync<EditTransaction>("Edit transaction", parameters, options);
            var result = await dialog.Result;

            // No result, a cancelled dialog or an unexpected payload means there is nothing to persist.
            if (result is null || result.Canceled || result.Data is not Transaction edited)
            {
                return;
            }

            try
            {
                IsLoading = true;

                await TransactionRepository.UpdateTransactionWithLinesAsync(edited);

                ToastService.ShowSuccess("Transaction updated successfully");

                // Reload the range so the cards, the totals and the table show the stored values.
                await ApplyFilter();
            }
            catch (InvalidOperationException ex)
            {
                Logger?.LogWarning(ex, "UpdateTransaction failed for transaction {TransactionId}.", edited.Id);
                ToastService.ShowError(ex.Message);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "UpdateTransaction failed for transaction {TransactionId}.", edited.Id);
                ToastService.ShowError("Failed to update transaction");
            }
            finally
            {
                IsLoading = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        // Opens the confirmation dialog that lets the user delete a transaction card and, when the user
        // confirms, deletes it through the repository. The repository removes the transaction together
        // with every line stored against it, the user lines as well as the auto-balance entries that
        // compensate them, so the range is reloaded afterwards to show what is actually left.
        protected async Task OpenDeleteTransactionDialogAsync(Transaction transaction)
        {
            var parameters = new DialogParameters
            {
                [nameof(DeleteTransactionBase.Transaction)] = transaction
            };
            var options = new DialogOptions
            {
                MaxWidth = MaxWidth.Small,
                FullWidth = true
            };

            var dialog = await DialogService.ShowAsync<DeleteTransaction>("Delete transaction", parameters, options);
            var result = await dialog.Result;

            // No result or a cancelled dialog means the user kept the transaction, so there is nothing to do.
            if (result is null || result.Canceled)
            {
                return;
            }

            try
            {
                IsLoading = true;

                await TransactionRepository.DeleteTransactionWithLinesAsync(transaction.Id);

                // The card is gone, so its expanded/collapsed state would only leak if it were kept.
                ExpandedTransactions.Remove(transaction.Id);

                ToastService.ShowSuccess("Transaction deleted successfully");

                // Reload the range so the totals and the table no longer count the deleted transaction.
                await ApplyFilter();
            }
            catch (InvalidOperationException ex)
            {
                Logger?.LogWarning(ex, "DeleteTransaction failed for transaction {TransactionId}.", transaction.Id);
                ToastService.ShowError(ex.Message);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "DeleteTransaction failed for transaction {TransactionId}.", transaction.Id);
                ToastService.ShowError("Failed to delete transaction");
            }
            finally
            {
                IsLoading = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        protected TableGroupDefinition<FlatTransactionRow> TransactionGroupDefinition = new()
        {
            GroupName = "Transaction",
            Indentation = true,
            Expandable = false,
            Selector = row => row.Transaction.Id
        };

        protected IEnumerable<FlatTransactionRow> FlatTransactions =>
            (FilteredTransactions ?? Enumerable.Empty<Transaction>())
                .OrderByDescending(transaction => transaction.Date)
                .SelectMany(transaction => transaction.TransactionLines.Select(line =>
                    new FlatTransactionRow(transaction, line)));

        protected sealed record FlatTransactionRow(Transaction Transaction, TransactionLine Line)
        {
            public int Quantity => Line.Quantity <= 0 ? 1 : Line.Quantity;

            public decimal LineTotal => Line.Amount * Quantity;
        }

        private DateTime? ParseDate(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (DateTime.TryParse(s, out var d)) return d;
            return null;
        }

    }
}
