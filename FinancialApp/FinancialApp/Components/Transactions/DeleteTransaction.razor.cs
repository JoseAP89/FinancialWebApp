using FinancialApp.Data.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinancialApp.Components.Transactions;

public class DeleteTransactionBase : ComponentBase
{
    [CascadingParameter]
    protected IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public Transaction Transaction { get; set; } = null!;

    // The card shows the transaction description, so it is the name the confirmation refers to.
    protected string TransactionName => Transaction?.Description ?? "Transaction";

    protected void Cancel()
    {
        MudDialog.Cancel();
    }

    protected void ConfirmDelete()
    {
        MudDialog.Close(DialogResult.Ok(true));
    }
}
