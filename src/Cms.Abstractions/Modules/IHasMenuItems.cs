namespace Cms.Abstractions.Modules;

using Cms.Abstractions.Modules.Menu;

public interface IHasMenuItems
{
    IReadOnlyList<MenuItem> GetMenuItems();
}
