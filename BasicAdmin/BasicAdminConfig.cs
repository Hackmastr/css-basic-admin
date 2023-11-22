using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace BasicAdmin;

public class BasicAdminConfig : BasePluginConfig
{
    [JsonPropertyName("admin_tag")]
    public string AdminTag { get; set; } = "\x06[Admin]\x01";
    
    [JsonPropertyName("hide_activity")]
    public bool HideActivity { get; set; } = false;
    
    [JsonPropertyName("admin_say_text")]
    public string AdminSayText { get; set; } = $"\x03{{0}}\x01: {{1}}";
    
    [JsonPropertyName("admin_say_text_admins")]
    public string AdminSayTextTeam { get; set; } = $"(Admins only) \x03{{0}}\x01: {{1}}";
    
    [JsonPropertyName("admin_list_min_flag")]
    public string AdminListMinFlag { get; set; } = "@css/kick";
    
    [JsonPropertyName("admin_list_req_flag")]
    public string AdminListReqFlag { get; set; } = string.Empty;
    
    [JsonPropertyName("freeze_duration")]
    public int FreezeDuration { get; set; } = 5;
}
