using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace FluentMigrator.Repl.Code;

public class ResourceResolver : IResourceResolver
{
    private readonly IBlazorHttpClientFactory _httpClientFactory;
    private HttpClient? _httpClient;
    private readonly Lazy<Task<Dictionary<string, string>>> _resourceMappings;

    public ResourceResolver(IBlazorHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
        _resourceMappings = new Lazy<Task<Dictionary<string, string>>>(FetchResourcesAsync);
    }
    
    public async Task<string> ResolveResource(string logicalName)
    {
        if (string.IsNullOrWhiteSpace(logicalName))
            throw new ArgumentException("Logical name cannot be null or empty.", nameof(logicalName));

        var resources = await _resourceMappings.Value;

        if (resources.TryGetValue(logicalName, out var hashedName))
        {
            return hashedName;
        }

        throw new FileNotFoundException($"Resource '{logicalName}' not found in blazor.boot.json.");
    }

    private async Task<Dictionary<string, string>> FetchResourcesAsync()
    {
        _httpClient ??= await _httpClientFactory.CreateHttpClient();
        
        var baseUri = GetBaseUri(_httpClient);
        var bootJsonUrl = $"{baseUri}/_framework/blazor.boot.json";
        var bootJsonContent = await _httpClient.GetStringAsync(bootJsonUrl);

        var bootJson = JsonSerializer.Deserialize<BlazorBootJson>(bootJsonContent);
        if (bootJson?.Resources?.Fingerprinting == null)
        {
            throw new InvalidOperationException("Invalid blazor.boot.json structure.");
        }

        // Combine all relevant resources into one dictionary for easy lookup
        var allResources = new Dictionary<string, string>();

        foreach (var resource in bootJson.Resources.Fingerprinting.Where(resource => !allResources.ContainsKey(resource.Value)))
        {
            allResources.Add(resource.Value, resource.Key);
        }

        return allResources;
    }

    private static string GetBaseUri(HttpClient httpClient)
    {
        return httpClient.BaseAddress!.ToString().TrimEnd('/');
    }
}