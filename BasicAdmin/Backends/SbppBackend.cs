using System.Data;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using BasicAdmin.Config;
using BasicAdmin.Ents;
using CounterStrikeSharp.API.Modules.Entities;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace BasicAdmin.Backends;

public sealed class SbppBackend : IBackend
{
    private readonly SbppConfig _config;
    private readonly BasicAdmin _context;
    private MySqlConnection _conn = null!;
    private readonly string _prefix;
    private readonly SbppMappingConfig _mappingConfig;
    private readonly int _port;

    public SbppBackend(int hostPort)
    {
        _context = BasicAdmin.Instance!;
        
        CreateConfig();
     
        _config = ReadConfig<SbppConfig>("sbpp");
        _mappingConfig = ReadConfig<SbppMappingConfig>("sbpp_mapping");
        _prefix = _config.Database.TablePrefix;
        _port = hostPort;
    }

    private T ReadConfig<T>(string name)
    {
        var configDir = Path.Combine(_context.ModuleDirectory, "../../", "configs", "plugins", "BasicAdmin");
        var filePath = $"{configDir}/{name.ToLower()}.json";

        if (File.Exists(filePath)) 
            return JsonSerializer.Deserialize<T>(File.ReadAllText(filePath))!;
        
        _context.Logger.LogError($"[Backend: SBPP] Failed to load config at {filePath}.");
        
        return default!;
    }

    private void CreateConfig()
    {
        var configDir = Path.Combine(_context.ModuleDirectory, "../..", "configs", "plugins", "BasicAdmin");
        var sbppConfigPath = $"{configDir}/sbpp.json";

        if (Path.Exists(sbppConfigPath))
            return;
        
        _context.Logger.LogInformation($"[Backend: SBPP] Creating SB++ config at {configDir}.");
        _context.Logger.LogInformation($"[Backend: SBPP] Creating SB++ config at {sbppConfigPath}.");
        
        File.WriteAllText(sbppConfigPath, JsonSerializer.Serialize(new SbppConfig(), new JsonSerializerOptions()
        {
            WriteIndented = true
        }));
            
        File.WriteAllText($"{configDir}/sbpp_mapping.json", JsonSerializer.Serialize(new SbppMappingConfig(), new JsonSerializerOptions()
        {
            WriteIndented = true
        }));
            
        _context.Logger.LogInformation("[Backend: SBPP] Created SB++ config and mapping config.");
    }

    public async Task<bool> Load()
    {
        _conn = new MySqlConnection(_config.Database.GetDslString());
        await _conn.OpenAsync();
        
        if (_conn.State != ConnectionState.Open)
        {
            _context.Logger.LogError("[Backend: SBPP] Failed to connect to the database.");
            return false;
        }

        if (!await FetchData())
        {
            _context.Logger.LogError("[Backend: SBPP] Failed to fetch admins.");
            return false;
        }
        
        _context.Logger.LogInformation("[Backend: SBPP] Successfully loaded.");
        return true;
    }

    private async Task<bool> FetchData()
    {
        var groupsLoaded = await LoadGroups();
        var adminsLoaded = await LoadAdmins();

        _context.Logger.LogInformation($"[Backend: SBPP] Loaded {groupsLoaded} groups and {adminsLoaded} admins.");
        return true;
    }
    
    private async Task<int> LoadGroups()
    {
        var total = 0;
        var groups = new Dictionary<string, GroupData>();
        var sql = $"SELECT name, flags, immunity FROM {_prefix}srvgroups ORDER BY id";
        var cmd = new MySqlCommand(sql, _conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            total++;
            var name = NormalizeGroup(reader.GetString(0));
            var flags = reader.GetString(1);
            var immunity = reader.GetInt32(2);
            groups.Add(name, new GroupData()
            {
                Flags = new HashSet<string>(TranslateFlags(flags)),
                Immunity = immunity
            });
        }
        
        var configDir = Path.GetFullPath(Path.Combine(_context.ModuleDirectory, "../../", "configs"));
        var oldFilepath = $"{configDir}/admin_groups.json.old";
        var filePath = $"{configDir}/admin_groups.json";
        
        if (!Path.Exists(oldFilepath))
        {
            File.Move(filePath, oldFilepath, true);
        }
        
        await File.WriteAllTextAsync(filePath,  JsonSerializer.Serialize(groups, new JsonSerializerOptions()
        {
            WriteIndented = true
        }));
        
        return total;
    }

