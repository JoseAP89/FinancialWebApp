using FinancialApp.Data.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinancialApp.Components.Accounts;

public class CreateAccountBase : ComponentBase
{
    [CascadingParameter]
    protected IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public IReadOnlyList<Account> ParentAccounts { get; set; } = [];

    protected MudForm _form = null!;
    protected Account _account = new();

    protected void Cancel()
    {
        MudDialog.Cancel();
    }

    protected async Task SubmitAsync()
    {
        await _form.Validate();

        if (_form.IsValid)
        {
            MudDialog.Close(DialogResult.Ok(_account));
        }
    }
}
