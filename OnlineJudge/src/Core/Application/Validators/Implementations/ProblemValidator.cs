using FluentValidation;
using ScheduleManager.Core.Domain.Models;

namespace ScheduleManager.Core.Application.Validators.Implementations;

public class ProblemValidator : AbstractValidator<Problem>
{
    public ProblemValidator()
    {
        RuleFor(Problem => Problem.Description)
            .NotNull()
            .NotEmpty();
    }
}
