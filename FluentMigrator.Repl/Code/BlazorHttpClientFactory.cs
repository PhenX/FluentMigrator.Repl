using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace FluentMigrator.Repl.Code;

public class BlazorHttpClientFactory : IBlazorHttpClientFactory
{
    private readonly IJSRuntime _jsRuntime;

    public BlazorHttpClientFactory(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }
    
    public async Task<HttpClient> CreateHttpClient()
    {
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(await _jsRuntime.InvokeAsync<string>("getBaseUrl")),
        };

        return httpClient;
    }
}