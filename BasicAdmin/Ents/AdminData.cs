using System.Text.Json.Serialization;

namespace BasicAdmin.Ents;

internal record struct AdminData
{
    [JsonPropertyName("identity")]
    public string Identity { get; init; }
    
    [JsonPropertyName("immunity")]
    public int Immunity { get; init; }
    
    [JsonPropertyName("groups")]
    public HashSet<string>? Groups { get; init; }
    
    [JsonPropertyName("flags")]
    public HashSet<string>? Flags { get; init; }
}