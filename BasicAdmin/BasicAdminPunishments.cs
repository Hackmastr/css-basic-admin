using BasicAdmin.Ents;
using BasicAdmin.Enums;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using Microsoft.Extensions.Logging;
using static CounterStrikeSharp.API.Modules.Entities.SteamID;

namespace BasicAdmin;

partial class BasicAdmin
{
    [ConsoleCommand("css_ban", "Ban a player.")]
    [CommandHelper(1, "<target or steamid64> [duration] [reason]")]
    [RequiresPermissions("@css/ban")]
    public void OnBanPlayer(CCSPlayerController? caller, CommandInfo info)
    {
        var player = GetTarget(info, false, true)?.Players.First();
        
        var duration = Config.Punishments.Defaults.BanDuration;

        if (!string.IsNullOrEmpty(info.GetArg(2)) && !int.TryParse(info.GetArg(2), out duration))
        {
            info.ReplyToCommand(FormatAdminMessage(Localizer["ba.invalid_value", info.GetArg(2)]));
            return;
        }
        
        var message = !string.IsNullOrEmpty(info.GetArg(3)) ? info.GetArg(3) : Localizer["ba.punishments.defaults.ban"];
        
        var adminId = caller?.AuthorizedSteamID ?? null;
        var playerName = player?.PlayerName ?? "-";
        var playerId = player?.AuthorizedSteamID;
        
        if (playerId is null && (!TryParse(info.GetArg(1), out playerId)))
        {
            info.ReplyToCommand(FormatMessage(Localizer["ba.punishments.target.authorized_steam_id_invalid", info.GetArg(1)]));
            return;
        }
        
        // if (!_adminMgr.TryGetValue(caller.AuthorizedSteamID!.SteamId64, out var adminId) || playerId == null)
        
        Task.Run(async () =>
        {
            if (await IsAlreadyPunished(info, playerId!, playerName, PunishmentType.Ban)) return;
            
            var res = await _punishmentMgr.Ban(adminId, playerId!, playerName, duration, message);
            
            if (!res)
            {
                Server.NextFrame(() =>
                {
                    Server.PrintToChatAll($"Error banning player ({playerId!.SteamId64})");
                    info.ReplyToCommand(FormatMessage(Localizer["ba.punishments.error", playerId.SteamId64]));
                });
                
                Logger.LogError($"Error banning player ({playerId!.SteamId64})");
                
                return;
            }
            
            Server.NextFrame(() =>
            {
                if (player is { IsValid: true })
                    ServerUtils.KickPlayer(player.UserId, Localizer["ba.punishments.defaults.ban"]);
                
                if (!Config.HideActivity)
                    Server.PrintToChatAll(FormatAdminMessage(
                        Localizer[(duration == 0 ? "ba.punishments.target.banned_perm" : "ba.punishments.target.banned"), player?.PlayerName ?? playerId!.SteamId64.ToString(), duration]
                        ));
            });
        });
    }
    
    [ConsoleCommand("css_gag", "Gag a player.")]
    [CommandHelper(1, "<target or steamid64> [duration] [reason]")]
    [RequiresPermissions("@css/ban")]
    public void OnGagPlayer(CCSPlayerController? caller, CommandInfo info)
    {
        var player = GetTarget(info, false, true)?.Players.First();
        
        var duration = Config.Punishments.Defaults.GagDuration;

        if (!string.IsNullOrEmpty(info.GetArg(2)) && !int.TryParse(info.GetArg(2), out duration))
        {
            info.ReplyToCommand(FormatAdminMessage(Localizer["ba.invalid_value", info.GetArg(2)]));
            return;
        }
        
        var message = !string.IsNullOrEmpty(info.GetArg(3)) ? info.GetArg(3) : Localizer["ba.punishments.defaults.gag"];
        
        var adminId = caller?.AuthorizedSteamID ?? null;
        var playerName = player?.PlayerName ?? "-";
        var playerId = player?.AuthorizedSteamID;

        if (playerId is null)
            TryParse(info.GetArg(1), out playerId);

        if (playerId is null)
        {
            info.ReplyToCommand(FormatMessage(Localizer["ba.punishments.target.authorized_steam_id_invalid", info.GetArg(1)]));
            return;
        }
        
        Task.Run(async () =>
        {
            if (await IsAlreadyPunished(info, playerId, playerName, PunishmentType.Gag)) return;

            var res = await _punishmentMgr.Gag(adminId, playerId, playerName, duration, message);
            
            if (!res)
            {
                Server.NextFrame(() =>
                {
                    info.ReplyToCommand(FormatMessage(Localizer["ba.punishments.error"]));
                });
                
                Logger.LogError($"Error gagging player ({playerId.SteamId64})");
                
                return;
            }
            
            
            Server.NextFrame(() =>
            {
                var playerTwo = Utilities.GetPlayerFromSteamId(playerId.SteamId64);

                if (playerTwo is { IsValid: true })
                {
                    if (!ActivePunishments.ContainsKey(playerTwo.Handle))
                        ActivePunishments.Add(playerTwo.Handle, new HashSet<ActivePunishment>());
                    
                    ActivePunishments[playerTwo.Handle].Add(new ActivePunishment
                    {
                        Type = PunishmentType.Gag,
                        Length = duration,
                        ExpiresAt = DateTime.Now.AddSeconds(duration)
                    });
                }
                
                if (Config.HideActivity)
                    return;
                
                Server.PrintToChatAll(FormatAdminMessage(
                    Localizer[(duration == 0 ? "ba.punishments.target.gagged_perm" : "ba.punishments.target.gagged"), playerId.SteamId64, duration]
                    ));
            });
        });
    }
    
