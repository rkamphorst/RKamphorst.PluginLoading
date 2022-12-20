using Sample.Contract;

namespace Sample.Plugin.A;

public class TodoListSource2 : ITodoListSource
{
    public Task<IEnumerable<TodoItem>> GetTodoItems()
    {
        return Task.FromResult((IEnumerable<TodoItem>)new[]
        {
            new TodoItem { Name = "Todo 1", IsDone = false, DueDate = DateTimeOffset.Now.AddDays(3) },
            new TodoItem { Name = "Todo 2", IsDone = true, DueDate = DateTimeOffset.Now.AddDays(-6) }
        });
    }
}