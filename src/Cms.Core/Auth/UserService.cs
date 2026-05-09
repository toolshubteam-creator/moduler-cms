namespace Cms.Core.Auth;

using Cms.Core.Data;
using Cms.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;

public sealed class UserService(MasterDbContext db, IPasswordHasher hasher) : IUserService
{
    public async Task<AuthenticationResult> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            return AuthenticationResult.UserNotFound();
        }

        var normalized = email.Trim().ToLowerInvariant();
        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == normalized, cancellationToken);

        if (user is null)
        {
            return AuthenticationResult.UserNotFound();
        }

        if (!hasher.Verify(password, user.PasswordHash))
        {
            return AuthenticationResult.WrongPassword();
        }

        if (!user.IsActive)
        {
            return AuthenticationResult.InactiveUser(user);
        }

        var tracked = await db.Users.FirstAsync(u => u.Id == user.Id, cancellationToken);
        tracked.LastLoginAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return AuthenticationResult.Success(tracked);
    }

    public Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
}