    [ConsoleCommand("css_mute", "Mute a player.")]
    [CommandHelper(1, "<target or steamid64> [duration] [reason]")]
    [RequiresPermissions("@css/ban")]
    public void OnMutePlayer(CCSPlayerController? caller, CommandInfo info)
    {
        var player = GetTarget(info, false, true)?.Players.First();
        
        var duration = Config.Punishments.Defaults.MuteDuration;

        if (!string.IsNullOrEmpty(info.GetArg(2)) && !int.TryParse(info.GetArg(2), out duration))
        {
            info.ReplyToCommand(FormatAdminMessage(Localizer["ba.invalid_value", info.GetArg(2)]));
            return;
        }
        
        var message = !string.IsNullOrEmpty(info.GetArg(3)) ? info.GetArg(3) : Localizer["ba.punishments.defaults.mute"];
        
        var adminId = caller?.AuthorizedSteamID ?? null;
        var playerName = player?.PlayerName ?? "-";
        var playerId = player?.AuthorizedSteamID;
        
        if (playerId is null)
            TryParse(info.GetArg(1), out playerId);

        if (playerId is null)
        {
            info.ReplyToCommand(FormatMessage(Localizer["ba.punishments.target.authorized_steam_id_invalid", info.GetArg(1)]));
            return;
        }
        
        Task.Run(async () =>
        {
            if (await IsAlreadyPunished(info, playerId, playerName, PunishmentType.Mute)) return;

            var res = await _punishmentMgr.Mute(adminId, playerId, playerName, duration, message);
            
            if (!res)
            {
                Server.NextFrame(() =>
                {
                    info.ReplyToCommand(FormatMessage(Localizer["ba.punishments.error"]));
                });
                
                Logger.LogError($"Error muting player ({playerId.SteamId64})");
                
                return;
            }
            
            
            Server.NextFrame(() =>
            {
                var playerTwo = Utilities.GetPlayerFromSteamId(playerId.SteamId64);

                if (playerTwo is { IsValid: true })
                {
                    if (!ActivePunishments.ContainsKey(playerTwo.Handle))
                        ActivePunishments.Add(playerTwo.Handle, new HashSet<ActivePunishment>());
                    
                    ActivePunishments[playerTwo.Handle].Add(new ActivePunishment
                    {
                        Type = PunishmentType.Mute,
                        Length = duration,
                        ExpiresAt = DateTime.Now.AddSeconds(duration)
                    });

                    MutedPlayers.Add(playerTwo.Slot);
                }
                
                if (Config.HideActivity)
                    return;
                
                Server.PrintToChatAll(FormatAdminMessage(
                    Localizer[(duration == 0 ? "ba.punishments.target.muted_perm" : "ba.punishments.target.muted"), playerId.SteamId64, duration]
                    ));
            });
        });
    }

