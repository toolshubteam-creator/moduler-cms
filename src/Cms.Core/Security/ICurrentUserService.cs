namespace Cms.Core.Security;

/// <summary>
/// Geceli istek/scope baglamindaki kimligin minimal projeksiyonu.
/// HTTP disinda (Hangfire job, console seed, vb.) bu servis null doner; bu
/// senaryolarda UserId null'dur ve audit kayitlari "system" placeholder yerine
/// nullable UserId ile yazilir.
/// </summary>
public interface ICurrentUserService
{
    int? UserId { get; }

    string? Email { get; }
}
