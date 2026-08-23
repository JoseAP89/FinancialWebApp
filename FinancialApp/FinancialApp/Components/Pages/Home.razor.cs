using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using FinancialApp.Infrastructure.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
// no direct using for Blazor web events to avoid ambiguity with MAUI FocusEventArgs

namespace FinancialApp.Components.Pages
{
    public partial class Home : ComponentBase
    {
        [Inject]
        protected IToastService ToastService { get; set; } = null!;

        protected LoanModel Model { get; set; } = new LoanModel();
        protected EditContext EditContext { get; set; } = null!;
        protected List<AdditionalDeposit> AdditionalDeposits { get; set; } = new List<AdditionalDeposit>();
        protected decimal FutureValue { get; set; } = 0m;
        protected decimal AdditionalDepositsTotal { get; set; } = 0m;
        // display strings for inputs that will show formatted currency on blur
        protected string AmountDisplay { get; set; } = string.Empty;
        protected string DepositDisplay { get; set; } = string.Empty;
        protected List<string> AdditionalDepositDisplays { get; set; } = new List<string>();

        protected override void OnInitialized()
        {
            EditContext = new EditContext(Model);
            // initialize display strings from model
            AmountDisplay = Model.Amount.ToString("C");
            DepositDisplay = Model.Deposit.ToString("C");
            AdditionalDepositDisplays = AdditionalDeposits.Select(ad => ad.Amount.ToString("C")).ToList();
        }

        protected void AddAdditionalDeposit()
        {
            AdditionalDeposits.Add(new AdditionalDeposit());
            AdditionalDepositDisplays.Add(0m.ToString("C"));
        }

        protected void RemoveAdditionalDeposit(int index)
        {
            if (index >= 0 && index < AdditionalDeposits.Count)
            {
                AdditionalDeposits.RemoveAt(index);
                if (index >= 0 && index < AdditionalDepositDisplays.Count)
                {
                    AdditionalDepositDisplays.RemoveAt(index);
                }
            }
        }

        // focus/blur handlers for deposit formatting
        protected System.Threading.Tasks.Task DepositFocus(global::Microsoft.AspNetCore.Components.Web.FocusEventArgs e)
        {
            // show raw numeric value for editing
            DepositDisplay = Model.Deposit != 0m ? Model.Deposit.ToString("G") : string.Empty;
            return System.Threading.Tasks.Task.CompletedTask;
        }

        protected System.Threading.Tasks.Task DepositBlur(global::Microsoft.AspNetCore.Components.Web.FocusEventArgs e)
        {
            // try to parse the entered value (allow currency symbols)
            if (TryParseCurrency(DepositDisplay, out var value))
            {
                Model.Deposit = value;
                // notify edit context so validations can run
                EditContext?.NotifyFieldChanged(new FieldIdentifier(Model, nameof(Model.Deposit)));
            }
            // format back to currency for display
            DepositDisplay = Model.Deposit.ToString("C");
            return System.Threading.Tasks.Task.CompletedTask;
        }

        protected System.Threading.Tasks.Task AmountFocus(global::Microsoft.AspNetCore.Components.Web.FocusEventArgs e)
        {
            // show raw numeric value for editing
            AmountDisplay = Model.Amount != 0m ? Model.Amount.ToString("G") : string.Empty;
            return System.Threading.Tasks.Task.CompletedTask;
        }

        protected System.Threading.Tasks.Task AmountBlur(global::Microsoft.AspNetCore.Components.Web.FocusEventArgs e)
        {
            // try to parse the entered value (allow currency symbols)
            if (TryParseCurrency(AmountDisplay, out var value))
            {
                Model.Amount = value;
                // notify edit context so validations can run
                EditContext?.NotifyFieldChanged(new FieldIdentifier(Model, nameof(Model.Amount)));
            }
            // format back to currency for display
            AmountDisplay = Model.Amount.ToString("C");
            return System.Threading.Tasks.Task.CompletedTask;
        }

