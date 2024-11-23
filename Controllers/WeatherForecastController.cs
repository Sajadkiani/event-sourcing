using Marten;
using Microsoft.AspNetCore.Mvc;

namespace event_sourcing.Controllers;

[ApiController]
[Route("[controller]")]
public class TestController : ControllerBase
{
    private readonly ILogger<TestController> _logger;
    private readonly IDocumentSession _session;

    public TestController(ILogger<TestController> logger, IDocumentSession session)
    {
        _logger = logger;
        _session = session;
    }

    [HttpPost]
    public async Task Post()
    {
        var @event = new PromissoryCreated(Guid.NewGuid(), new Random().NextInt64().ToString(), DateTime.Now, Guid.NewGuid(), Guid.NewGuid());
        _session.Events.StartStream(@event.Id, @event);
        await _session.SaveChangesAsync();
    }
}

// public record BaseEvent(Guid Id, DateTime CreatedOn, Guid CreatedBy, Guid RowVersion);

public sealed record PromissoryCreated(Guid Id, string promissoryId, DateTime CreatedOn, Guid CreatedBy, Guid RowVersion);


