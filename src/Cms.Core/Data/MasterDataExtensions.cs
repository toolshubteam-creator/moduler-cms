namespace Cms.Core.Data;

using Cms.Core.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class MasterDataExtensions
{
    public static IServiceCollection AddCmsMaster(this IServiceCollection services, IConfiguration configuration)
    {
        var connStr = configuration.GetConnectionString("Master")
            ?? throw new InvalidOperationException("ConnectionStrings:Master tanimli degil.");

        services.AddDbContext<MasterDbContext>(options =>
            options.UseMySql(connStr, ServerVersion.AutoDetect(connStr)));

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();

        return services;
    }
}
