


using System;
using System.ComponentModel.DataAnnotations;

namespace MpesaPaymentApi.Models.Dtos;


public class PayoutDetailRequest
{
    [Required]
    public string Method { get; set; } = string.Empty; // mpesa-phone | mpesa-till | mpesa-paybill | bank

    public string? PhoneNumber { get; set; }
    public string? TillNumber { get; set; }
    public string? PaybillNumber { get; set; }
    public string? PaybillAccount { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankBranchCode { get; set; }
}


public class PayoutDetailResponse
{
    public int Id { get; set; }
    public string FreelancerId { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? TillNumber { get; set; }
    public string? PaybillNumber { get; set; }
    public string? PaybillAccount { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankBranchCode { get; set; }
    public bool IsVerified { get; set; }
    public DateTime UpdatedAt { get; set; }
}