    [ConsoleCommand("css_unban", "Unban a player.")]
    [CommandHelper(1, "<target or steamid64>")]
    [RequiresPermissions("@css/ban")]
    public void OnUnbanPlayer(CCSPlayerController? caller, CommandInfo info)
    {
        var player = GetTarget(info, false, true)?.Players.First();
        var adminId = caller?.AuthorizedSteamID?.SteamId64 ?? 0;
        
        var playerId = player?.AuthorizedSteamID;
        
        if (playerId is null)
            TryParse(info.GetArg(1), out playerId);

        if (playerId is null)
        {
            info.ReplyToCommand(FormatMessage(Localizer["ba.punishments.target.authorized_steam_id_invalid", info.GetArg(1)]));
            return;
        }
        
        Task.Run(async () =>
        {
            var res = await _punishmentMgr.Unban(playerId);
            
            if (!res)
            {
                Server.NextFrame(() =>
                {
                    info.ReplyToCommand(FormatMessage(Localizer["ba.punishments.error"]));
                });
                
                Logger.LogError($"Error unbanning player ({playerId.SteamId64})");
                return;
            }
            
            Logger.LogInformation($" {adminId} unbanned player ({playerId.SteamId64})");
            
            Server.NextFrame(() =>
            {
                if (Config.HideActivity)
                    return;
                
                caller?.PrintToChat(FormatAdminMessage(
                    Localizer["ba.punishments.target.unbanned", playerId.SteamId64]
                    ));
            });
        });
    }

    [ConsoleCommand("css_ungag", "Ungag a player.")]
    [CommandHelper(1, "<target or steamid64>")]
    [RequiresPermissions("@css/ban")]
    public void OnUngagPlayer(CCSPlayerController? caller, CommandInfo info)
    {
        var player = GetTarget(info, false, true)?.Players.First();
        var adminId = caller?.AuthorizedSteamID?.SteamId64 ?? 0;
        
        var playerId = player?.AuthorizedSteamID;
        
        if (playerId is null)
            TryParse(info.GetArg(1), out playerId);

        if (playerId is null)
        {
            info.ReplyToCommand(FormatMessage(Localizer["ba.punishments.target.authorized_steam_id_invalid", info.GetArg(1)]));
            return;
        }
        
        Task.Run(async () =>
        {
            var res = await _punishmentMgr.Ungag(playerId);
            
            if (!res)
            {
                Server.NextFrame(() =>
                {
                    info.ReplyToCommand(FormatMessage(Localizer["ba.punishments.error"]));
                });
                
                Logger.LogError($"Error ungagging player ({playerId.SteamId64})");
                return;
            }
            
            Logger.LogInformation($" {adminId} ungagged player ({playerId.SteamId64})");
            
            Server.NextFrame(() =>
            {
                var playerTwo = Utilities.GetPlayerFromSteamId(playerId.SteamId64);

                if (playerTwo is { IsValid: true } && ActivePunishments.ContainsKey(playerTwo.Handle))
                {
                    ActivePunishments[playerTwo.Handle].RemoveWhere(p => p.Type == PunishmentType.Gag);
                }

                if (Config.HideActivity)
                    return;
                
                Server.PrintToChatAll(FormatAdminMessage(
                    Localizer["ba.punishments.target.ungagged", playerTwo?.PlayerName ?? playerId.SteamId64.ToString() ]
                    ));
            });
        });
    }

    [ConsoleCommand("css_unmute", "Unmute a player.")]
    [CommandHelper(1, "<target or steamid64>")]
    [RequiresPermissions("@css/ban")]
    public void OnUnmutePlayer(CCSPlayerController? caller, CommandInfo info)
    {
        var player = GetTarget(info, false, true)?.Players.First();
        var adminId = caller?.AuthorizedSteamID?.SteamId64 ?? 0;
        
        var playerId = player?.AuthorizedSteamID;

        if (playerId is null)
            TryParse(info.GetArg(1), out playerId);

        if (playerId is null)
        {
            info.ReplyToCommand(FormatMessage(Localizer["ba.punishments.target.authorized_steam_id_invalid", info.GetArg(1)]));
            return;
        }
        Task.Run(async () =>
        {
            var res = await _punishmentMgr.Unmute(playerId);
            
            if (!res)
            {
                Server.NextFrame(() =>
                {
                    info.ReplyToCommand(FormatMessage(Localizer["ba.punishments.error"]));
                });
                
                Logger.LogError($"Error unmuting player ({playerId.SteamId64})");
                return;
            }
            
            Logger.LogInformation($" {adminId} unmuted player ({playerId.SteamId64})");
            
            Server.NextFrame(() =>
            {
                var playerTwo = Utilities.GetPlayerFromSteamId(playerId.SteamId64);

                if (playerTwo is { IsValid: true } && ActivePunishments.ContainsKey(playerTwo.Handle))
                {
                    ActivePunishments[playerTwo.Handle].RemoveWhere(p => p.Type == PunishmentType.Mute);
                    MutedPlayers.RemoveWhere(i => i == playerTwo.Slot);
                }

                if (Config.HideActivity)
                    return;
                
                Server.PrintToChatAll(FormatAdminMessage(
                    Localizer["ba.punishments.target.unmuted", playerTwo?.PlayerName ?? playerId.SteamId64.ToString() ]
                    ));
            });
        });
    }
}

