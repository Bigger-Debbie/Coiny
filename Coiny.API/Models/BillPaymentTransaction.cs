namespace Coiny.API.Models;

// Additional ledger entries; the original reference remains compatible with existing payments.
public class BillPaymentTransaction
{
    public int Id { get; set; }
    public int BillPaymentId { get; set; }
    public BillPayment BillPayment { get; set; } = null!;
    public int TransactionId { get; set; }
    public Transaction Transaction { get; set; } = null!;
}
