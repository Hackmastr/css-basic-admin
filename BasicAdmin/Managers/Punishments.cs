using BasicAdmin.Ents;
using BasicAdmin.Enums;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Modules.Entities;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace BasicAdmin.Managers;

internal sealed class Punishments
{
    private readonly MySqlConnection _conn;
    private readonly BasicAdmin _context;
    private readonly string _prefix;
    
    public Punishments(BasicAdmin context)
    {
        _conn = context.Database.GetConnection();
        _context = context;
        _prefix = context.Config.Database.TablePrefix;
    }
    
    public Task<bool> Ban(SteamID? adminId, SteamID target, string targetName, int duration, string reason)
    {
        return Add(adminId, target, targetName, PunishmentType.Ban, duration, reason);
    }
    
    public Task<bool> Unban(SteamID target)
    {
        return Expire(target, PunishmentType.Ban);
    }
    
    public Task<bool> Gag(SteamID? adminId, SteamID target, string targetName, int duration, string reason)
    {
        return Add(adminId, target, targetName, PunishmentType.Gag, duration, reason);
    }
    
    public Task<bool> Ungag(SteamID target)
    {
        return Expire(target, PunishmentType.Gag);
    }
    
    public Task<bool> Mute(SteamID? adminId, SteamID target, string targetName, int duration, string reason)
    {
        return Add(adminId, target, targetName, PunishmentType.Mute, duration, reason);
    }
    
    public Task<bool> Unmute(SteamID target)
    {
        return Expire(target, PunishmentType.Mute);
    }

    public async Task<bool> HasActivePunishment(SteamID steamId, PunishmentType type)
    {
        var query = $"SELECT * FROM {_prefix}punishments WHERE target = @sid AND type = @type AND length > 0";
        await using var cmd = new MySqlCommand(query, _conn);
        
        cmd.Parameters.AddWithValue("@sid", steamId.SteamId64);
        cmd.Parameters.AddWithValue("@type", (int) type);

        try
        {
            await using var reader = await cmd.ExecuteReaderAsync();
            return reader.HasRows;
        } catch (Exception e)
        {
            _context.Logger.LogError(e, $"Failed to check if {steamId.SteamId64} has active punishment");
        }
        
        return false;
    }

    public async Task<List<ActivePunishment>> GetActivePunishments(SteamID id)
    {
        var query = $"SELECT `type`, `length`, `expires_at`  FROM {_prefix}punishments WHERE target = @sid AND length > 0 AND expires_at > @now";
        await using var cmd = new MySqlCommand(query, _conn);
        
        cmd.Parameters.AddWithValue("@sid", id.SteamId64);
        cmd.Parameters.AddWithValue("@now", DateTime.Now);

        try
        {
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!reader.HasRows)
                return new List<ActivePunishment>();
            
            var punishments = new List<ActivePunishment>();
            while (await reader.ReadAsync())
            {
                punishments.Add(new ActivePunishment
                {
                    // Id = reader.GetInt32("id"),
                    Type = (PunishmentType) reader.GetInt32("type"),
                    Length = reader.GetInt32("length"),
                    ExpiresAt = reader.GetDateTime("expires_at")
                });
            }

            return punishments;
            
        } catch (Exception e)
        {
            _context.Logger.LogError(e, $"Failed to get active punishments for {id.SteamId64}");
        }
        
        return new List<ActivePunishment>();
    }

    private async Task<bool> Add(SteamID? adminId, SteamID target, string targetName, PunishmentType type, int duration, string reason)
    {
        var query = $"INSERT INTO {_prefix}punishments (server_id, admin_id, target, target_name, reason, length, type, expires_at) VALUES (@sid, @aid, @tid, @tname, @reason, @length, @type, @expiresAt)";
        await using var cmd = new MySqlCommand(query, _conn);
        
        cmd.Parameters.AddWithValue("@sid", _context.Config.Database.ServerId);
        cmd.Parameters.AddWithValue("@aid", adminId?.SteamId64 ?? 0);
        cmd.Parameters.AddWithValue("@tid", target.SteamId64);
        cmd.Parameters.AddWithValue("@tname", targetName);
        cmd.Parameters.AddWithValue("@reason", reason);
        cmd.Parameters.AddWithValue("@length", duration);
        cmd.Parameters.AddWithValue("@type", (int) type);
        cmd.Parameters.AddWithValue("@expiresAt", DateTime.Now.AddMinutes(duration));

        try
        {
            await cmd.ExecuteNonQueryAsync();
        } catch (Exception e)
        {
            _context.Logger.LogError(e, $"Failed to add punishment for {targetName} ({target.SteamId64})");
            return false;
        }
        
        return true;
    }
    
    private async Task<bool> Expire(SteamID target, PunishmentType type)
    {
        var query = $"UPDATE {_prefix}punishments SET length = -1 WHERE target = @sid AND type = @type";
        await using var cmd = new MySqlCommand(query, _conn);
        
        cmd.Parameters.AddWithValue("@sid", target.SteamId64);
        cmd.Parameters.AddWithValue("@type", (int) type);

        try
        {
            var res = await cmd.ExecuteNonQueryAsync();
            return res > 0;
        } catch (Exception e)
        {
            _context.Logger.LogError(e, $"Failed to expire punishment for {target.SteamId64}");
        }

        return false;
    }

    public async void ExpirePunishments()
    {
        try
        {
            await using var cmd = new MySqlCommand("ExpireBans", _conn);
            cmd.CommandType = System.Data.CommandType.StoredProcedure;
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _context.Logger.LogError(ex, "Failed to expire punishments");
        }
    }
}
