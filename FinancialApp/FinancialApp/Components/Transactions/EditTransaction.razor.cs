using FinancialApp.Data.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinancialApp.Components.Transactions;

public class EditTransactionBase : ComponentBase
{
    [CascadingParameter]
    protected IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public Transaction Transaction { get; set; } = null!;

    protected MudForm _form = null!;
    protected Transaction _transaction = new();
    protected List<TransactionLine> _lines = [];

    // Total of every line, matching the TOTAL shown on the financial history card.
    // It is a computed property, so editing a line's amount (or quantity) refreshes it on re-render.
    protected decimal TotalTransactionAmount =>
        _lines.Sum(line => line.Amount * (line.Quantity <= 0 ? 1 : line.Quantity));

    protected override void OnInitialized()
    {
        // Work on a copy so the transaction shown in the card is only changed once the edit succeeds.
        _transaction = new Transaction
        {
            Id = Transaction.Id,
            Description = Transaction.Description,
            Date = Transaction.Date
        };

        // Copy each line so the dialog is prefilled with the original data.
        _lines = Transaction.TransactionLines
            .Select(line => new TransactionLine
            {
                Id = line.Id,
                TransactionId = line.TransactionId,
                AccountId = line.AccountId,
                Amount = line.Amount,
                Quantity = line.Quantity,
                Description = line.Description,
                AutoBalancedByLineId = line.AutoBalancedByLineId
            })
            .ToList();
    }

    protected void Cancel()
    {
        MudDialog.Cancel();
    }

    protected async Task SubmitAsync()
    {
        await _form.ValidateAsync();

        if (!_form.IsValid)
        {
            return;
        }

        // Hand the header back together with the edited lines so the caller can persist the whole edit.
        _transaction.TransactionLines = _lines;

        MudDialog.Close(DialogResult.Ok(_transaction));
    }
}
