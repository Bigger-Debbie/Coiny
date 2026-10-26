using Coiny.API.Enums;
using System.ComponentModel.DataAnnotations;
namespace Coiny.API.DTOs.Budgets;
public class AvailableCashRequest
{
    public DateOnly? AsOf { get; set; }
    [MaxLength(200)] public int[]? AccountIds { get; set; }
}
public record AvailableCashAccount(int Id, string Name, AccountType AccountType, decimal Balance, bool IsActive);
public record AvailableCashResponse(DateOnly BalanceDate, DateOnly EndDate, decimal CashBalance, decimal CardBalance,
    decimal ExpectedIncome, decimal RemainingAllocations, decimal? ExpectedRemaining,
    IReadOnlyList<int> AccountIds, IReadOnlyList<AvailableCashAccount> Accounts);
