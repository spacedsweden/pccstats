using Microsoft.AspNetCore.Mvc;
using Sinch.MessageRouter.Core.Dispatch;
using System.Collections.Concurrent;

namespace Sinch.MessageRouter.Gateway.Controllers;

[ApiController]
[Route("v1/dispatch")]
public class DispatchController : ControllerBase
{
    private static readonly ConcurrentDictionary<string, DispatchRule> Rules = new();

    [HttpPost]
    [ProducesResponseType(typeof(DispatchRule), StatusCodes.Status201Created)]
    public IActionResult Create([FromBody] CreateDispatchRuleRequest request)
    {
        var rule = new DispatchRule
        {
            Id = Guid.NewGuid().ToString("N")[..16],
            Name = request.Name, Description = request.Description,
            Settings = request.Settings, Active = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        Rules[rule.Id!] = rule;
        return CreatedAtAction(nameof(Get), new { dispatchId = rule.Id }, rule);
    }

    [HttpGet]
    public IActionResult List() => Ok(new { data = Rules.Values.ToList() });

    [HttpGet("{dispatchId}")]
    public IActionResult Get(string dispatchId)
        => Rules.TryGetValue(dispatchId, out var rule) ? Ok(rule) : NotFound();

    [HttpPatch("{dispatchId}")]
    public IActionResult Update(string dispatchId, [FromBody] UpdateDispatchRuleRequest request)
    {
        if (!Rules.TryGetValue(dispatchId, out var existing)) return NotFound();
        var updated = new DispatchRule
        {
            Id = existing.Id,
            Name = request.Name ?? existing.Name,
            Description = request.Description ?? existing.Description,
            Settings = request.Settings ?? existing.Settings,
            Active = request.Active ?? existing.Active,
            CreatedAt = existing.CreatedAt,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        Rules[dispatchId] = updated;
        return Ok(updated);
    }

    [HttpDelete("{dispatchId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Delete(string dispatchId)
        => Rules.TryRemove(dispatchId, out _) ? NoContent() : NotFound();
}
