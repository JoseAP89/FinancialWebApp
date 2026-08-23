using FinancialApp.Core.DTOs;
using FinancialApp.Data.Models;
using FinancialApp.Core.Data.Repositories;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinancialApp.Core.Services;

public class TransactionBalancingService: ITransactionBalancingService
{
    private readonly IAccountRepository _accountRepository;

    public TransactionBalancingService(IAccountRepository accountRepository)
    {
        _accountRepository = accountRepository;
    }

    // Auto-balance a transaction similar to the provided python implementation:
    // - Calculate current balance using account FinancialStatement:
    //   ASSET and EXPENSE contribute as debits (+), LIABILITY/EQUITY/REVENUE as credits (-)
    // - If the absolute balance > 0.01, find a cash/bank ASSET account and append a
    //   compensating TransactionLine with Amount = -balance and Description = "Auto-balance entry".
    public async Task<List<TransactionLineDTO>> BalanceTransactionAsync(
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

                // Apply financial statement rules
                line.Amount = AdjustAmountForFinancialStatement(line.Amount, acct.FinancialStatement, line.LiabilityAction);

                var lineValue = line.Amount * (line.Quantity <= 0 ? 1 : line.Quantity);

                if (line.PaidWithCreditCard)
                    credit += lineValue;
                else
                    debit += lineValue;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "BalanceTransaction: failed while reading account for line account id {AccountId}",
                    line.SelectedChildId!.Value);
            }
        }

        // Add compensating entries if needed
        if (Math.Abs(debit) > 0.01m || Math.Abs(credit) > 0.01m)
        {
            await AddCompensatingEntriesAsync(transactionLines, debit, credit, logger);
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
        List<TransactionLineDTO> transactionLines,
        decimal debit,
        decimal credit,
        ILogger logger)
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
            transactionLines.Add(new TransactionLineDTO
            {
                SelectedChildId = checkAccount.Id,
                Amount = balance,
                Description = "Auto-balance entry",
                Quantity = 1
            });

            logger?.LogInformation("BalanceTransaction: added auto-balance line to account id {AccountId} amount {Amount}.",
                checkAccount.Id, balance);
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
            transactionLines.Add(new TransactionLineDTO
            {
                SelectedChildId = creditCardAccount.Id,
                Amount = balance,
                Description = "Auto-balance entry",
                Quantity = 1
            });

            logger?.LogInformation("BalanceTransaction: added auto-balance line to account id {AccountId} amount {Amount}.",
                creditCardAccount.Id, balance);
        }
    }

}
