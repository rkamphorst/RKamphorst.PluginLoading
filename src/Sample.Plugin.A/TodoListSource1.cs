using Sample.Contract;

namespace Sample.Plugin.A;

public class TodoListSource1 : ITodoListSource
{
    public Task<IEnumerable<TodoItem>> GetTodoItems()
    {
        return Task.FromResult((IEnumerable<TodoItem>)new[]
        {
            new TodoItem { Name = "Item 1", IsDone = false, DueDate = DateTimeOffset.Now.AddDays(1) },
            new TodoItem { Name = "Item 2", IsDone = true, DueDate = DateTimeOffset.Now.AddDays(-1) }
        });
    }
}