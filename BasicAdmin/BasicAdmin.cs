﻿using System.Reflection;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CSSTargetResult = CounterStrikeSharp.API.Modules.Commands.Targeting.TargetResult;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using Microsoft.Extensions.Logging;

namespace BasicAdmin;

[MinimumApiVersion(98)]
public sealed class BasicAdmin : BasePlugin, IPluginConfig<BasicAdminConfig>
{
    public override string ModuleName => "BasicAdmin";
    public override string ModuleAuthor => "livevilog";
    public override string ModuleVersion => "1.6.0";
    
    public BasicAdminConfig Config {get; set;} = new ();
    
    private static readonly Dictionary<IntPtr, bool> ActiveGodMode = new ();
    
    public void OnConfigParsed(BasicAdminConfig config)
    {
        this.Config = config;
    }

    public override void Load(bool hotReload)
    {
        base.Load(hotReload);
        
        AddCommandListener("say", OnSayCommand);
        AddCommandListener("say_team", OnSayCommand);
        VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Hook(OnTakeDamage, HookMode.Pre);
    }

    public override void Unload(bool hotReload)
    {
        base.Unload(hotReload);
        
        VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Unhook(OnTakeDamage, HookMode.Pre);
    }


    private static HookResult OnTakeDamage(DynamicHook hook)
    {
        var entindex = hook.GetParam<CEntityInstance>(0).Index;
       
        if (entindex == 0)
            return HookResult.Continue;

        var pawn = Utilities.GetEntityFromIndex<CCSPlayerPawn>((int)entindex);
        
        if (pawn.OriginalController.Value is not { } player)
            return HookResult.Continue;
        
        if (ActiveGodMode.ContainsKey(player.Handle))
        {
            hook.GetParam<CTakeDamageInfo>(1).Damage = 0;
        }

        return HookResult.Continue;
    }

    private HookResult OnSayCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (!(info.GetArg(1).StartsWith('@') && AdminManager.PlayerHasPermissions(caller, "@css/chat")))
            return HookResult.Continue;

        var isTeam = info.GetArg(0).Length > 4;
        var start = isTeam ? 11 : 6;
        string message;

        if (isTeam)
        {
            message = string.Format(Config.AdminSayTextTeam, caller!.PlayerName,
                info.GetCommandString[start..^1]);
        }
        else
        {
            message = FormatAdminMessage(string.Format(Config.AdminSayText, caller!.PlayerName,
                info.GetCommandString[start..^1]));
        }
        
        if (isTeam)
            ServerUtils.PrintToChatTeam(TargetFilter.Admin, message);
        else
            Server.PrintToChatAll(message);
        
