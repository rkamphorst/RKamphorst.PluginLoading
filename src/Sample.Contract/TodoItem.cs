namespace Sample.Contract;

public readonly struct TodoItem
{
    public string Name { get; init; }
    
    public bool IsDone { get; init; }
    
    public DateTimeOffset DueDate { get; init; }
    
}