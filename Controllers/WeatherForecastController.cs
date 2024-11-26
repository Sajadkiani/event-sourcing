using Marten;
using Marten.Events;
using Marten.Events.Aggregation;
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

    [HttpPost("promissory/add")]
    public async Task<Guid> AddPromissory()
    {
        var @event = new PromissoryCreated(Guid.NewGuid(),new Random().NextInt64().ToString(),1, DateTime.UtcNow, Guid.NewGuid(), Guid.NewGuid());

        _session.Events.StartStream(@event.Id, @event);
        await _session.SaveChangesAsync();

        return @event.Id;
    }

    [HttpPost("promissory/guarantor/add")]
    public async Task<IActionResult> AddGuarantor([FromBody] AddGuarantorDto dto)
    {
        var stream = await _session.Events.FetchStreamAsync(dto.PId);
        if (stream == null)
        {
            return BadRequest($"Stream with ID {dto.PId} does not exist.");
        }

        var @event = new GuarantorCreated(dto.PId, 2,
            new List<PromissoryGuarantorAdded>
            {
                new PromissoryGuarantorAdded(Guid.NewGuid(), dto.NationalId, dto.Name, dto.Family, DateTime.UtcNow, Guid.NewGuid(), Guid.NewGuid())
            });

        _session.Events.Append(dto.PId, @event);
        await _session.SaveChangesAsync();

        return Ok();
    }
    
    [HttpPost("promissory/done")]
    public async Task<IActionResult> Done([FromBody] DoneDto dto)
    {
        var stream = await _session.Events.FetchStreamAsync(dto.PId);
        if (stream == null)
        {
            return BadRequest($"Stream with ID {dto.PId} does not exist.");
        }

        var @event = new PromissoryDone(dto.PId, 3, DateTime.Now, Guid.NewGuid());

        _session.Events.Append(dto.PId, @event);
        await _session.SaveChangesAsync();

        return Ok();
    }
    
    [HttpGet("promissory/{id}")]
    public async Task<IActionResult> GetPromissory(Guid id)
    {
        var promissory = await _session.Events.AggregateStreamAsync<Promissory>(id);
        if (promissory == null)
        {
            return NotFound($"Promissory with ID {id} not found.");
        }

        return Ok(promissory);
    }
    
    [HttpGet("promissory/all")]
    public async Task<IActionResult> GetAllPromissory()
    {
        var finishedPromissories = 
            await _session.Query<GetAllPromissory>()
            .ToListAsync();
        
        return Ok(finishedPromissories);
    }
}

#region projections


public class Promissory
{
    public Guid Id { get; private set; }
    public string PromissoryId { get; private set; }    
    public int State { get; private set; }
    public List<PromissoryGuarantorAdded> Guarantors { get; private set; } = new();
    public void Apply(PromissoryCreated @event)
    {
        Id = @event.Id;
        PromissoryId = @event.PromissoryId;
    }

    public void Apply(GuarantorCreated @event)
    {
        Guarantors.AddRange(@event.Guarantors);
    }
    
    public void Apply(PromissoryDone @event)
    {
        State = @event.State;
    }
}

public record GetAllPromissory
{
    public Guid Id { get;  set; }
    public string PromissoryId { get;  set; }
    public int State { get;  set; }
    public DateTime Created { get;  set; }
    public Guid CreatedBy { get;  set; }
    public int GuarantorCount { get;  set; }
}

public sealed class PromissoryProjection :  SingleStreamProjection<GetAllPromissory>
{
    public static GetAllPromissory Created(PromissoryCreated @event)
    {
        var promissory = new GetAllPromissory
        {
            Id = @event.Id,
            PromissoryId = @event.PromissoryId,
            Created = @event.CreatedOn,
            CreatedBy = @event.CreatedBy,
            State = @event.State
        };
        
        Console.WriteLine("event: " + @event.Id);
        return promissory;
    }

    public static GetAllPromissory Apply(GuarantorCreated @event, GetAllPromissory promissory)
    {
        promissory.GuarantorCount += @event.Guarantors.Count;
        Console.WriteLine("event: " + @event.Id);
        return promissory;
    }

    public static GetAllPromissory Apply(PromissoryDone @event, GetAllPromissory promissory)
    {
        promissory.State = @event.State;
        Console.WriteLine("event: " + @event.Id);

        return promissory;
    }
}

#endregion

#region DTO

public record AddGuarantorDto(Guid PId, string NationalId, string Name, string Family);
public record DoneDto(Guid PId);

#endregion

#region events

public sealed record PromissoryCreated(Guid Id, string PromissoryId, int State, DateTime CreatedOn, Guid CreatedBy, Guid RowVersion);

public sealed record GuarantorCreated(Guid Id, int State, List<PromissoryGuarantorAdded> Guarantors);
public sealed record PromissoryDone(Guid Id, int State, DateTime Created, Guid CreatedBy);

public sealed record PromissoryGuarantorAdded(Guid Id, string NationalId, string Name, string Family, DateTime CreatedOn, Guid CreatedBy, Guid RowVersion);

#endregion
