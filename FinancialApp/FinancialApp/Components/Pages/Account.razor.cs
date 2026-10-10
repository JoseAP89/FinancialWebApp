using FinancialApp.Core.Data.Repositories;
using AccountModel = FinancialApp.Data.Models.Account;
using FinancialApp.Components.Accounts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinancialApp.Components.Pages;

public class AccountBase : ComponentBase
{
    [Inject]
    protected IAccountRepository AccountRepository { get; set; } = null!;

    [Inject]
    protected ILogger<AccountBase> Logger { get; set; } = null!;

    [Inject]
    protected IDialogService DialogService { get; set; } = null!;

    [Inject]
    protected ISnackbar Snackbar { get; set; } = null!;

    protected IReadOnlyList<AccountModel> Accounts { get; private set; } = [];
    protected string SearchString { get; private set; } = string.Empty;
    protected bool IsLoading { get; private set; }
    protected string? ErrorMessage { get; private set; }

    protected IEnumerable<AccountModel> FilteredAccounts =>
        Accounts.Where(account =>
            string.IsNullOrWhiteSpace(SearchString)
            || account.Id.ToString().Contains(SearchString, StringComparison.OrdinalIgnoreCase)
            || account.Name.Contains(SearchString, StringComparison.OrdinalIgnoreCase)
            || account.Description.Contains(SearchString, StringComparison.OrdinalIgnoreCase)
            || account.FinancialStatement.ToString().Contains(SearchString, StringComparison.OrdinalIgnoreCase)
            || (account.ParentId?.ToString().Contains(SearchString, StringComparison.OrdinalIgnoreCase) ?? false)
            || account.CreatedAt.ToString("g").Contains(SearchString, StringComparison.OrdinalIgnoreCase));

    protected override async Task OnInitializedAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            Accounts = (await AccountRepository.ListAsync())
                .OrderBy(account => account.Name)
                .ToList();
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

    protected void OnSearchChanged(string? searchString)
    {
        SearchString = searchString ?? string.Empty;
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
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Failed to create an account.");
            Snackbar.Add("Unable to create the account. Please try again.", Severity.Error);
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

            Snackbar.Add($"Account {account.Name} was deleted successfully", Severity.Success);
        }
        catch (InvalidOperationException exception)
        {
            // The account could not be deleted because it still has transaction lines associated with it.
            Snackbar.Add(exception.Message, Severity.Error);
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Failed to delete account with id {AccountId}.", account.Id);
            Snackbar.Add("Unable to delete the account. Please try again.", Severity.Error);
        }
    }
}
