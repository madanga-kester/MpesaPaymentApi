using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
    private readonly HashSet<string> _knownClientIds;

    public MpesaController(
        IMpesaService mpesaService,
        AppDbContext dbContext,
        ILogger<MpesaController> logger,
        Microsoft.Extensions.Options.IOptions<MpesaPaymentApi.Models.Configuration.ClientAppOptions> clientAppOptions)
    {
        _mpesaService = mpesaService;
        _dbContext = dbContext;
        _logger = logger;
        _knownClientIds = clientAppOptions.Value.Apps.Select(a => a.ClientId).ToHashSet();
    }

    private IActionResult? ValidateClientId(out string clientId)
    {
        clientId = Request.Headers["X-Client-Id"].ToString();

        if (string.IsNullOrWhiteSpace(clientId))
            return BadRequest(new { Error = "X-Client-Id header is required." });

        if (!_knownClientIds.Contains(clientId))
        {
            _logger.LogWarning("Request received with unrecognized client id: {ClientId}", clientId);
            return BadRequest(new { Error = "Unrecognized client application." });
        }

        return null;
    }

    //  STK Push 

    [HttpPost("stkpush")]
    public async Task<IActionResult> InitiateStkPush([FromBody] StkPushRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var clientIdError = ValidateClientId(out var clientId);
        if (clientIdError != null) return clientIdError;

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var response = await _mpesaService.SendStkPushAsync(request, userId, clientId, ct);
        return Ok(response);
    }

    //  Transactions 
    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions(
              [FromQuery] int page = 1,
              [FromQuery] int pageSize = 20,
              [FromQuery] string? phoneNumber = null,
              [FromQuery] string? status = null,
              [FromQuery] string? originClientId = null,
              CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = _dbContext.MpesaTransactions.AsQueryable();

        // Row-level scoping: non-admins only ever see their own transactions,
        // regardless of what filters they pass in the query string.
        if (!User.IsInRole("Admin"))
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            query = query.Where(t => t.UserId == userId);
        }

        if (!string.IsNullOrWhiteSpace(phoneNumber))
            query = query.Where(t => t.PhoneNumber == phoneNumber);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(t => t.Status == status);

        if (!string.IsNullOrWhiteSpace(originClientId))
            query = query.Where(t => t.OriginClientId == originClientId);

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
                t.OriginClientId,
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
        var query = _dbContext.MpesaTransactions.AsNoTracking().Where(t => t.Id == id);

        if (!User.IsInRole("Admin"))
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            query = query.Where(t => t.UserId == userId);
        }

        var transaction = await query.FirstOrDefaultAsync(ct);

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
            transaction.OriginClientId,
            transaction.TransactionDate,
            transaction.CallbackReceivedAt,
            transaction.CreatedAt,
            transaction.UpdatedAt
        });
    }

    [HttpGet("transactions/checkout/{checkoutRequestId}")]
    public async Task<IActionResult> GetTransactionByCheckoutId(string checkoutRequestId, CancellationToken ct)
    {
        var query = _dbContext.MpesaTransactions.AsNoTracking().Where(t => t.CheckoutRequestID == checkoutRequestId);

        if (!User.IsInRole("Admin"))
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            query = query.Where(t => t.UserId == userId);
        }

        var transaction = await query.FirstOrDefaultAsync(ct);

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
            transaction.OriginClientId,
            transaction.CreatedAt,
            transaction.UpdatedAt
        });
    }

    //  Refund 
    [HttpPost("refund")]
    [Authorize(Policy = "FinanceOps")]
    [ProducesResponseType(typeof(B2cRefundResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> InitiateRefund([FromBody] B2cRefundRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var clientIdError = ValidateClientId(out var clientId);
        if (clientIdError != null) return clientIdError;

        var response = await _mpesaService.InitiateB2cRefundAsync(request, clientId, ct);
        return Ok(response);
    }
    //  Callback (Safaricom  this API, no auth)
    [HttpPost("callback")]
    [AllowAnonymous]
    [EnableRateLimiting("callback")]
    public async Task<IActionResult> HandleMpesaCallback([FromBody] MpesaCallbackPayload payload, CancellationToken ct)
    {
        _logger.LogDebug("M-Pesa callback received for CheckoutRequestID {CheckoutId}",
            payload?.Body?.StkCallback?.CheckoutRequestID);

        if (payload == null)
        {
            _logger.LogWarning("M-Pesa callback received with null body.");
            return Ok(new { ResultCode = 1, ResultDesc = "Rejected" });
        }

        var result = await _mpesaService.ValidateCallbackAsync(payload, ct);
        return result
            ? Ok(new { ResultCode = 0, ResultDesc = "Accepted" })
            : Ok(new { ResultCode = 1, ResultDesc = "Rejected" }); // Safaricom expects 200 either way; retries on non-2xx
    }
}