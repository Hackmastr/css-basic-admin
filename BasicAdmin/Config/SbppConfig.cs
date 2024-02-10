using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace BasicAdmin.Config;

public sealed class SbppMappingConfig 
{
    public int Version { get; set; } = 1;

    [JsonPropertyName("groups")]
    [JsonInclude]
    public Dictionary<string, string> Groups = new()
    {
        {"admins", "#css/admins"},
        {"reservations", "#css/reservation"},
        {"vip", "#css/vip"},
        {"root", "#css/root"}
    };
    
    [JsonPropertyName("flags")]
    [JsonInclude]
    public Dictionary<char, string> Flags = new ()
    {
        {'a', "@css/reservation"},
        {'b', "@css/generic"},
        {'c', "@css/kick"},
        {'d', "@css/ban"},
        {'e', "@css/unban"},
        // {",", "@css/vip"} // unmapped flag.
        {'f', "@css/slay"},
        {'g', "@css/changemap"},
        {'h', "@css/cvar"},
        {'l', "@css/config"},
        {'j', "@css/chat"},
        {'k', "@css/vote"},
        {'i', "@css/password"},
        {'m', "@css/rcon"},
        {'n', "@css/cheats"},
        {'z', "@css/root"}
    };
}

public sealed class SbppConfig
{
    public int Version { get; set; } = 1;
    
    /// <summary>
    /// Require site login at least once.
    /// </summary>
    [JsonPropertyName("requireSiteLogin")]
    public bool RequireSiteLogin { get; set; } = false;
    
    [JsonPropertyName("database")]
    public BasicAdminDatabaseConfig Database { get; set; } = new ();
}