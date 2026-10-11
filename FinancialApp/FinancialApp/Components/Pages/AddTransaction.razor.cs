using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
// using System.Timers; (use fully-qualified name to avoid ambiguity with System.Threading.Timer)
using Microsoft.Extensions.Logging;
using FinancialApp.Data.Models;
using AccountModel = FinancialApp.Data.Models.Account;
using FinancialApp.Core.Data.Repositories;
using FinancialApp.Core.DTOs;
using FinancialApp.Infrastructure.Services;
using FinancialApp.Core.Services;

namespace FinancialApp.Components.Pages
{
    public partial class AddTransaction : ComponentBase
    {
        [Inject]
        protected ITransactionBalancingService TransactionBalancingService { get; set; } = null!;

        [Inject]
        protected IAccountRepository AccountRepository { get; set; } = null!;

        [Inject]
        protected ILogger<AddTransaction> Logger { get; set; } = null!;

        [Inject]
        protected ITransactionRepository TransactionRepository { get; set; } = null!;

        [Inject]
        protected ITransactionLineRepository TransactionLineRepository { get; set; } = null!;

        [Inject]
        protected IToastService ToastService { get; set; } = null!;


        protected List<AccountModel> ParentAccounts { get; set; } = [];

        protected List<AccountModel> ChildAccounts { get; set; } = [];

        private string TransactionDescription { get; set; } = string.Empty;

        protected bool IsLoading { get; set; }

        protected string? ErrorMessage { get; set; }

        protected override async Task OnInitializedAsync()
        {
            IsLoading = true;
            try
            {
                var parents = await AccountRepository.GetVisibleParentAccountsAsync();
                ParentAccounts = parents?.ToList() ?? new List<AccountModel>();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Unable to load accounts. Please check the database connection.";
                Logger?.LogError(ex, "Failed to load parent accounts in PersonalFinance component.");
                ParentAccounts = new List<AccountModel>();
            }
            finally
            {
                IsLoading = false;
            }
        }

        protected async Task OnChildChangedForLine(ChangeEventArgs e, TransactionLineDTO line)
        {
            if (int.TryParse(Convert.ToString(e.Value), out var id))
            {
                line.SelectedChildId = id;
                line.SelectedChild = await AccountRepository.GetByIdAsync(id);
            }
            else
            {
                line.SelectedChildId = null;
                line.SelectedChild = null;
                // hide any child popover and stop its timer when selection cleared
                if (line.ChildPopoverTimer is not null)
                {
                    try { line.ChildPopoverTimer.Stop(); line.ChildPopoverTimer.Dispose(); } catch { }
                    line.ChildPopoverTimer = null;
                }
                line.IsChildPopoverVisible = false;
            }

            // Clear validation display when the user edits inputs
            ClearValidation();
            await InvokeAsync(StateHasChanged);
        }

        protected List<TransactionLineDTO> TransactionLines { get; set; } = new List<TransactionLineDTO>();

        protected Transaction CurrentTransaction { get; set; } = new Transaction();

        // Controls whether validation errors are shown after the user attempts to submit
        protected bool ShowValidation { get; set; }

        protected void ClearValidation()
        {
            ShowValidation = false;
            _ = InvokeAsync(StateHasChanged);
        }

        // Validation: only allow submit when transaction description present, at least one line,
        // and every line has selected parent & child account and amount > 0
        protected bool CanSubmit =>
            !string.IsNullOrWhiteSpace(TransactionDescription)
            && TransactionLines is not null
            && TransactionLines.Any()
            && TransactionLines.All(l => 
                l.SelectedParentId.HasValue && 
                l.SelectedChildId.HasValue && 
                !string.IsNullOrWhiteSpace(l.Description) &&
                l.Amount > 0 &&
                l.Quantity >= 1);

        protected IEnumerable<string> GetValidationErrors()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(TransactionDescription))
            {
                errors.Add("Transaction description is required.");
            }

            if (TransactionLines is null || !TransactionLines.Any())
            {
                errors.Add("At least one transaction line is required.");
                return errors;
            }

