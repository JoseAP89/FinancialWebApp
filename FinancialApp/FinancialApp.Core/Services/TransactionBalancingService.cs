using FinancialApp.Core.DTOs;
using FinancialApp.Data.Models;
using FinancialApp.Core.Data.Repositories;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Data;

namespace FinancialApp.Core.Services;

public class TransactionBalancingService: ITransactionBalancingService
{
    // Description stamped on every compensating line this service persists: the row whose
    // AutoBalancedByLineId stays NULL while the lines it offsets point back at it.
    private const string AutoBalanceDescription = "Auto-balance entry";

    private readonly IAccountRepository _accountRepository;
    private readonly ITransactionLineRepository _transactionLineRepository;

    public TransactionBalancingService(
        IAccountRepository accountRepository,
        ITransactionLineRepository transactionLineRepository)
    {
        _accountRepository = accountRepository;
        _transactionLineRepository = transactionLineRepository;
    }

    // Auto-balance a transaction similar to the provided python implementation:
    // - Calculate current balance using account FinancialStatement:
    //   ASSET and EXPENSE contribute as debits (+), LIABILITY/EQUITY/REVENUE as credits (-)
    // - If the absolute balance > 0.01, find a cash/bank ASSET account and persist a compensating
    //   TransactionLine with Amount = -balance and Description = "Auto-balance entry".
    // The transaction must already exist: every compensating entry is inserted on the spot so its real
    // id can be handed to the lines it balances (TransactionLine.AutoBalancedByLineId).
    public async Task<List<TransactionLineDTO>> BalanceTransactionAsync(
        int transactionId,
        List<TransactionLineDTO> transactionLines,
        ILogger logger = null!)
    {
        if (transactionLines == null || !transactionLines.Any())
            return [];

        decimal debit = 0m;
        decimal credit = 0m;

        foreach (TransactionLineDTO line in transactionLines)
        {
            try
            {
                var acct = line.SelectedChild;
                if (acct is null)
                {
                    logger?.LogWarning("BalanceTransaction: account id {AccountId} not found; skipping line in balance calc.",
                        line.SelectedChildId!.Value);
                    continue;
                }
                if (acct.Name.Equals("Cash on Hand"))
                {
                    var checkAcc = await _accountRepository.GetByNameAsync("Checking Account"); 
                    decimal totalAmount = await _accountRepository.GetAccountTotalAsync(checkAcc!.Id);
                    if (line.Amount > totalAmount)
                    {
                        throw new ArgumentException("Cannot withdraw a superior quantity than what is currently on your account");
                    }
                }

                // Apply financial statement rules
                line.Amount = AdjustAmountForFinancialStatement(line.Amount, acct.FinancialStatement, line.LiabilityAction);

                var lineValue = line.Amount * (line.Quantity <= 0 ? 1 : line.Quantity);

                if (line.PaidWithCreditCard)
                    credit += lineValue;
                else
                    debit += lineValue;
            }
            catch (ArgumentException ex)
            {
                logger?.LogError(ex, "BalanceTransaction: failed while reading account for line account id {AccountId}",
                    line.SelectedChildId!.Value);
                throw;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "BalanceTransaction: failed while reading account for line account id {AccountId}",
                    line.SelectedChildId!.Value);
                throw;
            }
        }

        // Add compensating entries if needed
        if (Math.Abs(debit) > 0.01m || Math.Abs(credit) > 0.01m)
        {
            await AddCompensatingEntriesAsync(transactionId, transactionLines, debit, credit, logger);
        }

        return transactionLines;

    }

    private decimal AdjustAmountForFinancialStatement(decimal amount, FinancialStatement statement, int liabilityAction)
    {
        return statement switch
        {
            FinancialStatement.LIABILITY => liabilityAction != 0 ? liabilityAction * Math.Abs(amount) : amount,
            FinancialStatement.REVENUE => -1 * Math.Abs(amount),
            FinancialStatement.EXPENSE => Math.Abs(amount),
            FinancialStatement.ASSET => Math.Abs(amount),
            _ => amount
        };
    }

    private async Task AddCompensatingEntriesAsync(
        int transactionId,
        List<TransactionLineDTO> transactionLines,
        decimal debit,
        decimal credit,
        ILogger? logger)
    {
        if (Math.Abs(debit) > 0.01m)
        {
            var checkAccount = await _accountRepository.GetByNameAsync("Checking Account");
            if (checkAccount is null)
            {
                logger?.LogWarning("BalanceTransaction: no cash/bank ASSET account found to auto-balance transaction (balance={Balance}).", debit);
                return;
            }

            var balance = decimal.Round(-debit, 2, MidpointRounding.ToEven);

            // Save the entry first: the lines it compensates can only reference it once it has an id.
            var entry = new TransactionLine
            {
                TransactionId = transactionId,
                AccountId = checkAccount.Id,
                Amount = balance,
                Description = AutoBalanceDescription,
                Quantity = 1
            };
            await _transactionLineRepository.AddAsync(entry);
            await _transactionLineRepository.SaveChangesAsync();

            // The debit imbalance comes from the lines not flagged as paid with a credit card.
            LinkAutoBalancedByEntry(transactionLines, entry.Id, paidWithCreditCard: false);

            logger?.LogInformation("BalanceTransaction: added auto-balance line id {LineId} to account id {AccountId} amount {Amount}.",
                entry.Id, checkAccount.Id, balance);
        }

        if (Math.Abs(credit) > 0.01m)
        {
            var creditCardAccount = await _accountRepository.GetByNameAsync("Credit Card");
            if (creditCardAccount is null || creditCardAccount.Id == 0)
            {
                logger?.LogWarning("BalanceTransaction: no LIABILITY account found to auto-balance transaction (balance={Balance}).", credit);
                return;
            }

            var balance = decimal.Round(-credit, 2, MidpointRounding.ToEven);

            // Save the entry first: the lines it compensates can only reference it once it has an id.
            var entry = new TransactionLine
            {
                TransactionId = transactionId,
                AccountId = creditCardAccount.Id,
                Amount = balance,
                Description = AutoBalanceDescription,
                Quantity = 1
            };
            await _transactionLineRepository.AddAsync(entry);
            await _transactionLineRepository.SaveChangesAsync();

            // The credit imbalance comes from the lines flagged as paid with a credit card.
            LinkAutoBalancedByEntry(transactionLines, entry.Id, paidWithCreditCard: true);

            logger?.LogInformation("BalanceTransaction: added auto-balance line id {LineId} to account id {AccountId} amount {Amount}.",
                entry.Id, creditCardAccount.Id, balance);
        }
    }

    // Records on every compensated line the database id of the auto-balance entry that offsets it, so
    // the lineage can be written together with the lines themselves. The split mirrors the imbalance
    // calculation in BalanceTransactionAsync: PaidWithCreditCard lines form the credit group, every
    // other line the debit group, and only lines that took part in that calculation (they have a
    // SelectedChild) are linked. A line whose group imbalance cancelled out gets no entry (NULL).
    private static void LinkAutoBalancedByEntry(
        List<TransactionLineDTO> transactionLines,
        int autoBalanceLineId,
        bool paidWithCreditCard)
    {
        foreach (TransactionLineDTO line in transactionLines.Where(l =>
                     l.PaidWithCreditCard == paidWithCreditCard && l.SelectedChild is not null))
        {
            line.AutoBalancedByLineId = autoBalanceLineId;
        }
    }

}
