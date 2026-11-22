using HeroRegistry.Application.Users;
using Microsoft.Extensions.Options;

namespace HeroRegistry.Infrastructure.Security;

/// <summary>Reports every invalid <see cref="RefreshTokenOptions"/> setting at startup, with a clear message.</summary>
internal sealed class RefreshTokenOptionsValidator : IValidateOptions<RefreshTokenOptions>
{
    public ValidateOptionsResult Validate(string? name, RefreshTokenOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var problems = options.Validate();
        return problems.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(problems);
    }
}
