using FinancialApp.Data.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinancialApp.Components.Accounts;

public class EditAccountBase : ComponentBase
{
    [CascadingParameter]
    protected IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public Account Account { get; set; } = null!;

    [Parameter]
    public IReadOnlyList<Account> ParentAccounts { get; set; } = [];

    protected MudForm _form = null!;
    protected Account _account = new();

    // Only accounts that belong to the selected financial statement and are not the edited account itself
    // can be used as a parent.
    protected IEnumerable<Account> MatchingParentAccounts =>
        ParentAccounts.Where(account =>
            account.Id != Account.Id && account.FinancialStatement == _account.FinancialStatement);

    // The Update button is enabled only once every required input is filled.
    protected bool IsFilled =>
        !string.IsNullOrWhiteSpace(_account.Name)
        && !string.IsNullOrWhiteSpace(_account.Description);

    // True as soon as one of the editable values differs from the original account state.
    protected bool IsModified =>
        !string.Equals(_account.Name, Account.Name, StringComparison.Ordinal)
        || !string.Equals(_account.Description, Account.Description, StringComparison.Ordinal)
        || _account.FinancialStatement != Account.FinancialStatement
        || _account.ParentId != Account.ParentId;

    protected bool CanUpdate => IsFilled && IsModified;

    protected override void OnInitialized()
    {
        // Work on a copy so the account shown in the table is only changed once the update succeeds.
        _account = new Account
        {
            Id = Account.Id,
            Name = Account.Name,
            Description = Account.Description,
            FinancialStatement = Account.FinancialStatement,
            ParentId = Account.ParentId,
            CreatedAt = Account.CreatedAt,
            IsSystem = Account.IsSystem
        };
    }

    protected void Cancel()
    {
        MudDialog.Cancel();
    }

    protected async Task SubmitAsync()
    {
        await _form.ValidateAsync();

        if (!_form.IsValid || !CanUpdate)
        {
            return;
        }

        MudDialog.Close(DialogResult.Ok(_account));
    }

    protected void OnFinancialStatementChanged()
    {
        // A parent must share the account's financial statement, so clear it when the statement changes.
        _account.ParentId = null;
    }
}