        protected void AdditionalDepositFocus(int index)
        {
            if (index >= 0 && index < AdditionalDeposits.Count && index < AdditionalDepositDisplays.Count)
            {
                var val = AdditionalDeposits[index].Amount;
                AdditionalDepositDisplays[index] = val != 0m ? val.ToString("G") : string.Empty;
            }
        }

        protected void AdditionalDepositBlur(int index)
        {
            if (index < 0 || index >= AdditionalDeposits.Count) return;
            if (index < 0 || index >= AdditionalDepositDisplays.Count) return;
            var display = AdditionalDepositDisplays[index];
            if (TryParseCurrency(display, out var value))
            {
                AdditionalDeposits[index].Amount = value;
            }
            AdditionalDepositDisplays[index] = AdditionalDeposits[index].Amount.ToString("C");
        }

        private bool TryParseCurrency(string? input, out decimal value)
        {
            value = 0m;
            if (string.IsNullOrWhiteSpace(input)) return true;
            // allow currency and number styles
            return decimal.TryParse(input, System.Globalization.NumberStyles.Currency | System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.CurrentCulture, out value);
        }
        
        private bool CheckValidation()
        {
            if (EditContext == null) return false;
            var isValid = EditContext.Validate();
            if (!isValid)
            {
                ToastService.ShowError("Please correct the errors in the form.", "Validation Error");
                return false;
            }
            if ( Model.Amount <= 0 && Model.Period <= 0)
            {
                ToastService.ShowError("Amount and period must be positive.", "Validation Error");
                return false;
            }
            if (Model.AnnualInterestRate < 0 || Model.AnnualInterestRate > 100)
            {
                ToastService.ShowError("Annual Interest Rate must be between 0 and 100.", "Validation Error");
                return false;
            }
            if (Model.Period < 1 || Model.Period > 1200)
            {
                ToastService.ShowError("Period must be between 1 and 1200 months.", "Validation Error");
                return false;
            }
            return true;
        }

        private void UpdateResult()
        {

            if ( !CheckValidation())
            {
                return;
            }
            // Future value of an annuity formula
            // r: interest rate per period (use monthly rate)
            decimal r = Model.AnnualInterestRate / 100m / 12m;
            int n = Model.Period; // Period is in months
            decimal pv = Model.Amount;
            decimal p = Model.Deposit;

            decimal fv;
            if (r == 0m)
            {
                // No interest: FV = PV + P * n
                fv = pv + p * n;
            }
            else
            {
                // FV = (PV * ((1 + r) ** n)) + (P * (((1 + r) ** n - 1) / r))
                var pow = (decimal)Math.Pow((double)(1 + r), n);
                fv = pv * pow + p * ((pow - 1m) / r);
            }
            FutureValue = fv;

            AdditionalDepositsTotal = 0;
            // include any one-time additional deposits specified in the form
            if (AdditionalDeposits != null && AdditionalDeposits.Count > 0)
            {
                // recalculate including additional deposits (add their grown value to the computed fv)
                decimal additionalTotal = 0m;
                foreach (var ad in AdditionalDeposits)
                {
                    if (ad == null) continue;
                    // only consider deposits that occur within the total period
                    if (ad.Period <= 0 || ad.Period > n) continue;
                    int monthsToGrow = n - ad.Period;
                    var grow = (decimal)Math.Pow((double)(1 + r), monthsToGrow);
                    additionalTotal += ad.Amount * grow;
                }
                AdditionalDepositsTotal = additionalTotal;
            }
        }

    }

    public class LoanModel
    {
        public decimal Amount { get; set; } = 0m;

        public decimal Deposit { get; set; }

        public decimal AnnualInterestRate { get; set; }

        public int Period { get; set; }
    }

    public class AdditionalDeposit
    {
        public decimal Amount { get; set; } = 0m;
        public int Period { get; set; } = 0; // month at which this one-time deposit is made
    }
}
