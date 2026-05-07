using Microsoft.AspNetCore.Mvc;
using MpesaPaymentApi.Models.Dtos;
using MpesaPaymentApi.Services;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace MpesaPaymentApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MpesaCallbackController : ControllerBase
{
    private readonly IMpesaService _mpesaService;
    private readonly ILogger<MpesaCallbackController> _logger;

    public MpesaCallbackController(IMpesaService mpesaService, ILogger<MpesaCallbackController> logger)
    {
        _mpesaService = mpesaService;
        _logger = logger;
    }

    [HttpPost("callback")]
    public async Task<IActionResult> HandleCallback([FromBody] MpesaCallbackPayload payload, CancellationToken ct)
    {
        try
        {
            var success = await _mpesaService.ValidateCallbackAsync(payload, ct);
            if (success)
            {
                return Ok(new { ResultCode = 0, ResultDesc = "Accepted" });
            }
            return BadRequest(new { ResultCode = 1, ResultDesc = "Rejected" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Callback processing failed.");
            return StatusCode(500, new { ResultCode = 1, ResultDesc = "Internal Server Error" });
        }
    }
}