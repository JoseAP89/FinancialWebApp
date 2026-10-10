using FinancialApp.Data.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinancialApp.Components.Accounts;

public class DeleteAccountBase : ComponentBase
{
    [CascadingParameter]
    protected IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public Account Account { get; set; } = null!;

    protected string AccountName => Account?.Name ?? string.Empty;

    protected void Cancel()
    {
        MudDialog.Cancel();
    }

    protected void ConfirmDelete()
    {
        MudDialog.Close(DialogResult.Ok(true));
    }
}
