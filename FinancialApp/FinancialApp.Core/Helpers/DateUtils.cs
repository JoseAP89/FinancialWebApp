using System;
using System.Collections.Generic;
using System.Text;

namespace FinancialApp.Core.Helpers;

public static class DateUtils
{
    /// <summary>
    /// Helper method to convert DateOnly to UTC DateTime for PostgreSQL
    /// </summary>
    public static DateTime ConvertToUTCPostgreSQL(DateOnly dt, int addDays = 0)
    {
        // Convert DateOnly to LOCAL DateTime first
        var localDateTime = new DateTime(dt.Year, dt.Month, dt.Day, 0, 0, 0, DateTimeKind.Local).AddDays(addDays);
        // Convert to UTC for PostgreSQL
        return localDateTime.ToUniversalTime();
    }
}
