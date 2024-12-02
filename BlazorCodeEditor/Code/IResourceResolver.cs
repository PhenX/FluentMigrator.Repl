namespace BlazorCodeEditor.Code;

public interface IResourceResolver
{
    public Task<string> ResolveResource(string resource);
}