## FinancialApp

FinancialApp is an android app (Blazor/.NET MAUI based workspace) that demonstrates a simple financial calculator for computing the future value of an annuity. It lets a user enter an initial amount (present value), a periodic deposit, an annual interest rate and a period (in months), and shows the computed future value using the formula:

$$
FV = PV \times (1 + r)^n + P \times \frac{(1 + r)^n - 1}{r}
$$


Where:
- `PV` is the initial amount (present value)
- `P` is the periodic deposit
- `r` is the interest rate per period (monthly rate = annualRate / 12)
- `n` is the number of periods (months)

It also has the capability to store your daily finances so that you can track them and take better financial decisions.

Project structure (key files)
-----------------------------
- `Components/Pages/Home.razor` - UI for the calculator (inputs and result)
- `Components/Pages/Home.razor.cs` - form model (`LoanModel`), `EditContext` and calculation logic
- `Components/Pages/Home.razor.css` - local styles for the page

How to run
----------
1. Open the solution in Microsoft Visual Studio (2026) that targets .NET 10.
2. Set the appropriate startup project (the Blazor/MAUI host) and run (F5) or use the debugger.
3. Navigate to the root page (`/`) to use the calculator.

## 🏛️ The Core Architecture 

This guide walks you through a clean, theory-driven approach to explaining your financial database structure. By treating your bank accounts and spending categories as equals, we build a system that's both powerful and simple.

