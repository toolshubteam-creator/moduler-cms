namespace Cms.Core.Auth;

using Cms.Core.Data.Entities;

public interface IUserService
{
    Task<AuthenticationResult> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}
