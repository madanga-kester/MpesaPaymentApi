using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MpesaPaymentApi.Data;
using MpesaPaymentApi.Models.Dtos;
using MpesaPaymentApi.Services;

namespace MpesaPaymentApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] 
public class MpesaController : ControllerBase
{
    private readonly IMpesaService _mpesaService;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<MpesaController> _logger;

    public MpesaController(
        IMpesaService mpesaService,
        AppDbContext dbContext,
        ILogger<MpesaController> logger)
    {
        _mpesaService = mpesaService;
        _dbContext = dbContext;
        _logger = logger;
    }

    //  STK Push 

    [HttpPost("stkpush")]
    public async Task<IActionResult> InitiateStkPush([FromBody] StkPushRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.PhoneNumber) || request.Amount <= 0)
            return BadRequest(new { Error = "Invalid phone number or amount." });

        try
        {
            var response = await _mpesaService.SendStkPushAsync(request, ct);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "STK push failed for {Phone}", request.PhoneNumber);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    //  Transactions 

    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? phoneNumber = null,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = _dbContext.MpesaTransactions.AsQueryable();

        if (!string.IsNullOrWhiteSpace(phoneNumber))
            query = query.Where(t => t.PhoneNumber == phoneNumber);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(t => t.Status == status);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                t.Id,
                t.CheckoutRequestID,
                t.PhoneNumber,
                t.Amount,
                t.Status,
                t.ResultCode,
                t.MpesaReceiptNumber,
                t.CreatedAt,
                t.UpdatedAt
            })
            .ToListAsync(ct);

        return Ok(new
        {
            page,
            pageSize,
            total,
            totalPages = (int)Math.Ceiling(total / (double)pageSize),
            items
        });
    }

    [HttpGet("transactions/{id:int}")]
    public async Task<IActionResult> GetTransaction(int id, CancellationToken ct)
    {
        var transaction = await _dbContext.MpesaTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (transaction == null)
            return NotFound(new { Error = "Transaction not found." });

        return Ok(new
        {
            transaction.Id,
            transaction.CheckoutRequestID,
            transaction.MerchantRequestID,
            transaction.PhoneNumber,
            transaction.Amount,
            transaction.AccountReference,
            transaction.TransactionDesc,
            transaction.Status,
            transaction.ResultCode,
            transaction.ResultDesc,
            transaction.MpesaReceiptNumber,
            transaction.TransactionDate,
            transaction.CallbackReceivedAt,
            transaction.CreatedAt,
            transaction.UpdatedAt
        });
    }

    [HttpGet("transactions/checkout/{checkoutRequestId}")]
    public async Task<IActionResult> GetTransactionByCheckoutId(string checkoutRequestId, CancellationToken ct)
    {
        var transaction = await _dbContext.MpesaTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.CheckoutRequestID == checkoutRequestId, ct);

        if (transaction == null)
            return NotFound(new { Error = "Transaction not found." });

        return Ok(new
        {
            transaction.Id,
            transaction.CheckoutRequestID,
            transaction.Status,
            transaction.ResultCode,
            transaction.ResultDesc,
            transaction.Amount,
            transaction.MpesaReceiptNumber,
            transaction.CreatedAt,
            transaction.UpdatedAt
        });
    }

    //  Refund 

    [HttpPost("refund")]
    [ProducesResponseType(typeof(B2cRefundResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> InitiateRefund([FromBody] B2cRefundRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.PhoneNumber) || request.Amount <= 0)
            return BadRequest(new { Error = "Invalid phone number or amount." });

        if (string.IsNullOrWhiteSpace(request.OriginalCheckoutRequestId))
            return BadRequest(new { Error = "OriginalCheckoutRequestId is required." });

        try
        {
            var response = await _mpesaService.InitiateB2cRefundAsync(request, ct);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Refund failed for CheckoutRequestID: {CheckoutID}", request.OriginalCheckoutRequestId);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    //  Callback (Safaricom  this API, no auth)

    [HttpPost("callback")]
    [AllowAnonymous] 
    public async Task<IActionResult> HandleMpesaCallback([FromBody] MpesaCallbackPayload payload, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("M-Pesa callback received: {@Payload}", payload);
            var result = await _mpesaService.ValidateCallbackAsync(payload, ct);
            return result
                ? Ok(new { status = "received" })
                : BadRequest(new { error = "Invalid callback payload" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Callback processing failed");
            
            return Ok(new { status = "error_logged" });
        }
    }
}