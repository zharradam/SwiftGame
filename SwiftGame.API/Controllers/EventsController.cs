using Microsoft.AspNetCore.Mvc;
using SwiftGame.API.Services;

namespace SwiftGame.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventsController(IAuditService audit) : ControllerBase
{
    public record VisitRequest(string VisitorId, string? Source, string? Device);

    // Called once when the page loads
    [HttpPost("visit")]
    public async Task<IActionResult> Visit([FromBody] VisitRequest req)
    {
        if (!Guid.TryParse(req.VisitorId, out _)) return BadRequest();
        await audit.LogVisitAsync(req.VisitorId, req.Source, req.Device);
        return NoContent();
    }
}
