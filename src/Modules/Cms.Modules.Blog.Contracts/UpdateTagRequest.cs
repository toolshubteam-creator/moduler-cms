namespace Cms.Modules.Blog.Contracts;

public sealed record UpdateTagRequest(int Id, string Name, string Slug);
