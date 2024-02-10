﻿using BasicAdmin.Ents;
using BasicAdmin.Enums;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;

namespace BasicAdmin;

partial class BasicAdmin
{
    private void PunishmentTimer()
    {
        _punishmentMgr.ExpirePunishments();
        
        foreach (var (handle, punishments) in ActivePunishments)
        {
            foreach (var punishment in punishments)
            {
                if (DateTime.Now < punishment.ExpiresAt)
                    continue;
            
                var player = new CCSPlayerController(handle);
            
                if (player is not { IsValid: true } || !ActivePunishments.ContainsKey(player.Handle))
                    continue;
            
                ActivePunishments[handle].RemoveWhere(p => p.Type == punishment.Type);

                if (punishment.Type == PunishmentType.Mute)
                    MutedPlayers.RemoveWhere(i => i == player.Slot);
            
                player.PrintToChat(FormatAdminMessage(Localizer[punishment.Type == PunishmentType.Gag ? "ba.punishments.target.gag_expired" : "ba.punishments.target.mute_expired"]));
            }
        }
    }

    private Task FetchActivePunishments(int slot, SteamID id)
    {
        return Task.Run(async () =>
        {
            var punishments = await _punishmentMgr.GetActivePunishments(id);
            if (punishments.Count == 0)
                return;

            Server.NextFrame(() =>
            {
                var player = Utilities.GetPlayerFromSlot(slot);
                if (player?.IsValid != true)
                    return;
                
                ActivePunishments.Add(player.Handle, new HashSet<ActivePunishment>());
            
                punishments.ForEach(activePunishment =>
                {
                    ActivePunishments[player.Handle].Add(activePunishment);
                    
                    if (activePunishment.Type == PunishmentType.Mute)
                        MutedPlayers.Add(player.Slot);
                });
            });
        });
    }

    private Task<bool> KickPlayerIfBanned(int slot, SteamID id)
    {
        return Task.Run(async () =>
        {
            var isBanned = await _punishmentMgr.HasActivePunishment(id, PunishmentType.Ban);
            if (!isBanned)
                return false;

            Server.NextFrame(() =>
            {
                var playerId = Utilities.GetPlayerFromSlot(slot).UserId;
                
                ServerUtils.KickPlayer(playerId, Localizer["ba.punishments.defaults.ban"]);
            });
            
            return true;
        });
    }

    private async Task<bool> IsAlreadyPunished(CommandInfo info, SteamID playerId, string playerName, PunishmentType type)
    {
        var isAlreadyBanned = await _punishmentMgr.HasActivePunishment(playerId, type);

        if (!isAlreadyBanned) return false;
        
        Server.NextFrame(() =>
        {
            info.ReplyToCommand(FormatMessage(Localizer["ba.punishments.target.already_punished",
                playerName]));
        });
        
        return true;
    }
}
