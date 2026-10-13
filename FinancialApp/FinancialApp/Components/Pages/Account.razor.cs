using FinancialApp.Core.Data.Repositories;
using AccountModel = FinancialApp.Data.Models.Account;
using FinancialApp.Components.Accounts;
using FinancialApp.Infrastructure.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;
using FinancialApp.Data.Models;

namespace FinancialApp.Components.Pages;

public class AccountBase : ComponentBase
{
    // The different ways the account table can be filtered.
    protected enum AccountFilterMode
    {
        None,
        FinancialStatement,
        NumberOfLines
    }

    // The comparison applied to the transaction line count when filtering by number of lines.
    protected enum LineCountComparison
    {
        GreaterThan,
        EqualTo,
        LessThan
    }

    [Inject]
    protected IAccountRepository AccountRepository { get; set; } = null!;

    [Inject]
    protected ILogger<AccountBase> Logger { get; set; } = null!;

    [Inject]
    protected IDialogService DialogService { get; set; } = null!;

    [Inject]
    protected IToastService ToastService { get; set; } = null!;

    protected IReadOnlyList<AccountModel> Accounts { get; private set; } = [];
    protected IReadOnlyDictionary<int, int> TransactionLineCounts { get; private set; } =
        new Dictionary<int, int>();
    protected string SearchString { get; private set; } = string.Empty;
    protected AccountFilterMode FilterMode { get; private set; } = AccountFilterMode.None;
    protected FinancialStatement? FilterFinancialStatement { get; private set; }
    protected LineCountComparison FilterLineCountComparison { get; private set; } = LineCountComparison.EqualTo;
    protected int? FilterLineCount { get; private set; }
    protected bool IsLoading { get; private set; }
    protected string? ErrorMessage { get; private set; }

    protected IEnumerable<AccountModel> FilteredAccounts
    {
        get
        {
            var filteredAccounts = Accounts.Where(MatchesSearch);

            switch (FilterMode)
            {
                case AccountFilterMode.FinancialStatement when FilterFinancialStatement.HasValue:
                    filteredAccounts = filteredAccounts
                        .Where(account => account.FinancialStatement == FilterFinancialStatement.Value);
                    break;
                case AccountFilterMode.NumberOfLines when FilterLineCount.HasValue:
                    filteredAccounts = filteredAccounts
                        .Where(account => MatchesLineCount(GetTransactionLineCount(account.Id)));
                    break;
            }

            return filteredAccounts;
        }
    }

    private bool MatchesSearch(AccountModel account) =>
        string.IsNullOrWhiteSpace(SearchString)
        || account.Id.ToString().Contains(SearchString, StringComparison.OrdinalIgnoreCase)
        || account.Name.Contains(SearchString, StringComparison.OrdinalIgnoreCase)
        || account.Description.Contains(SearchString, StringComparison.OrdinalIgnoreCase)
        || account.FinancialStatement.ToString().Contains(SearchString, StringComparison.OrdinalIgnoreCase)
        || (account.ParentId?.ToString().Contains(SearchString, StringComparison.OrdinalIgnoreCase) ?? false)
        || account.CreatedAt.ToString("g").Contains(SearchString, StringComparison.OrdinalIgnoreCase);

    private bool MatchesLineCount(int lineCount)
    {
        var target = FilterLineCount ?? 0;

        return FilterLineCountComparison switch
        {
            LineCountComparison.GreaterThan => lineCount > target,
            LineCountComparison.LessThan => lineCount < target,
            _ => lineCount == target
        };
    }

    protected override async Task OnInitializedAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            Accounts = (await AccountRepository.ListAsync())
                .OrderBy(account => account.Name)
                .ToList();

            TransactionLineCounts = await LoadTransactionLineCountsAsync(Accounts);
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Failed to load accounts for the Accounts page.");
            ErrorMessage = "Unable to load accounts. Please try again later.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected int GetTransactionLineCount(int accountId)
    {
        // Accounts without an entry (for example a newly created one) have no transaction lines.
        return TransactionLineCounts.TryGetValue(accountId, out var count) ? count : 0;
    }

    private async Task<IReadOnlyDictionary<int, int>> LoadTransactionLineCountsAsync(IEnumerable<AccountModel> accounts)
    {
        var counts = new Dictionary<int, int>();

        foreach (var account in accounts)
        {
            counts[account.Id] = await AccountRepository.GetTransactionLineCountByAccountIdAsync(account.Id);
        }

        return counts;
    }

    protected void OnSearchChanged(string? searchString)
    {
        SearchString = searchString ?? string.Empty;
    }

    protected void OnFilterModeChanged(AccountFilterMode filterMode)
    {
        FilterMode = filterMode;

        // Reset the mode-specific inputs when leaving their mode so a stale value
        // cannot silently keep filtering the table.
        if (filterMode != AccountFilterMode.FinancialStatement)
        {
            FilterFinancialStatement = null;
        }

        if (filterMode != AccountFilterMode.NumberOfLines)
        {
            FilterLineCountComparison = LineCountComparison.EqualTo;
            FilterLineCount = null;
        }
    }

