namespace Cms.Core.Auth;

using Cms.Core.Data.Entities;

public enum AuthenticationOutcome
{
    Success,
    UserNotFound,
    WrongPassword,
    InactiveUser,
}

public sealed record AuthenticationResult(AuthenticationOutcome Outcome, User? User)
{
    public bool IsSuccess => Outcome == AuthenticationOutcome.Success && User is not null;

    public static AuthenticationResult Success(User user) => new(AuthenticationOutcome.Success, user);
    public static AuthenticationResult UserNotFound() => new(AuthenticationOutcome.UserNotFound, null);
    public static AuthenticationResult WrongPassword() => new(AuthenticationOutcome.WrongPassword, null);
    public static AuthenticationResult InactiveUser(User user) => new(AuthenticationOutcome.InactiveUser, user);
}
