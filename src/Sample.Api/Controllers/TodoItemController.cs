using Microsoft.AspNetCore.Mvc;
using Sample.Contract;

namespace Sample.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class TodoItemController : ControllerBase
{
    private readonly IEnumerable<ITodoListSource> _todoListSources;
    private readonly ILogger<TodoItemController> _logger;

    public TodoItemController(IEnumerable<ITodoListSource> todoListSources, ILogger<TodoItemController> logger)
    {
        _todoListSources = todoListSources;
        _logger = logger;
    }

    [HttpGet(Name = "GetTodoList")]
    public async Task<IEnumerable<TodoItem>> Get()
    {
        return (await Task.WhenAll(_todoListSources.Select(s => s.GetTodoItems()))).SelectMany(x => x);
    }
}