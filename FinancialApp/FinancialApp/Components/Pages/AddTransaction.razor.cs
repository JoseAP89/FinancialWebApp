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
        protected IToastService ToastService { get; set; } = null!;


        private AccountModel CreditCard { get; set; } = null!;

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
                CreditCard = await AccountRepository.GetByNameAsync("Credit Card") ?? new AccountModel();
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

        // Helper: map current UI inputs into a Transaction model
        protected Transaction BuildTransactionFromInputs()
        {
            var tx = new Transaction
            {
                Description = TransactionDescription,
                Date = DateTime.UtcNow,
                TransactionLines = []
            };

            foreach (TransactionLineDTO line in TransactionLines)
            {
                if (line.SelectedChildId.HasValue && Math.Abs(line.Amount) > 0.01m)
                {
                    tx.TransactionLines.Add(new TransactionLine
                    {
                        AccountId = line.SelectedChildId.Value,
                        Amount = line.Amount,
                        Description = line.Description,
                        Quantity = line.Quantity,
                        IsAutoBalanced = line.IsAutoBalanced
                    });
                }
            }

            return tx;
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
            try
            {
                TransactionLines = await TransactionBalancingService.BalanceTransactionAsync(TransactionLines, Logger);
            }
            catch (ArgumentException e)
            {
                ToastService.ShowError(e.Message);
                return;
            }
            catch (Exception)
            {
                ToastService.ShowError("Balance transaction operation failed");
                return;
            }
            var tx = BuildTransactionFromInputs();
            Logger?.LogDebug("CreateTransaction debug: {@Transaction}", tx);

            try
            {
                IsLoading = true;

                // Persist transaction (EF will cascade insert TransactionLines attached to the Transaction)
                await TransactionRepository.AddAsync(tx);
                await TransactionRepository.SaveChangesAsync();

                // Show success toast (JS alert fallback)
                ToastService.ShowSuccess("Transaction added successfully");
                ClearTransactionPageState();

            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "CreateTransaction failed while saving transaction.");
                ToastService.ShowError("Failed to add transaction");
            }
            finally
            {
                IsLoading = false;
                await InvokeAsync(StateHasChanged);
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
