namespace Sample.Contract;

public interface ITodoListSource
{
    Task<IEnumerable<TodoItem>> GetTodoItems();
}