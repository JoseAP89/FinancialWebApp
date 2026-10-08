using FinancialApp.Core.Data.Repositories;
using AccountModel = FinancialApp.Data.Models.Account;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinancialApp.Components.Pages;

public class AccountBase : ComponentBase
{
    [Inject]
    protected IAccountRepository AccountRepository { get; set; } = null!;

    [Inject]
    protected ILogger<AccountBase> Logger { get; set; } = null!;

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
}