    private async Task<int> LoadAdmins()
    {
        var total = 0;
        var admins = new Dictionary<string, AdminData>();
        
        string query;
        var rslQuery = _config.RequireSiteLogin ? "lastvisit IS NOT NULL AND lastvisit != '' AND" : "";
        
        if (_config.Database.ServerId == -1)
        {
            // We have to get the external ip using a web request. Hostip cvar doesn't exist.
            var ip = await GetExternalIpAddress();

            query = string.Format("SELECT authid, (SELECT name FROM {0}srvgroups WHERE name = srv_group AND flags != '') AS srv_group, srv_flags, user, immunity " +
                                  "FROM {0}admins_servers_groups AS asg " +
                                  "LEFT JOIN {0}admins AS a ON a.aid = asg.admin_id " +
                                  "WHERE {1} (server_id = (SELECT sid FROM {0}servers WHERE ip = '{2}' AND port = '{3}' LIMIT 0,1) " +
                                  "OR srv_group_id = ANY (SELECT group_id FROM {0}servers_groups WHERE server_id = (SELECT sid FROM {0}servers WHERE ip = '{2}' AND port = '{3}' LIMIT 0,1))) " +
                                  "GROUP BY aid, authid, srv_password, srv_group, srv_flags, user",
                _prefix, rslQuery , ip, _port);
        }
        else
        {
            query = string.Format("SELECT authid, (SELECT name FROM {0}srvgroups WHERE name = srv_group AND flags != '') AS srv_group, srv_flags, user, immunity " +
                                  "FROM {0}admins_servers_groups AS asg " +
                                  "LEFT JOIN {0}admins AS a ON a.aid = asg.admin_id " +
                                  "WHERE {1} server_id = {2} " +
                                  "OR srv_group_id = ANY (SELECT group_id FROM {0}servers_groups WHERE server_id = {2}) " +
                                  "GROUP BY aid, authid, srv_password, srv_group, srv_flags, user",
                _prefix, rslQuery, _config.Database.ServerId);
        }
        
        _context.Logger.LogInformation($"[Backend: SBPP] Querying admins with query: {query}");
        
        var cmd = new MySqlCommand(query, _conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            if (reader.IsDBNull(0))
                continue;
            
            var authId = reader.GetString("authid");
            var group = reader.IsDBNull("srv_group") ? string.Empty : reader.GetString("srv_group");
            var flags = reader.IsDBNull("srv_flags") ? string.Empty : reader.GetString("srv_flags");
            var name = reader.GetString("user");
            var immunity = reader.GetInt32("immunity");
            
            HashSet<string>? groups = null;
            
            if (!string.IsNullOrEmpty(group))
            {
                groups = new HashSet<string>(group.Split(',').Select(NormalizeGroup).ToList());
            }
            
            // Skip if the authid is invalid or the group or flags are empty.
            if ((string.IsNullOrEmpty(group) && string.IsNullOrEmpty(flags)) || !SteamID.TryParse(authId, out var steamId) || steamId == null)
                continue;
            
            total++;
            
            admins.Add(name, new AdminData()
            {
                Identity = authId,
                Immunity = immunity,
                Groups = groups,
                Flags = string.IsNullOrEmpty(flags) ? null : new HashSet<string>(TranslateFlags(flags))
            });
        }
        
        var configDir = Path.GetFullPath(Path.Combine(_context.ModuleDirectory, "../../", "configs"));
        var oldFilepath = $"{configDir}/admins.json.old";
        var filePath = $"{configDir}/admins.json";
        
        if (!Path.Exists(oldFilepath))
        {
            File.Move(filePath, oldFilepath, true);
        }
        
        await File.WriteAllTextAsync(filePath,  JsonSerializer.Serialize(admins, new JsonSerializerOptions()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        }));
        
        return total;
    }

    private IEnumerable<string> TranslateFlags(string smFlags)
    {
        return smFlags.Select(flag => _mappingConfig.Flags.TryGetValue(flag, out var value) ? value : null).Where(s => s != null).ToArray()!;
    }

    private static string NormalizeGroup(string s)
    {
        return  "#sbpp/" + s.Replace(" ", "").ToLower();
    }
    
    private static async Task<string?> GetExternalIpAddress()
    {
        var externalIpString = (await new HttpClient().GetStringAsync("https://icanhazip.com"))
            .Replace(@"\r\n", "").Replace("\\n", "").Trim();
        return !IPAddress.TryParse(externalIpString, out var ipAddress) ? null : ipAddress.ToString();
    }
}
