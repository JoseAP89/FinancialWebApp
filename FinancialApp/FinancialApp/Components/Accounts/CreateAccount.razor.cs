using FinancialApp.Core.Data.Repositories;
using FinancialApp.Data.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinancialApp.Components.Accounts;

public class CreateAccountBase : ComponentBase
{
    [Inject]
    protected IAccountRepository AccountRepository { get; set; } = null!;

    [CascadingParameter]
    protected IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public IReadOnlyList<Account> ParentAccounts { get; set; } = [];

    protected MudForm _form = null!;
    protected Account _account = new();
    private string? _nameValidationError;

    protected void Cancel()
    {
        MudDialog.Cancel();
    }

    protected async Task SubmitAsync()
    {
        await _form.Validate();

        if (!_form.IsValid)
        {
            return;
        }

        var existingAccount = await AccountRepository.GetByNameAsync(_account.Name);
        if (existingAccount is not null)
        {
            _nameValidationError = "An account with that name already exists";
            await _form.Validate();
            return;
        }

        MudDialog.Close(DialogResult.Ok(_account));
    }

    protected string? ValidateName(string name)
    {
        return _nameValidationError;
    }

    protected void ClearNameValidationError()
    {
        _nameValidationError = null;
    }
}
