using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace BasicAdmin;

public class PunishmentDefaults
{
    [JsonPropertyName("ban_duration")]
    public int BanDuration { get; set; } = 30;
    
    [JsonPropertyName("gag_duration")]
    public int GagDuration { get; set; } = 30;
    
    [JsonPropertyName("mute_duration")]
    public int MuteDuration { get; set; } = 30;
}

public class BasicAdminPunishmentsConfig
{
    [JsonPropertyName("defaults")]
    public PunishmentDefaults Defaults { get; set; } = new();
}

public sealed class BasicAdminDatabaseConfig
{
    [JsonPropertyName("host")] 
    public string Host { get; set; } = "localhost";

    [JsonPropertyName("username")]
    public string Username { get; set; } = "user";

    [JsonPropertyName("password")]
    public string Password { get; set; } = "pass";
    
    [JsonPropertyName("database")]
    public string Database { get; set; } = "db_name";
    
    [JsonPropertyName("tablePrefix")]
    public string TablePrefix { get; set; } = "ba_";

    [JsonPropertyName("port")] 
    public int Port { get; set; } = 3306;
    
    /// <summary>
    /// Server ID on the database.
    /// </summary>
    [JsonPropertyName("serverId")]
    public int ServerId { get; set; } = -1;

    public string GetDslString()
    {
        return $"Server={Host};Database={Database};User Id={Username};Password={Password};Port={Port}";
    }
}

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
    
    /// <summary>
    /// Database config.
    /// </summary>
    [JsonPropertyName("database")]
    public BasicAdminDatabaseConfig Database { get; set; } = new BasicAdminDatabaseConfig();
    
    [JsonPropertyName("punishments")]
    public BasicAdminPunishmentsConfig Punishments { get; set; } = new ();
}