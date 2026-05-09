namespace Cms.Abstractions.Modules;

using Microsoft.AspNetCore.Routing;

public interface IHasEndpoints
{
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