## Table of Contents
- [The Core Architectural Shift](#1-core-architectural-shift)
- [Complete Financial ERD](#2-complete-financial-erd)
- [Comprehensive Table Structures](#3-comprehensive-table-structures)
- [Anticipating Your Reports](#4-anticipating-your-reports-how-data-maps)
- [Practical Ledger Examples](#5-practical-ledger-examples)
- [The Foundational Five](#the-foundational-five)

## 1. Core Architectural Shift

Forget the old way of having separate tables for **Accounts** and **Categories**. In our world, everything lives together in one master directory: **Accounts**.

- Your **Bank Accounts** (Checking, Credit Card) and your **Spending Categories** (Groceries, Gas, Salary) are structurally identical.
- They are simply distinguished by their high-level **Accounting Type** (e.g., `ASSET`, `EXPENSE`).

This unification makes your database incredibly flexible and easier to manage.

## 2. Complete Financial ERD

Here is the big picture of how everything connects. It's simpler than you think!
```
+----------------------------------------------------------------+
| ACCOUNTS                                                       |
+----------------------------------------------------------------+
| id (PK)                                                        |
| name (e.g., "Checking", "Groceries", "Salary")                 |
| financial_statement(ASSET, LIABILITY, EQUITY, REVENUE, EXPENSE)|
| parent_id (FK -> ACCOUNTS.id)                                  |
+------------------------+---------------------------------------+
                         |
                         | 1
                         |
                         | Many
+------------------------+-------------------------+
| TRANSACTION_LINES                                |
+--------------------------------------------------+
| id (PK)                                          |
| transaction_id (FK -> TRANSACTIONS.id)           |
| account_id (FK -> ACCOUNTS.id)                   |
| amount (NUMERIC)                                 |
| description (TEXT)                               |
+------------------------+-------------------------+
                         |
                         | Many
                         |
                         | 1
+------------------------+-------------------------+
| TRANSACTIONS                                     |
+--------------------------------------------------+
| id (PK)                                          |
| date (DATE)                                      |
| description (TEXT)                               |
+--------------------------------------------------+
```

## 3. Comprehensive Table Structures

Let's break down exactly what lives inside each table.

### Accounts Table
This is the master list. It contains *everything*—your assets, liabilities, revenues, expenses, and all the nested subcategories.

| Column Name | Data Type | Description |
| :--- | :--- | :--- |
| `id (PK)` | `UUID / INT` | Unique identifier for the account. |
| `name` | `VARCHAR` | A friendly name (e.g., "Chase Checking", "Food & Dining"). |
| `financial_statement` | `ENUM` | The root type: `ASSET`, `LIABILITY`, `EQUITY`, `REVENUE`, `EXPENSE`. |
| `parent_id (FK)` | `UUID / INT` | References `id` in this table. `NULL` for top-level classes. Allows for an infinite nesting of subcategories. |

### Transactions Table
Think of this as the "header" or receipt for a financial event. It tells us *when* something happened.

| Column Name | Data Type | Description |
| :--- | :--- | :--- |
| `id (PK)` | `UUID / INT` | Unique identifier for the transaction. |
| `date` | `DATE` | The exact day the transaction occurred. |
| `description` | `TEXT` | A memo or note (e.g., "Weekly grocery run at Walmart"). |

### Transaction Lines Table (The Ledger Entries)
This is where the magic of double-entry accounting happens. Each transaction must have **at least two lines**.

- **The Golden Rule:** The `amount` for all lines in a single `transaction_id` must **always sum to exactly $0.00** (Debits = Credits).
- **Direction:** Positive numbers mean money is *entering* an account. Negative numbers mean money is *leaving*.

| Column Name | Data Type | Description |
| :--- | :--- | :--- |
| `id (PK)` | `UUID / INT` | Unique identifier for the line item. |
| `transaction_id (FK)` | `UUID / INT` | Links back to the parent `TRANSACTIONS` header. |
| `account_id (FK)` | `UUID / INT` | Links to the specific Account/Category in the `ACCOUNTS`. |
| `amount` | `NUMERIC` | The signed financial amount (e.g., `150.00` or `-150.00`). |
| `description` | `TEXT` | A memo or note (e.g., "1kg of pork meat"). |

## 4. Anticipating Your Reports (How Data Maps)

With this structure, generating financial statements is as easy as filtering by the `class` column. Your data is ready for analysis!

### Balance Sheet (Balance General)
- **Snapshot:** A picture of your financial health "As of Today."
- **Filter:** `WHERE financial_statement IN ('ASSET', 'LIABILITY', 'EQUITY')`.
    - **Assets:** Checking, Savings, Investments, Property.
    - **Liabilities:** Credit Card Balances, Student Loans, Mortgages.
    - **Equity:** Your calculated Net Worth (Assets - Liabilities).

### Income Statement (Estado de Resultados)
- **Performance:** Tracks your financial activity over a period (e.g., "January 1 to January 31").
- **Filter:** `WHERE class IN ('REVENUE', 'EXPENSE')`.
    - **Revenue:** Salary, Side hustle revenue, Dividends.
    - **Expenses:** Your nested categories (Groceries, Auto, Gas).
    - **Net Income:** Income minus Expenses. This is the exact amount you saved during that timeframe!

## Balance Sheet Elements (Financial Position)

**Assets:** Economic resources owned or controlled by a business that will provide future economic benefits.

**Liabilities:** Present obligations of a business arising from past events, requiring a future outflow of resources.

**Equity:** The residual interest in the assets of the business after deducting all of its liabilities.

## Income Statement Elements (Financial Performance)

**Revenue (Income):** Increases in economic benefits during the accounting period, resulting in an increase in equity (other than contributions from equity participants).

**Expenses:** Decreases in economic benefits during the accounting period, resulting in a decrease in equity (other than distributions to equity participants).

## 5. Practical Ledger Examples

Here is how everyday money moves route through this schema to ensure your books always balance.

### Buying Gas ($50)
- **Line 1:** `account_id` = Checking (`ASSET`), `amount` = `-50.00`
- **Line 2:** `account_id` = Gas (`EXPENSE`), `amount` = `50.00`
- **Result:** Assets drop by $50, Expenses increase by $50.

### Moving Money to Savings ($200)
- **Line 1:** `account_id` = Checking (`ASSET`), `amount` = `-200.00`
- **Line 2:** `account_id` = Savings (`ASSET`), `amount` = `200.00`
- **Result:** Total Assets remain completely unchanged. The Income Statement is never touched.

### Receiving Paycheck ($3,000)
- **Line 1:** `account_id` = Checking (`ASSET`), `amount` = `3000.00`
- **Line 2:** `account_id` = Salary (`REVENUE`), `amount` = `-3000.00`
- **Result:** Assets increase by $3,000, Income increases by $3,000.

## The Foundational Five

All of this is built on the core framework of financial accounting. These five categories are the universal building blocks.

```
                  ┌───────────────────────────────┐
                  │ Elements of Financial State.  │
                  └───────────────┬───────────────┘
                                  │
         ┌────────────────────────┴────────────────────────┐
         │                                                 │
         ▼                                                 ▼
┌─────────────────┐                               ┌─────────────────┐
│  Balance Sheet  │                               │Income Statement │
└────────┬────────┘                               └────────┬────────┘
         │                                                 │
         ├─► Asset                                         ├─► Revenue
         ├─► Liability                                     └─► Expense
         └─► Equity
```

To ensure the accounting equation (Asset+Expense) - (Liability+Equity+Revenue) = 0 (which is the same as Debits - Credits = 0), you need to automatically add balancing transaction lines when users only record their expenses or income.

Here's the complete solution:

# Understanding the Sign Convention

The rules:
- Debit accounts (+) : ASSET, EXPENSE
- Credit accounts (-) : LIABILITY, EQUITY, REVENUE

For a transaction to be balanced, the sum of positive amounts (Debits) must equal the sum of negative amounts (Credits).

# Logic to Auto-Balance Transactions

When a user inserts transaction lines, you need to:
1. Calculate the current balance
2. Determine which account to use as the balancing account
3. Insert a compensating line

# Example: User records an expense

```
-- User records: "Bought groceries for $50"
-- They would create:
-- TransactionLine: AccountId=Groceries, Amount=50.00 (Debit)

-- The system would auto-balance by adding:
-- TransactionLine: AccountId=CheckingAccount, Amount=-50.00 (Credit)
```

# Accounts to Hide from Users

We hide certain accounts from the user interface to prevent confusion and maintain data integrity.
Must-Hide Accounts:
- All Equity accounts - These are system-calculated and should never be directly manipulated
- Contra-asset accounts (like Accumulated Depreciation)
- Balancing/cash accounts used for auto-balancing

# Understanding Liability Accounts in Your System

Under your accounting rules:
- LIABILITY accounts are negative (-) (normal Credit balance)
- When you increase a liability (take out a loan), you CREDIT the liability account
- When you decrease a liability (make a payment), you DEBIT the liability account

**Scenario A: Taking Out a New Loan** 
When you take out a loan, you receive cash (or an asset) and create a liability:
```
-- Example: Taking out a $10,000 Auto Loan
-- User records:
-- 1. They received $10,000 in their checking account (DEBIT - Asset increases)
-- 2. They now owe $10,000 (CREDIT - Liability increases)
-- The balanced transaction would be:
INSERT INTO TransactionLines (TransactionId, AccountId, Amount, Description) VALUES
(1, (SELECT Id FROM Accounts WHERE Name='Checking Account'), 10000.00, 'Auto loan proceeds'),  -- DEBIT (+)
(1, (SELECT Id FROM Accounts WHERE Name='Auto Loan'), -10000.00, 'Auto loan balance');        -- CREDIT (-)
```

**Scenario B: Making a Loan Payment** 
When you make a payment, it typically splits between:
- Principal (reduces the liability)
- Interest (an expense)
```
-- Example: $500 auto loan payment ($400 principal + $100 interest)
-- User records:
INSERT INTO TransactionLines (TransactionId, AccountId, Amount, Description) VALUES
(2, (SELECT Id FROM Accounts WHERE Name='Auto Loan'), 400.00, 'Principal payment'),     -- DEBIT (+ reduces liability)
(2, (SELECT Id FROM Accounts WHERE Name='Interest Expense'), 100.00, 'Interest payment'); -- DEBIT (+ expense)

-- System auto-balances with:
INSERT INTO TransactionLines (TransactionId, AccountId, Amount, Description) VALUES
(2, (SELECT Id FROM Accounts WHERE Name='Checking Account'), -500.00, 'Auto-balance: Payment'); -- CREDIT (-)
```

# Common Liability Scenarios and Auto-Balancing

**Scenario 1: Recording a Loan Payment**
```
-- User records (manual entry by user):
INSERT INTO TransactionLines (TransactionId, AccountId, Amount) VALUES
(100, (SELECT Id FROM Accounts WHERE Name='Auto Loan'), 400.00, 'Principal'),        -- DEBIT (reduces liability)
(100, (SELECT Id FROM Accounts WHERE Name='Interest Expense'), 100.00, 'Interest');  -- DEBIT (expense)

-- Total debits: +500.00
-- System auto-balances:
INSERT INTO TransactionLines (TransactionId, AccountId, Amount) VALUES
(100, (SELECT Id FROM Accounts WHERE Name='Checking Account'), -500.00, 'Auto-balance: Payment');

-- Result: Balanced! (Debits = Credits)
-- Debits: 400 + 100 = 500
-- Credits: 500
```

**Scenario 2: Taking Out a New Loan**
```
-- User records:
INSERT INTO TransactionLines (TransactionId, AccountId, Amount) VALUES
(101, (SELECT Id FROM Accounts WHERE Name='Auto Loan'), -25000.00, 'New car loan');  -- CREDIT (increases liability)

-- Total debits: 0, Credits: 25000
-- System auto-balances:
INSERT INTO TransactionLines (TransactionId, AccountId, Amount) VALUES
(101, (SELECT Id FROM Accounts WHERE Name='Checking Account'), 25000.00, 'Auto-balance: Loan proceeds');

-- Result: Balanced! (Debits = Credits)
-- Debits: 25000
-- Credits: 25000
```

**Scenario 3: Credit Card Purchase**
```
-- User records a $100 purchase on credit card:
INSERT INTO TransactionLines (TransactionId, AccountId, Amount) VALUES
(102, (SELECT Id FROM Accounts WHERE Name='Groceries'), 100.00, 'Grocery purchase');  -- DEBIT (expense)

-- Total debits: 100, Credits: 0
-- System auto-balances:
INSERT INTO TransactionLines (TransactionId, AccountId, Amount) VALUES
(102, (SELECT Id FROM Accounts WHERE Name='Credit Card'), -100.00, 'Auto-balance: Credit card purchase');

-- Result: Balanced! (Debits = Credits)
-- Debits: 100
-- Credits: 100
```

**Scenario 4: Credit Card Payment**
```
-- User records a $200 credit card payment:
INSERT INTO TransactionLines (TransactionId, AccountId, Amount) VALUES
(103, (SELECT Id FROM Accounts WHERE Name='Credit Card'), 200.00, 'Credit card payment');  -- DEBIT (reduces liability)

-- Total debits: 200, Credits: 0
-- System auto-balances:
INSERT INTO TransactionLines (TransactionId, AccountId, Amount) VALUES
(103, (SELECT Id FROM Accounts WHERE Name='Checking Account'), -200.00, 'Auto-balance: Payment');

-- Result: Balanced! (Debits = Credits)
-- Debits: 200
-- Credits: 200
```

# The Revenue Recording Flow

**Scenario: User receives $20,000 salary** 
Revenue accounts have a normal Credit balance (-) under your system, so when you earn money, you CREDIT the Revenue account.
```
-- User records the salary income:
INSERT INTO TransactionLines (TransactionId, AccountId, Amount, Description) VALUES
(1, (SELECT Id FROM Accounts WHERE Name='Salary'), -20000.00, 'Monthly salary');

-- Transaction balance calculation:
-- Salary (REVENUE) = -20000.00 (CREDIT)
-- Balance = -20000.00 (needs balancing)

-- System auto-balances with CASH/BANK (ASSET):
INSERT INTO TransactionLines (TransactionId, AccountId, Amount, Description) VALUES
(1, (SELECT Id FROM Accounts WHERE Name='Checking Account'), 20000.00, 'Auto-balance: Salary deposit');

-- Result: BALANCED! ✅
-- Debits: 20000.00 (Checking Account)
-- Credits: 20000.00 (Salary)
-- Debits = Credits ✓
```

# Key Takeaway on balancing-out accounts

Always use a Cash/Bank account as the balancing account for liabilities, but:
- For loan payments: Credit Cash (you're paying money)
- For new loans: Debit Cash (you're receiving money)
- For credit card purchases: Credit Credit Card (you're increasing debt)
- For credit card payments: Debit Credit Card (you're reducing debt)

The auto-balance logic should detect whether the transaction involves:
1. Paying a liability → Credit Cash
2. Increasing a liability → Debit Cash
3. Paying with credit → Credit Credit Card

Notes
-----
- The database on Android depends on the Resourses/Raw/PersonalFinanceDB.db accuracy, thus, the local DB must be copied and pasted in that
directory, so that the app can copy it from the project and paste it at the android device. To update it a deletion must be proformed on the outdated android database.
- The app uses Blazor `EditForm` with an `EditContext` and data annotations for validation; this provides behavior similar to reactive forms.
- The `Period` input is expressed in months.
- If you want a different convention for deposit timing (beginning vs end of period) or additional features (export, charts, different compounding), open an issue or extend the `Home.razor.cs` logic.

License
-------
This repository contains example/demo code. Adapt and reuse as needed.
