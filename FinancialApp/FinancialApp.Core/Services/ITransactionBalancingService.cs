using FinancialApp.Core.DTOs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinancialApp.Core.Services;

public interface ITransactionBalancingService
{
    /// <summary>
    /// Calculates the debit/credit imbalance of the given lines, persists the compensating entries for
    /// <paramref name="transactionId"/> and records, on every line it compensates, the database id of
    /// the entry that offsets it (<see cref="TransactionLineDTO.AutoBalancedByLineId"/>).
    /// The transaction itself must already exist, since each entry is inserted right away.
    /// </summary>
    Task<List<TransactionLineDTO>> BalanceTransactionAsync(
        int transactionId,
        List<TransactionLineDTO> transactionLines,
        ILogger logger = null!);
}
