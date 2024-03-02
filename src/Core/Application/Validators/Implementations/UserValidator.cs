using FluentValidation;
using OnlineJudgeAdmin.Core.Domain.Models;
using System.Text.RegularExpressions;

namespace OnlineJudgeAdmin.Core.Application.Validators.Implementations
{
    public class UserValidator : AbstractValidator<User>
    {
        private readonly Regex regexName;
        private readonly Regex regexEmail;
        public UserValidator()
        {
            regexName = new Regex("^[a-zA-Z0-9-_ ]+$");
            regexEmail = new Regex(@"^([\w\.\-]+)@([\w\-]+)((\.(\w){2,3})+)$");

            RuleFor(User => User.Name).NotNull()
                .NotEmpty()
                .Length(2, 32)
                .Matches(regexName);

            RuleFor(User => User.Email).NotNull()
                .NotEmpty()
                .Matches(regexEmail);
        }
    }
}