    protected void OnFilterFinancialStatementChanged(FinancialStatement? financialStatement)
    {
        FilterFinancialStatement = financialStatement;
    }

    protected void OnLineCountComparisonChanged(LineCountComparison comparison)
    {
        FilterLineCountComparison = comparison;
    }

    protected void OnLineCountChanged(int? lineCount)
    {
        // The number of transaction lines can never be negative, so clamp the
        // filter value to zero when the user enters something below it.
        FilterLineCount = lineCount is < 0 ? 0 : lineCount;
    }

    protected async Task OpenCreateAccountDialogAsync()
    {
        var parameters = new DialogParameters
        {
            [nameof(CreateAccountBase.ParentAccounts)] = Accounts
                .OrderBy(account => account.Name)
                .ToList()
        };
        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Small,
            FullWidth = true
        };

        var dialog = await DialogService.ShowAsync<CreateAccount>("Create account", parameters, options);
        var result = await dialog.Result;

        if (result is null || result.Canceled || result.Data is not AccountModel account)
        {
            return;
        }

        try
        {
            await AccountRepository.AddAsync(account);
            await AccountRepository.SaveChangesAsync();
            Accounts = Accounts
                .Append(account)
                .OrderBy(existingAccount => existingAccount.Name)
                .ToList();

            // A freshly created account cannot have transaction lines yet.
            TransactionLineCounts = new Dictionary<int, int>(TransactionLineCounts)
            {
                [account.Id] = 0
            };
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Failed to create an account.");
            ToastService.ShowError("Unable to create the account. Please try again.");
        }
    }

    protected async Task OpenEditAccountDialogAsync(AccountModel account)
    {
        var parameters = new DialogParameters
        {
            [nameof(EditAccountBase.Account)] = account,
            [nameof(EditAccountBase.ParentAccounts)] = Accounts
                .OrderBy(existingAccount => existingAccount.Name)
                .ToList()
        };
        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Small,
            FullWidth = true
        };

        var dialog = await DialogService.ShowAsync<EditAccount>("Edit account", parameters, options);
        var result = await dialog.Result;

        // The user cancelled the operation, did not change anything, or closed the dialog.
        if (result is null || result.Canceled || result.Data is not AccountModel updatedAccount)
        {
            return;
        }

        try
        {
            await AccountRepository.UpdateAccountByIdAsync(
                account.Id,
                updatedAccount.Name,
                updatedAccount.Description,
                updatedAccount.FinancialStatement,
                updatedAccount.ParentId);

            var refreshedAccount = new AccountModel
            {
                Id = account.Id,
                Name = updatedAccount.Name,
                Description = updatedAccount.Description,
                FinancialStatement = updatedAccount.FinancialStatement,
                ParentId = updatedAccount.ParentId,
                CreatedAt = account.CreatedAt,
                IsSystem = account.IsSystem
            };

            Accounts = Accounts
                .Select(existingAccount => existingAccount.Id == account.Id ? refreshedAccount : existingAccount)
                .OrderBy(existingAccount => existingAccount.Name)
                .ToList();

            ToastService.ShowSuccess($"Account {refreshedAccount.Name} was updated successfully");
        }
        catch (InvalidOperationException exception)
        {
            // The account could not be updated because it breaks one of the account rules
            // (it has transaction lines or children accounts, a duplicate name, or an invalid parent).
            ToastService.ShowError(exception.Message);
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Failed to update account with id {AccountId}.", account.Id);
            ToastService.ShowError("Unable to update the account. Please try again.");
        }
    }

    protected async Task OpenDeleteAccountDialogAsync(AccountModel account)
    {
        var parameters = new DialogParameters
        {
            [nameof(DeleteAccountBase.Account)] = account
        };
        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Small,
            FullWidth = true
        };

        var dialog = await DialogService.ShowAsync<DeleteAccount>("Delete account", parameters, options);
        var result = await dialog.Result;

        // The user cancelled the operation (Cancel button, Escape key or clicking away).
        if (result is null || result.Canceled)
        {
            return;
        }

        try
        {
            await AccountRepository.DeleteAccountByIdAsync(account.Id);

            Accounts = Accounts
                .Where(existingAccount => existingAccount.Id != account.Id)
                .ToList();

            TransactionLineCounts = TransactionLineCounts
                .Where(pair => pair.Key != account.Id)
                .ToDictionary(pair => pair.Key, pair => pair.Value);

            ToastService.ShowSuccess($"Account {account.Name} was deleted successfully");
        }
        catch (InvalidOperationException exception)
        {
            // The account could not be deleted because it still has transaction lines associated with it.
            ToastService.ShowError(exception.Message);
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Failed to delete account with id {AccountId}.", account.Id);
            ToastService.ShowError("Unable to delete the account. Please try again.");
        }
    }
}
