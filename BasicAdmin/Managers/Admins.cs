using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Modules.Entities;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace BasicAdmin.Managers;


public sealed class Admins
{
    private readonly BasicAdmin _context;
    private readonly MySqlConnection _conn;
    private readonly string _prefix;
    private readonly Dictionary<ulong, int> _admins = new();

    public Admins(BasicAdmin context)
    {
        _context = context;
        _conn = context._database.GetConnection();
        _prefix = context.Config.Database.TablePrefix;
    }

    public async Task<bool> LoadAdmin(SteamID id)
    {
        if (_admins.ContainsKey(id.SteamId64))
            return false;
        
        var query = $"SELECT id FROM {_prefix}admins WHERE steamid64 = @sid";
        await using var cmd = new MySqlCommand(query, _conn);
        
        cmd.Parameters.AddWithValue("@sid", id.SteamId64);
        
        await using var reader = await cmd.ExecuteReaderAsync();
        
        if (!reader.HasRows)
            return false;
        
        await reader.ReadAsync();
        
        _admins.Add(id.SteamId64, reader.GetInt32("id"));
        
        return true;
    }
    
    public async Task<bool> AddAdmin(SteamID id, string name, int immunity = 0)
    {
        var query = $"INSERT INTO {_prefix}admins (steamid64, nick, immunity) VALUES (@sid, @name, @immunity)";
        await using var cmd = new MySqlCommand(query, _conn);
        
        cmd.Parameters.AddWithValue("@sid", id.SteamId64);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@immunity", immunity);
        
        try
        {
            await cmd.ExecuteNonQueryAsync();
            _admins.Add(id.SteamId64, (int) cmd.LastInsertedId);
            return true;
        } catch (Exception e)
        {
            _context.Logger.LogError(e, $"Failed to add admin {id.SteamId64}");
        }
        
        return false;
    }
    
    public async Task<bool> RemoveAdmin(SteamID id)
    {
        var query = $"DELETE FROM {_prefix}admins WHERE steamid64 = @sid";
        await using var cmd = new MySqlCommand(query, _conn);
        
        cmd.Parameters.AddWithValue("@sid", id.SteamId64);
        
        try
        {
            await cmd.ExecuteNonQueryAsync();
            _admins.Remove(id.SteamId64);
            return true;
        } catch (Exception e)
        {
            _context.Logger.LogError(e, $"Failed to remove admin {id.SteamId64}");
        }
        
        return false;
    }

    public bool TryGetValue(ulong steamId64, out int o)
    {
        return _admins.TryGetValue(steamId64, out o);
    }
}
