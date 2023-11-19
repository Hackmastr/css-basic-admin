using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace BasicAdmin;

public class BasicAdminConfig : BasePluginConfig
{
    [JsonPropertyName("admin_tag")]
    public string AdminTag { get; set; } = "\x06[Admin]\x01";
    
    [JsonPropertyName("hide_activity")]
    public bool HideActivity { get; set; } = false;
}
