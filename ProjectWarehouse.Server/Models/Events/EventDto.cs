namespace ProjectWarehouse.Server.Models.Events;

public class EventDto
{
    public AppEntity AppEntity { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
}