using System.Threading.Tasks;

namespace FluentMigrator.Repl.Code;

public interface IResourceResolver
{
    public Task<string> ResolveResource(string resource);
}