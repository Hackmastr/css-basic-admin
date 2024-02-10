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

public sealed class MySqlBackend : IBackend
{
    private readonly BasicAdmin _context;
    private MySqlConnection _conn;
    private readonly string _prefix;

    public MySqlBackend()
    {
        _context = BasicAdmin.Instance!;
        _prefix = _context.Config.Database.TablePrefix;
    }

    public async Task<bool> Load()
    {
        _conn = _context.Database.GetConnection();
        
        if (!await FetchData())
        {
            _context.Logger.LogError("[Backend: MySql] Failed to fetch admins.");
            return false;
        }
        
        _context.Logger.LogInformation("[Backend: MySql] Successfully loaded.");
        return true;
    }

    private async Task<bool> FetchData()
    {
        var groupsLoaded = await LoadGroups();
        var adminsLoaded = await LoadAdmins();

        _context.Logger.LogInformation($"[Backend: MySql] Loaded {groupsLoaded} groups and {adminsLoaded} admins.");
        return true;
    }
    
    private async Task<int> LoadGroups()
    {
        var total = 0;
        var groups = new Dictionary<string, GroupData>();
        var sql = string.Format("""
            SELECT g.name, g.immunity, GROUP_CONCAT(`value` SEPARATOR '||') `flag`
            FROM
                `{0}groups` g LEFT JOIN {0}flag_con fc ON fc.group_id = g.id
            LEFT JOIN {0}flags f ON fc.flag_id = f.id
            GROUP BY
            g.id
        """, _prefix);
        var cmd = new MySqlCommand(sql, _conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            total++;
            var name = reader.GetString(0);
            var immunity = reader.GetInt32(1);
            var flags = reader.GetString(2);
            
            groups.Add(name, new GroupData()
            {
                Flags = new HashSet<string>(flags.Split("||").ToList()),
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
        
        var query = string.Format("""
            SELECT
                a.nick,
                a.steamid64,
                GROUP_CONCAT(DISTINCT f.value) AS 'Flags',
                GROUP_CONCAT(DISTINCT g.name) AS 'Groups',
                GREATEST(MAX(g.immunity), IFNULL(a.immunity, 0)) as `immunity`
            FROM
                {0}admins a
                    LEFT JOIN
                {0}flag_con fc ON a.id = fc.admin_id
                    LEFT JOIN
                {0}flags f ON fc.flag_id = f.id
                    LEFT JOIN
                {0}group_admins ga ON a.id = ga.admin_id
                    LEFT JOIN
                `{0}groups` g ON ga.group_id = g.id
            WHERE g.server_id is null OR g.server_id = {1}
            GROUP BY a.id;                      
         """, _prefix, _context.Config.Database.ServerId);
        
        
        var cmd = new MySqlCommand(query, _conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            var name = reader.GetString(0);
            var steamId64 = reader.GetString(1);
            var group = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
            var flags = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
            var immunity = reader.GetInt32(4);
            
            HashSet<string>? groups = null;
            
            if (!string.IsNullOrEmpty(group))
            {
                groups = new HashSet<string>();
            }
            
            // Skip if the authid is invalid or the group or flags are empty.
            if (string.IsNullOrEmpty(group) && string.IsNullOrEmpty(flags))
                continue;
            
            total++;
            
            admins.Add(name, new AdminData()
            {
                Identity = steamId64,
                Immunity = immunity,
                Groups = groups,
                Flags = string.IsNullOrEmpty(flags) ? null : new HashSet<string>(){flags}
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
}
