using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Infrastructure.Database.Implementations;

namespace OnlineJudgeAdmin.Pages
{
public class IndexModel : PageModel
{
    private readonly IUsersRepository _usersRepository;

    public IndexModel(IUsersRepository usersRepository)
    {
        _usersRepository = usersRepository;
    }

    public async Task OnGetAsync()
    {
        var users = await _usersRepository.GetAllUsersAsync();
        var f = 1;
    }
}
}
