using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Infrastructure.Database.Implementations;

namespace OnlineJudgeAdmin.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IProblemRepository _problemRepository;

        public IndexModel(IProblemRepository problemRepository)
        {
            _problemRepository = problemRepository;
        }

        public async Task OnGetAsync()
        {

            Console.WriteLine("dasdsa");
            var tmp = await _problemRepository.GetAllProblemsAsync();
            var g = 1;
            //var users = await _usersRepository.GetAllUsersAsync();
            //var f = 1;

        }
    }
}
