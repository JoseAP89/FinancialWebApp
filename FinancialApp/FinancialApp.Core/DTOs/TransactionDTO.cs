using System;
using System.Collections.Generic;
using System.Text;

namespace FinancialApp.Core.DTOs;

public class TransactionDTO
{
    public int Id { get; set; }


    public string? Description { get; set; }

    public DateTime Date { get; set; } = DateTime.UtcNow;

    public ICollection<TransactionLineDTO> TransactionLines { get; set; } = new List<TransactionLineDTO>();
}
