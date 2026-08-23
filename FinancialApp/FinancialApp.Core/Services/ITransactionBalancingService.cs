using FinancialApp.Core.DTOs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinancialApp.Core.Services;

public interface ITransactionBalancingService
{
    Task<List<TransactionLineDTO>> BalanceTransactionAsync(
    List<TransactionLineDTO> transactionLines,
    ILogger logger = null!);
}
