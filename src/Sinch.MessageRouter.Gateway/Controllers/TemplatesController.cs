using Microsoft.AspNetCore.Mvc;
using Sinch.MessageRouter.Core.Templates;
using System.Collections.Concurrent;

namespace Sinch.MessageRouter.Gateway.Controllers;

[ApiController]
[Route("v1/templates")]
public class TemplatesController : ControllerBase
{
    private static readonly ConcurrentDictionary<string, Template> Templates = new();

    [HttpPost]
    [ProducesResponseType(typeof(Template), StatusCodes.Status201Created)]
    public IActionResult Create([FromBody] CreateTemplateRequest request)
    {
        var template = new Template
        {
            Id = Guid.NewGuid().ToString("N")[..16],
            Name = request.Name, Description = request.Description,
            Channel = request.Channel, Body = request.Body,
            ParameterNames = request.ParameterNames,
            Language = request.Language, Status = TemplateStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        Templates[template.Id!] = template;
        return CreatedAtAction(nameof(Get), new { templateId = template.Id }, template);
    }

    [HttpGet]
    public IActionResult List() => Ok(new { data = Templates.Values.ToList() });

    [HttpGet("{templateId}")]
    public IActionResult Get(string templateId)
        => Templates.TryGetValue(templateId, out var t) ? Ok(t) : NotFound();

    [HttpPatch("{templateId}")]
    public IActionResult Update(string templateId, [FromBody] UpdateTemplateRequest request)
    {
        if (!Templates.TryGetValue(templateId, out var existing)) return NotFound();
        var updated = new Template
        {
            Id = existing.Id, Name = request.Name ?? existing.Name,
            Description = request.Description ?? existing.Description,
            Channel = existing.Channel, Body = request.Body ?? existing.Body,
            ParameterNames = request.ParameterNames ?? existing.ParameterNames,
            Language = request.Language ?? existing.Language,
            Status = existing.Status, CreatedAt = existing.CreatedAt,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        Templates[templateId] = updated;
        return Ok(updated);
    }

    [HttpDelete("{templateId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Delete(string templateId)
        => Templates.TryRemove(templateId, out _) ? NoContent() : NotFound();
}
