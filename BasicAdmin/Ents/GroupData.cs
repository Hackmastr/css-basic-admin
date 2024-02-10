using System.Text.Json.Serialization;

namespace BasicAdmin.Ents;

internal record struct GroupData
{
    [JsonPropertyName("immunity")]
    public int Immunity { get; init; }
    
    [JsonPropertyName("flags")]
    public HashSet<string> Flags { get; init; }
}