        return HookResult.Stop;
    }

    [GameEventHandler]
    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        ActiveGodMode.Clear();
        
        return HookResult.Continue;
    }
    
    [ConsoleCommand("css_map", "Change map.")]
    [CommandHelper(1, "<mapname>")]
    [RequiresPermissions("@css/changemap")]
    public void OnMapCommand(CCSPlayerController? caller, CommandInfo info)
    {
        var map = info.GetArg(1);
        
        if (!Server.IsMapValid(map))
        {
            info.ReplyToCommand(FormatMessage($"Map {map} not found."));
            return;
        }
        
        Server.PrintToChatAll(FormatAdminMessage($"Changing map to {map}..."));
        
        AddTimer(3f, () =>
        {
            Server.ExecuteCommand($"changelevel {map}");
            // caller.Discon
        });
        
        Logger.LogInformation($"{caller!.PlayerName} changed map to {map}.");
    }
    
    [ConsoleCommand("css_wsmap", "Change map.")]
    [ConsoleCommand("css_workshop", "Change map.")]
    [CommandHelper(1, "<name or id>")]
    [RequiresPermissions("@css/changemap")]
    public void OnWorkshopMapCommand(CCSPlayerController? caller, CommandInfo info)
    {
        var map = info.GetArg(1);

        var command = ulong.TryParse(map, out var mapId) ? $"host_workshop_map {mapId}" : $"ds_workshop_changelevel {map}";
        
        // if (mapId == 0 && !Server.IsMapValid(map))
        // {
        //     info.ReplyToCommand(FormatMessage($"Map {map} not found."));
        //     return;
        // }
        
        Server.PrintToChatAll(FormatAdminMessage($"Changing map to {map}..."));
        
        AddTimer(3f, () =>
        {
            Server.ExecuteCommand(command);
        });
        
        Logger.LogInformation($"{caller!.PlayerName} changed map to {map}.");
    }
    
    [ConsoleCommand("css_kick", "Kick a player from the server.")]
    [CommandHelper(1, "<#userid or name> [reason]")]
    [RequiresPermissions("@css/kick")]
    public void OnKickCommand(CCSPlayerController? caller, CommandInfo info)
    {
        var target = GetTarget(info);
        
        var reason = info.GetArg(2);
        
        target?.Players.ForEach(player =>
        {
            if (!AdminManager.CanPlayerTarget(caller, player))
            {
                info.ReplyToCommand(FormatMessage("You can't target this player."));
                return;
            }

            ServerUtils.KickPlayer(player.UserId, reason);
            
            if (!Config.HideActivity)
                Server.PrintToChatAll(FormatAdminMessage($"{caller!.PlayerName} kicked {player.PlayerName}."));
        });
    }

    [ConsoleCommand("css_slay", "Slay a player.")]
    [CommandHelper(1, "<#userid or name>")]
    [RequiresPermissions("@css/slay")]
    public void OnSlayCommand(CCSPlayerController? caller, CommandInfo info)
    {
        GetTarget(info)?.Players.ForEach(player =>
        {
            if (!AdminManager.CanPlayerTarget(caller, player))
            {
                info.ReplyToCommand(FormatMessage("You can't target this player."));
                return;
            }
            player.Pawn.Value?.CommitSuicide(false, true);
            
            if (!Config.HideActivity)
                Server.PrintToChatAll(FormatAdminMessage($"{caller!.PlayerName} slayed {player.PlayerName}."));
        });
    }
    
    [ConsoleCommand("css_give", "Give a player an item.")]
    [CommandHelper(2, "<#userid or name> <item name>")]
    [RequiresPermissions("@css/cvar")]
    public void OnGiveCommand(CCSPlayerController? caller, CommandInfo info)
    {
        var target = GetTarget(info);

        var range = info.GetArg(0).Length + info.GetArg(1).Length + 2;
        var item = info.GetCommandString[range..];
        
        target?.Players.ForEach(player =>
        {
            player.GiveNamedItem(item);
            
            if (!Config.HideActivity)
                Server.PrintToChatAll(FormatAdminMessage($"{caller!.PlayerName} gave {player.PlayerName} {ChatColors.Lime}{item}{ChatColors.Default}."));
        });
    }
    
    [ConsoleCommand("css_swap", "Swap a player.")]
    [CommandHelper(1, "<#userid or name>")]
    [RequiresPermissions("@css/kick")]
    public void OnSwapCommand(CCSPlayerController? caller, CommandInfo info)
    {
        var target = GetTarget(info);
        target?.Players.ForEach(player =>
        {
            if (!AdminManager.CanPlayerTarget(caller, player))
            {
                info.ReplyToCommand(FormatMessage("You can't target this player."));
                return;
            }
            if ((int) CsTeam.Spectator == player.TeamNum)
            {
                info.ReplyToCommand(FormatMessage($"Target {info.GetArg(1)} is a spectator."));
                return;
            }
     
            var isCs = player.TeamNum == (int) CsTeam.CounterTerrorist;
        
            player.ChangeTeam(isCs ? CsTeam.Terrorist : CsTeam.CounterTerrorist);
        
            if (!Config.HideActivity)
                Server.PrintToChatAll(FormatAdminMessage($"{caller!.PlayerName} swapped {player.PlayerName}."));
        });
    }
    
    [ConsoleCommand("css_forcespec", "Change a player to spec.")]
    [CommandHelper(1, "<#userid or name>")]
    [RequiresPermissions("@css/kick")]
    public void OnForceSpecCommand(CCSPlayerController? caller, CommandInfo info)
    {
        var target = GetTarget(info);
        target?.Players.ForEach(player =>
        {
            if (!AdminManager.CanPlayerTarget(caller, player))
            {
                info.ReplyToCommand(FormatMessage("You can't target this player."));
                return;
            }
            player.ChangeTeam(CsTeam.Spectator);
        
            if (!Config.HideActivity)
                Server.PrintToChatAll(FormatAdminMessage($"{caller!.PlayerName} moved {player.PlayerName} to spec."));
        });
    }
    
    [ConsoleCommand("css_respawn", "Respawn a dead player.")]
    [CommandHelper(1, "<#userid or name>")]
    [RequiresPermissions("@css/kick")]
    public void OnRespawnCommand(CCSPlayerController? caller, CommandInfo info)
    {
        var target = GetTarget(info);
        target?.Players.ForEach(player =>
        {
            if (!AdminManager.CanPlayerTarget(caller, player))
            {
                info.ReplyToCommand(FormatMessage("You can't target this player."));
                return;
            }
            player.Respawn();
        
            if (Config.HideActivity)
                Server.PrintToChatAll(FormatAdminMessage($"{caller!.PlayerName} respawned {player.PlayerName}."));
        });
    }
    
    [ConsoleCommand("css_say", "Say to all players.")]
    [CommandHelper(1, "<message>")]
    [RequiresPermissions("@css/chat")]
    public void OnAdminSayCommand(CCSPlayerController? caller, CommandInfo info)
    {
        Server.PrintToChatAll(FormatAdminMessage(info.GetCommandString[info.GetCommandString.IndexOf(' ')..]));
    }
    
    [ConsoleCommand("css_psay", "Private message a player.")]
    [CommandHelper(2, "<#userid or name> <message>")]
    [RequiresPermissions("@css/chat")]
    public void OnAdminPrivateSayCommand(CCSPlayerController? caller, CommandInfo info)
    {
        var target = GetTarget(info);
        
        var range = info.GetArg(0).Length + info.GetArg(1).Length + 2;
        var message = info.GetCommandString[range..];
        
        target?.Players.ForEach(player =>
        {
            info.ReplyToCommand(FormatAdminMessage($"({player.PlayerName}) {message}"));
            player.PrintToChat(FormatAdminMessage($"({caller!.PlayerName}) {message}"));
        });
    }
    
    [ConsoleCommand("css_csay", "Say to all players (in center).")]
    [CommandHelper(1, "<message>")]
    [RequiresPermissions("@css/chat")]
    public void OnAdminCenterSayCommand(CCSPlayerController? caller, CommandInfo info)
    {
        ServerUtils.PrintToCenterAll(FormatAdminMessage(info.GetCommandString[info.GetCommandString.IndexOf(' ')..]));
    }
    
    [ConsoleCommand("css_hsay", "Say to all players (in hud).")]
    [CommandHelper(1, "<message>")]
    [RequiresPermissions("@css/chat")]
    public void OnAdminHudSayCommand(CCSPlayerController? caller, CommandInfo info)
    {
        VirtualFunctions.ClientPrintAll(
            HudDestination.Alert, 
            FormatAdminMessage(info.GetCommandString[info.GetCommandString.IndexOf(' ')..]), 
            0, 0, 0, 0);
    }
    
    [ConsoleCommand("css_extend", "Extend map timelimit.")]
    [CommandHelper(1, "<minutes>")]
    [RequiresPermissions("@css/changemap")]
    public void OnExtendCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (!int.TryParse(info.GetArg(1), out var time))
        {
            info.ReplyToCommand(FormatMessage($"Invalid time {info.GetArg(1)}"));
            return;
        }

        var timelimit = ConVar.Find("mp_timelimit");
        timelimit!.SetValue(timelimit.GetPrimitiveValue<float>() + time);
        
        if (!Config.HideActivity)
            Server.PrintToChatAll(FormatAdminMessage($"{caller!.PlayerName} extended the map."));
    }
    
    [ConsoleCommand("css_rr", "Restart game.")]
    [ConsoleCommand("css_restartgame", "Restart game.")]
    [RequiresPermissions("@css/changemap")]
    public void OnRestartGameCommand(CCSPlayerController? caller, CommandInfo info)
    {
        Server.ExecuteCommand("mp_restartgame 1");
        
        if (!Config.HideActivity)
            Server.PrintToChatAll(FormatAdminMessage($"{caller!.PlayerName} restarted the game."));
        
        Logger.LogInformation($"{caller!.PlayerName} restarted the game.");
    }
    
    [ConsoleCommand("css_bury", "Bury a player.")]
    [CommandHelper(1, "<#userid or name> [duration]")]
    [RequiresPermissions("@css/ban")]
    public void OnBuryCommand(CCSPlayerController? caller, CommandInfo info)
    {
        var target = GetTarget(info);
        
        var duration = 0;
        
        if (!string.IsNullOrEmpty(info.GetArg(2)) && !int.TryParse(info.GetArg(2), out duration))
        {
            info.ReplyToCommand(FormatMessage($"Invalid duration value."));
            return;
        }
        
        target?.Players.ForEach(player =>
        {
            if (!AdminManager.CanPlayerTarget(caller, player))
            {
                info.ReplyToCommand(FormatMessage("You can't target this player."));
                return;
            }
            
            player.Pawn.Value?.Bury();

            if (duration > 0)
                AddTimer(duration, () => player.Pawn.Value?.Unbury());
        
            if (!Config.HideActivity)
                Server.PrintToChatAll(FormatAdminMessage($"{caller?.PlayerName} buried {player.PlayerName}."));
        });
    }
    
    [ConsoleCommand("css_unbury", "Unbury a player.")]
    [CommandHelper(1, "<#userid or name>")]
    [RequiresPermissions("@css/ban")]
    public void OnUnburyCommand(CCSPlayerController? caller, CommandInfo info)
    {
        GetTarget(info)?.Players.ForEach(player =>
        {
            if (!AdminManager.CanPlayerTarget(caller, player))
            {
                info.ReplyToCommand(FormatMessage("You can't target this player."));
                return;
            }
            
            player.Pawn.Value?.Unbury();
        
            if (!Config.HideActivity)
                Server.PrintToChatAll(FormatAdminMessage($"{caller!.PlayerName} unburied {player.PlayerName}."));
        });
    }
    
    [ConsoleCommand("css_disarm", "Disarm a player.")]
    [CommandHelper(1, "<#userid or name>")]
    [RequiresPermissions("@css/ban")]
    public void OnDisarmCommand(CCSPlayerController? caller, CommandInfo info)
    {
        GetTarget(info)?.Players.ForEach(player =>
        {
            if (!AdminManager.CanPlayerTarget(caller, player))
            {
                info.ReplyToCommand(FormatMessage("You can't target this player."));
                return;
            }
            
            player.RemoveWeapons();
            
            if (!Config.HideActivity)
                Server.PrintToChatAll(FormatAdminMessage($"{caller!.PlayerName} disarmed {player.PlayerName}."));
        });
    }
    
    [ConsoleCommand("css_hp", "Change a player's HP.")]
    [CommandHelper(2, "<#userid or name> <health>")]
    [RequiresPermissions("@css/slay")]
    public void OnHealthCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (!int.TryParse(info.GetArg(2), out var health))
        {
            info.ReplyToCommand(FormatMessage($"Invalid health value."));
            return;
        }

        GetTarget(info)?.Players.ForEach(player =>
        {
            if (!AdminManager.CanPlayerTarget(caller, player))
            {
                info.ReplyToCommand(FormatMessage("You can't target this player."));
                return;
            }
            
            player.Pawn.Value!.Health = health;
        
            if (!Config.HideActivity)
                Server.PrintToChatAll(FormatAdminMessage($"{caller!.PlayerName} changed {player.PlayerName}'s health to {health}."));
        });
    }
    
    [ConsoleCommand("css_cvar", "Change a cvar.")]
    [CommandHelper(2, "<cvar> <value>")]
    [RequiresPermissions("@css/cvar")]
    public void OnCvarCommand(CCSPlayerController? caller, CommandInfo info)
    {
        var cvar = ConVar.Find(info.GetArg(1));

        if (cvar == null)
        {
            info.ReplyToCommand(FormatMessage($"Cvar \"{info.GetArg(1)}\" not found."));
            return;
        }

        if (cvar.Name.Equals("sv_cheats") && !AdminManager.PlayerHasPermissions(caller, "@css/cheats"))
        {
            info.ReplyToCommand(FormatMessage($"You don't have permissions to change \"{info.GetArg(1)}\"."));
            return;
        }

        var value = info.GetArg(2);
        
        Server.ExecuteCommand($"{cvar.Name} {value}");
        
        info.ReplyToCommand($"{caller!.PlayerName} changed cvar {cvar.Name} to {value}.");
        
        Logger.LogInformation($"{caller.PlayerName} changed cvar {cvar.Name} to {value}.");
    }
    
    [ConsoleCommand("css_admins", "Show connected admins.")]
    public void OnAdminsCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (!string.IsNullOrEmpty(Config.AdminListReqFlag) && !AdminManager.PlayerHasPermissions(caller, Config.AdminListReqFlag))
        {
            info.ReplyToCommand("[CSS] You don't have permissions to use this command.");
            return;
        }
        
        var admins = Utilities.GetPlayers().FindAll(x => AdminManager.PlayerHasPermissions(x, Config.AdminListMinFlag));
        
        var message = admins.Aggregate($" {ChatColors.Lime}Connected admins: {ChatColors.Green}\u2029", 
            (current, admin) => 
                current + $"{admin.PlayerName}\u2029");
        
        info.ReplyToCommand(message);
    }
    
    
    [ConsoleCommand("css_admin_help", "Show available admin commands.")]
    [RequiresPermissions("@css/generic")]
    public void OnAdminHelpCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (!string.IsNullOrEmpty(Config.AdminListReqFlag) && !AdminManager.PlayerHasPermissions(caller, Config.AdminListReqFlag))
        {
            info.ReplyToCommand("[CSS] You don't have permissions to use this command.");
            return;
        }
        
        var currentCommandIndex = 1;
        
        info.ReplyToCommand(FormatMessage("Help printed to your console."));
        
        caller!.PrintToConsole(FormatMessage(
            CommandHandlers.Aggregate($"Available commands: \u2029",
                (s, pair) => s + ChatColors.Lime + pair.Key.GetMethodInfo().GetCustomAttributes<ConsoleCommandAttribute>().First().Command + 
                             ChatColors.Default + (currentCommandIndex++ % 3 == 0 ? ",\u2029" : ", "))[..^2]
        ));
    }
    
    [ConsoleCommand("css_slap", "Slap a player.")]
    [CommandHelper(1, "<#userid or name> [damage]")]
    [RequiresPermissions("@css/slay")]
    public void OnSlapCommand(CCSPlayerController? caller, CommandInfo info)
    {
        var damage = 0;
        
        if (!string.IsNullOrEmpty(info.GetArg(2)) && !int.TryParse(info.GetArg(2), out damage))
        {
            info.ReplyToCommand(FormatMessage($"Invalid damage value."));
            return;
        }

        GetTarget(info)?.Players.ForEach(player =>
        {
            if (!AdminManager.CanPlayerTarget(caller, player))
            {
                info.ReplyToCommand(FormatMessage("You can't target this player."));
                return;
            }
        
            player.Pawn.Value!.Slap(damage);
        
            if (!Config.HideActivity)
                Server.PrintToChatAll(FormatAdminMessage($"{caller!.PlayerName} slapped {player.PlayerName}."));
        });
    }
    
    [ConsoleCommand("css_freeze", "Freeze a player.")]
    [CommandHelper(1, "<#userid or name> [duration]")]
    [RequiresPermissions("@css/slay")]
    public void OnFreezeCommand(CCSPlayerController? caller, CommandInfo info)
    {
        var duration = Config.FreezeDuration;
        
        if (!string.IsNullOrEmpty(info.GetArg(2)) && !int.TryParse(info.GetArg(2), out duration))
        {
            info.ReplyToCommand(FormatMessage($"Invalid duration value."));
            return;
        }
        
        GetTarget(info)?.Players.ForEach(player =>
        {
            if (!AdminManager.CanPlayerTarget(caller, player))
            {
                info.ReplyToCommand(FormatMessage("You can't target this player."));
                return;
            }
            
            player.Pawn.Value!.Freeze();
        
            AddTimer(duration, () => player.Pawn.Value!.Unfreeze());
        
            if (!Config.HideActivity)
                Server.PrintToChatAll(FormatAdminMessage($"{caller!.PlayerName} froze {player.PlayerName}."));
        });
    }
    
    [ConsoleCommand("css_unfreeze", "Unfreeze a player.")]
    [CommandHelper(1, "<#userid or name>")]
    [RequiresPermissions("@css/slay")]
    public void OnUnfreezeCommand(CCSPlayerController? caller, CommandInfo info)
    {
        GetTarget(info)?.Players.ForEach(player =>
        {
            if (!AdminManager.CanPlayerTarget(caller, player))
            {
                info.ReplyToCommand(FormatMessage("You can't target this player."));
                return;
            }
            
            player.Pawn.Value!.Unfreeze();
        
            if (!Config.HideActivity)
                Server.PrintToChatAll(FormatAdminMessage($"{caller!.PlayerName} unfroze {player.PlayerName}."));
        });
    }
    
    [ConsoleCommand("css_noclip", "Noclip a player.")]
    [CommandHelper(1, "<#userid or name>")]
    [RequiresPermissions("@css/cheats")]
    public void OnNoclipCommand(CCSPlayerController? caller, CommandInfo info)
    {
        GetTarget(info)?.Players.ForEach(player =>
        {
            if (!AdminManager.CanPlayerTarget(caller, player))
            {
                info.ReplyToCommand(FormatMessage("You can't target this player."));
                return;
            }
            
            player.Pawn.Value!.ToggleNoclip();
        
            if (!Config.HideActivity)
                Server.PrintToChatAll(FormatAdminMessage($"{caller!.PlayerName} toggled noclip on {player.PlayerName}."));
        });
    }
    
    [ConsoleCommand("css_godmode", "Godmode a player.")]
    [CommandHelper(1, "<#userid or name>")]
    [RequiresPermissions("@css/cheats")]
    public void OnGodmodeCommand(CCSPlayerController? caller, CommandInfo info)
    {
        GetTarget(info)?.Players.ForEach(player =>
        {
            if (!AdminManager.CanPlayerTarget(caller, player))
            {
                info.ReplyToCommand(FormatMessage("You can't target this player."));
                return;
            }
            
            if (!ActiveGodMode.Remove(player.Handle))
            {
                ActiveGodMode[player.Handle] = true;
            }

            if (!Config.HideActivity)
                Server.PrintToChatAll(FormatAdminMessage($"{caller!.PlayerName} toggled godmode on {player.PlayerName}."));
        });
    }
    
    [ConsoleCommand("css_rcon", "Run a server console command.")]
    [CommandHelper(1, "<command>")]
    [RequiresPermissions("@css/rcon")]
    public void OnRconCommand(CCSPlayerController? caller, CommandInfo info)
    {
        info.ReplyToCommand($"Command executed ({info.ArgString}).");
        
        Server.ExecuteCommand(info.ArgString);
        
        Logger.LogInformation($"{caller!.PlayerName} executed command ({info.ArgString}).");
    }
    
    [ConsoleCommand("css_vote", "Start a vote.")]
    [RequiresPermissions("@css/vote")]
    public void OnVoteCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount < 2)
        {
            info.ReplyToCommand("Usage: css_vote <question> [answer1] [answer2] [answer3] ...");
            return;
        }

        // Voting.StartVote(caller, info);
    }
    
    private static CSSTargetResult? GetTarget(CommandInfo info)
    {
        var matches = info.GetArgTargetResult(1);

        if (!matches.Any()) {
            info.ReplyToCommand(FormatMessage($"Target {info.GetArg(1)} not found."));
            return null;
        }

        if (!(matches.Count() > 1) || info.GetArg(1).StartsWith('@')) 
            return matches;
        
        info.ReplyToCommand(FormatMessage($"Multiple targets found for \"{info.GetArg(1)}\"."));
        
        return null;
    }
    
    private static string FormatMessage(string message) => $" {ChatColors.Lime}[BasicAdmin]{ChatColors.Default} {message}";
    private string FormatAdminMessage(string message) => $" {Config.AdminTag} {message}";
}