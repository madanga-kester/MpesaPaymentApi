using Microsoft.AspNetCore.Mvc;
using MpesaPaymentApi.Models.Dtos;
using MpesaPaymentApi.Services;
using System.Threading;
using System.Threading.Tasks;

namespace MpesaPaymentApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MpesaController : ControllerBase
{
    private readonly IMpesaService _mpesaService;

    public MpesaController(IMpesaService mpesaService)
    {
        _mpesaService = mpesaService;
    }

    [HttpPost("stkpush")]
    public async Task<IActionResult> InitiateStkPush([FromBody] StkPushRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.PhoneNumber) || request.Amount <= 0)
            return BadRequest("Invalid phone number or amount.");

        try
        {
            var response = await _mpesaService.SendStkPushAsync(request, ct);
            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = ex.Message });
        }
    }
}