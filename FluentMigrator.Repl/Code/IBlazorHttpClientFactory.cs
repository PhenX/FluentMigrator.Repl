using System.Net.Http;
using System.Threading.Tasks;

namespace FluentMigrator.Repl.Code;

public interface IBlazorHttpClientFactory
{
    Task<HttpClient> CreateHttpClient();
}