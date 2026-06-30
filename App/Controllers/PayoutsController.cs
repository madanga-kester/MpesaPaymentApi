using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MpesaPaymentApi.Models.Dtos;
using MpesaPaymentApi.Services;

namespace MpesaPaymentApi.Controllers;

[ApiController]
[Route("api/payouts")]
[Authorize]
public class PayoutsController : ControllerBase
{
    private readonly IMpesaService _mpesaService;

    public PayoutsController(IMpesaService mpesaService)
    {
        _mpesaService = mpesaService;
    }

    private string? GetFreelancerId() =>
        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
    {
        var freelancerId = GetFreelancerId();
        if (string.IsNullOrWhiteSpace(freelancerId))
            return Unauthorized();

        var result = await _mpesaService.GetPayoutSettingsAsync(freelancerId, ct);
        if (result == null)
            return NotFound(new { Error = "No payout settings found." });

        return Ok(result);
    }

    [HttpPut("settings")]
    public async Task<IActionResult> SaveSettings([FromBody] PayoutDetailRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var freelancerId = GetFreelancerId();
        if (string.IsNullOrWhiteSpace(freelancerId))
            return Unauthorized();

        var result = await _mpesaService.SavePayoutSettingsAsync(freelancerId, request, ct);
        return Ok(result);
    }
}