namespace DriverNet.Application.Interface;

public interface ICycleService
{
    DateOnly Month { get; set; }
    
    DateOnly LastMonth { get; set; }
    
    DateOnly Week { get; set; }
    
    DateOnly LastWeek { get; set; }
    
    void StartMonth();
    
    void EndMonth();

    void StartWeek();
    
    void EndWeek();
}