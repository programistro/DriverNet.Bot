using DriverNet.Application.Interface;

namespace DriverNet.Application.Service;

public class CycleService : ICycleService
{
    public DateOnly Month { get; set; }
    public DateOnly LastMonth { get; set; }
    public DateOnly Week { get; set; }
    public DateOnly LastWeek { get; set; }
    
    public void StartMonth()
    {
        Month = DateOnly.FromDateTime(DateTime.Now.AddMonths(-1));
    }

    public void EndMonth()
    {
        LastMonth = DateOnly.FromDateTime(DateTime.Now.AddMonths(1));
    }

    public void StartWeek()
    {
        Week = DateOnly.FromDateTime(DateTime.Now.AddMonths(-1));
    }

    public void EndWeek()
    {
        LastMonth = DateOnly.FromDateTime(DateTime.Now.AddMonths(1));
    }
}