            for (var i = 0; i < TransactionLines.Count; i++)
            {
                var l = TransactionLines[i];
                var lineIndex = i + 1;

                if (!l.SelectedParentId.HasValue)
                {
                    errors.Add($"Line {lineIndex}: account (parent) must be selected.");
                }

                if (!l.SelectedChildId.HasValue)
                {
                    errors.Add($"Line {lineIndex}: sub-account must be selected.");
                }

                if (string.IsNullOrWhiteSpace(l.Description))
                {
                    errors.Add($"Line {lineIndex}: description must be selected.");
                }

                if (l.Amount <= 0)
                {
                    errors.Add($"Line {lineIndex}: amount must be greater than zero.");
                }

                if (l.Quantity < 1)
                {
                    errors.Add($"Line {lineIndex}: quantity must be at least 1.");
                }
            }

            return errors;
        }

        protected void AddTransactionLine()
        {
            TransactionLines.Add(new TransactionLineDTO());
            ClearValidation();
        }

        protected void RemoveTransactionLine(TransactionLineDTO line)
        {
            // dispose any timers associated with the line to avoid leaks
            if (line.ParentPopoverTimer is not null)
            {
                try { line.ParentPopoverTimer.Stop(); line.ParentPopoverTimer.Dispose(); } catch { }
                line.ParentPopoverTimer = null;
            }
            if (line.ChildPopoverTimer is not null)
            {
                try { line.ChildPopoverTimer.Stop(); line.ChildPopoverTimer.Dispose(); } catch { }
                line.ChildPopoverTimer = null;
            }
            TransactionLines.Remove(line);
            ClearValidation();
        }

        protected async Task OnParentChangedForLine(ChangeEventArgs e, TransactionLineDTO line)
        {
            if (int.TryParse(Convert.ToString(e.Value), out var parentId))
            {
                line.SelectedParentId = parentId;
                // When the parent account changes, clear the previously selected sub-account
                // so the user must explicitly choose a new sub-account for this line.
                line.SelectedChildId = null;
                line.SelectedChild = null;
                // Also hide any child popover and stop its timer
                if (line.ChildPopoverTimer is not null)
                {
                    try { line.ChildPopoverTimer.Stop(); line.ChildPopoverTimer.Dispose(); } catch { }
                    line.ChildPopoverTimer = null;
                }
                line.IsChildPopoverVisible = false;
                await LoadChildAccountsForLineAsync(line, parentId);
            }
            else
            {
                line.SelectedParentId = null;
                line.ChildAccounts = new List<AccountModel>();
                line.SelectedChildId = null;
                line.SelectedChild = null;
                // hide both popovers and stop timers
                if (line.ParentPopoverTimer is not null)
                {
                    try { line.ParentPopoverTimer.Stop(); line.ParentPopoverTimer.Dispose(); } catch { }
                    line.ParentPopoverTimer = null;
                }
                line.IsParentPopoverVisible = false;

                if (line.ChildPopoverTimer is not null)
                {
                    try { line.ChildPopoverTimer.Stop(); line.ChildPopoverTimer.Dispose(); } catch { }
                    line.ChildPopoverTimer = null;
                }
                line.IsChildPopoverVisible = false;
            }

            // Clear validation display when the user edits inputs
            ClearValidation();
            await InvokeAsync(StateHasChanged);
        }

        // Toggle parent popover visibility. Clicking again will close. Auto-closes after 12s.
        protected void ToggleParentPopover(TransactionLineDTO line)
        {
            if (line.IsParentPopoverVisible)
            {
                // close
                line.IsParentPopoverVisible = false;
                if (line.ParentPopoverTimer is not null)
                {
                    try { line.ParentPopoverTimer.Stop(); line.ParentPopoverTimer.Dispose(); } catch { }
                    line.ParentPopoverTimer = null;
                }
            }
            else
            {
                // open
                line.IsParentPopoverVisible = true;
                // ensure previous timer disposed
                if (line.ParentPopoverTimer is not null)
                {
                    try { line.ParentPopoverTimer.Stop(); line.ParentPopoverTimer.Dispose(); } catch { }
                    line.ParentPopoverTimer = null;
                }
                var t = new System.Timers.Timer(12000) { AutoReset = false };
                t.Elapsed += async (s, e) =>
                {
                    try
                    {
                        t.Stop();
                        t.Dispose();
                    }
                    catch { }
                    line.ParentPopoverTimer = null;
                    line.IsParentPopoverVisible = false;
                    await InvokeAsync(StateHasChanged);
                };
                line.ParentPopoverTimer = t;
                t.Start();
            }
        }

        // Toggle child popover visibility. Clicking again will close. Auto-closes after 12s.
        protected void ToggleChildPopover(TransactionLineDTO line)
        {
            if (line.IsChildPopoverVisible)
            {
                line.IsChildPopoverVisible = false;
                if (line.ChildPopoverTimer is not null)
                {
                    try { line.ChildPopoverTimer.Stop(); line.ChildPopoverTimer.Dispose(); } catch { }
                    line.ChildPopoverTimer = null;
                }
            }
            else
            {
                line.IsChildPopoverVisible = true;
                if (line.ChildPopoverTimer is not null)
                {
                    try { line.ChildPopoverTimer.Stop(); line.ChildPopoverTimer.Dispose(); } catch { }
                    line.ChildPopoverTimer = null;
                }
                var t = new System.Timers.Timer(12000) { AutoReset = false };
                t.Elapsed += async (s, e) =>
                {
                    try
                    {
                        t.Stop();
                        t.Dispose();
                    }
                    catch { }
                    line.ChildPopoverTimer = null;
                    line.IsChildPopoverVisible = false;
                    await InvokeAsync(StateHasChanged);
                };
                line.ChildPopoverTimer = t;
                t.Start();
            }
        }

        private string GetParentDescription(TransactionLineDTO line)
        {
            if (!line.SelectedParentId.HasValue) return string.Empty;
            var acct = ParentAccounts?.FirstOrDefault(a => a.Id == line.SelectedParentId.Value);
            return acct?.Description ?? string.Empty;
        }

        private string GetChildDescription(TransactionLineDTO line)
        {
            if (!line.SelectedChildId.HasValue) return string.Empty;
            var acct = line.ChildAccounts?.FirstOrDefault(a => a.Id == line.SelectedChildId.Value);
            return acct?.Description ?? string.Empty;
        }

        private async Task LoadChildAccountsForLineAsync(TransactionLineDTO line, int parentId)
        {
            try
            {
                var children = await AccountRepository.GetVisibleChildAccountsByParentIdAsync(parentId);
                line.ChildAccounts = children?.ToList() ?? new List<AccountModel>();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Failed to load child accounts for parent id {ParentId} in line.", parentId);
                line.ChildAccounts = new List<AccountModel>();
            }
        }

        // Helper: map the transaction header inputs into a Transaction model. The lines are not part of
        // this graph: they are inserted one by one once the id and the auto-balance lineage are known.
        protected Transaction BuildTransactionHeader()
        {
            return new Transaction
            {
                Description = TransactionDescription,
                Date = DateTime.UtcNow
            };
        }


        protected async Task CreateTransaction()
        {
            // Show validation errors after the user clicked submit
            ShowValidation = true;

            if (!CanSubmit)
            {
                Logger?.LogWarning("CreateTransaction aborted: validation failed. Errors: {Errors}", string.Join("; ", GetValidationErrors()));
                return;
            }

            // The header goes in first: the compensating entries written by the balancing service and the
            // user lines both need a real TransactionId before they can be persisted.
            var tx = BuildTransactionHeader();

            try
            {
                IsLoading = true;

                await TransactionRepository.AddAsync(tx);
                await TransactionRepository.SaveChangesAsync();

                // Auto-balance: this persists the compensating entries against the new transaction and
                // records on each compensated line the real database id of the entry that offsets it.
                TransactionLines = await TransactionBalancingService.BalanceTransactionAsync(tx.Id, TransactionLines, Logger);

                // Persist the user lines against that transaction, carrying the auto-balance lineage.
                foreach (TransactionLineDTO line in TransactionLines)
                {
                    if (!line.SelectedChildId.HasValue || Math.Abs(line.Amount) <= 0.01m)
                    {
                        continue;
                    }

                    await TransactionLineRepository.AddAsync(new TransactionLine
                    {
                        TransactionId = tx.Id,
                        AccountId = line.SelectedChildId.Value,
                        Amount = line.Amount,
                        Description = line.Description,
                        Quantity = line.Quantity,
                        AutoBalancedByLineId = line.AutoBalancedByLineId
                    });
                }

                await TransactionLineRepository.SaveChangesAsync();

                Logger?.LogDebug("CreateTransaction: persisted transaction {TransactionId} with {LineCount} line(s).", tx.Id, TransactionLines.Count);

                // Show a success toast through the toast service
                ToastService.ShowSuccess("Transaction added successfully");
                ClearTransactionPageState();
            }
            catch (ArgumentException e)
            {
                await DiscardTransactionAsync(tx);
                ToastService.ShowError(e.Message);
            }
            catch (Exception ex)
            {
                await DiscardTransactionAsync(tx);
                Logger?.LogError(ex, "CreateTransaction failed while saving transaction.");
                ToastService.ShowError("Failed to add transaction");
            }
            finally
            {
                IsLoading = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        // Discards the transaction header (and, by cascade, every auto-balance entry and line written
        // against it) when balancing or a line insert fails, so a rejected submit leaves nothing behind.
        private async Task DiscardTransactionAsync(Transaction tx)
        {
            try
            {
                if (tx.Id == 0)
                {
                    return;
                }

                TransactionRepository.Remove(tx);
                await TransactionRepository.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "CreateTransaction failed to roll back transaction id {TransactionId}.", tx.Id);
            }
        }

        protected void ClearTransactionPageState()
        {
            // Reset UI state
            ClearValidation();
            TransactionLines.Clear();
            TransactionDescription = string.Empty;
            CurrentTransaction = new Transaction();
        }

        protected void OnTransactionDescriptionChanged(ChangeEventArgs e)
        {
            TransactionDescription = Convert.ToString(e.Value) ?? string.Empty;
            ClearValidation();
        }

        protected async Task OnLineDescriptionChanged(ChangeEventArgs e, TransactionLineDTO line)
        {
            line.Description = Convert.ToString(e.Value);
            ClearValidation();
            await InvokeAsync(StateHasChanged);
        }

        protected async Task OnLineAmountChanged(ChangeEventArgs e, TransactionLineDTO line)
        {
            var s = Convert.ToString(e.Value);
            if (decimal.TryParse(s, out var amount))
            {
                line.Amount = amount;
            }
            else
            {
                line.Amount = 0;
            }

            ClearValidation();
            await InvokeAsync(StateHasChanged);
        }

        protected async Task OnLineQuantityChanged(ChangeEventArgs e, TransactionLineDTO line)
        {
            var s = Convert.ToString(e.Value);
            if (int.TryParse(s, out var quantity))
            {
                line.Quantity = quantity;
            }
            else
            {
                line.Quantity = 1;
            }

            ClearValidation();
            await InvokeAsync(StateHasChanged);
        }
        
        protected async Task OnLinePaidWithCreditCardChanged(ChangeEventArgs e, TransactionLineDTO line)
        {
            line.PaidWithCreditCard = Convert.ToBoolean(e.Value);
            ClearValidation();
            await InvokeAsync(StateHasChanged);
        }

        protected async Task OnLineLiabilityActionChanged(ChangeEventArgs e, TransactionLineDTO line)
        {
            line.LiabilityAction = Convert.ToInt32(e.Value);
            ClearValidation();
            await InvokeAsync(StateHasChanged);
        }

        protected bool IsQuantityValid(TransactionLineDTO line)
        {
            return line.SelectedChild is not null && (
                line.SelectedChild.FinancialStatement == Data.Models.FinancialStatement.ASSET ||
                line.SelectedChild.FinancialStatement == Data.Models.FinancialStatement.LIABILITY ||
                line.SelectedChild.FinancialStatement == Data.Models.FinancialStatement.REVENUE
            );
        }
    }
}
