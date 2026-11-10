using FluentValidation;

namespace Application.Auth;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(r => r.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(r => r.Password).NotEmpty().MaximumLength(200);
    }
}
