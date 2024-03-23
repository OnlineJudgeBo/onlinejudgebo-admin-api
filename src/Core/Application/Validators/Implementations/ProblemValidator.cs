using FluentValidation;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Validators.Implementations;

public class ProblemValidator : AbstractValidator<Problem>
{
    public ProblemValidator()
    {
        RuleFor(Problem => Problem.Description)
            .NotNull()
            .NotEmpty();
    }
}
