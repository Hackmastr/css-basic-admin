using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Memory;

namespace BasicAdmin;

// [MinimumApiVersion(66)]
public sealed class BasicAdmin : BasePlugin, IPluginConfig<BasicAdminConfig>
{
    public override string ModuleName => "BasicAdmin";
    public override string ModuleAuthor => "livevilog";
    public override string ModuleVersion => "1.2.0";
    
    public BasicAdminConfig Config {get; set;} = new ();
    
    public void OnConfigParsed(BasicAdminConfig config)
    {
        this.Config = config;
    }

    public override void Load(bool hotReload)
    {
        base.Load(hotReload);
        
        AddCommandListener("say", OnSayCommand);
        AddCommandListener("say_team", OnSayCommand);
    }

    // public override void Unload(bool hotReload)
    // {
    //     base.Unload(hotReload);
    //     
    //     RemoveCommandListener("say", OnSayCommand, HookMode.Pre);
    //     RemoveCommandListener("say_team", OnSayCommand, HookMode.Pre);
    // }

    public HookResult OnSayCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (!(info.GetArg(1).StartsWith('@') && AdminManager.PlayerHasPermissions(caller, "@css/chat")))
            return HookResult.Continue;

        var isTeam = info.GetArg(0).Equals("say_team");
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
    }
    
    [ConsoleCommand("css_wsmap", "Change map.")]
    [ConsoleCommand("css_workshop", "Change map.")]
    [CommandHelper(1, "<mapid>")]
    [RequiresPermissions("@css/changemap")]
    public void OnWorkshopMapCommand(CCSPlayerController? caller, CommandInfo info)
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
            Server.ExecuteCommand($"ds_workshop_changelevel {map}");
        });
    }
    
    [ConsoleCommand("css_kick", "Kick a player from the server.")]
    [CommandHelper(1, "<#userid or name> [reason]")]
    [RequiresPermissions("@css/kick")]
    public void OnKickCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (!GetTarget(info, out var player)) 
            return;
        
        var reason = info.GetArg(2);
        
        ServerUtils.KickPlayer(player!.UserId, reason);
        
        if (!Config.HideActivity)
            Server.PrintToChatAll(FormatAdminMessage($"{caller!.PlayerName} kicked {player.PlayerName}."));
    }

    [ConsoleCommand("css_slay", "Slay a player.")]
    [CommandHelper(1, "<#userid or name>")]
    [RequiresPermissions("@css/slay")]
    public void OnSlayCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (!GetTarget(info, out var player)) 
            return;
        
        player!.Pawn.Value.CommitSuicide(false, true);
        
        if (!Config.HideActivity)
            Server.PrintToChatAll(FormatAdminMessage($"{caller!.PlayerName} slayed {player.PlayerName}."));
    }
    
    [ConsoleCommand("css_give", "Give a player an item.")]
    [CommandHelper(2, "<#userid or name> <item name>")]
    [RequiresPermissions("@css/cvar")]
    public void OnGiveCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (!GetTarget(info, out var player))
            return;

        var range = info.GetArg(0).Length + info.GetArg(1).Length + 2;
        var item = info.GetCommandString[range..];
        
        player!.GiveNamedItem(item);
        
        if (!Config.HideActivity)
            Server.PrintToChatAll(FormatAdminMessage($"{caller!.PlayerName} gave {player.PlayerName} {ChatColors.Lime}{item}{ChatColors.Default}."));
    }
    
    [ConsoleCommand("css_swap", "Swap a player.")]
    [CommandHelper(1, "<#userid or name>")]
    [RequiresPermissions("@css/kick")]
    public void OnSwapCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (!GetTarget(info, out var player)) 
            return;
        
        if ((int) CsTeam.Spectator == player!.TeamNum)
        {
            info.ReplyToCommand(FormatMessage($"Target {info.GetArg(1)} is a spectator."));
            return;
        }
     
        var isCs = player.TeamNum == (int) CsTeam.CounterTerrorist;
        
        player.ChangeTeam(isCs ? CsTeam.Terrorist : CsTeam.CounterTerrorist);
        
        if (!Config.HideActivity)
            Server.PrintToChatAll(FormatAdminMessage($"{caller!.PlayerName} swapped {player.PlayerName}."));
    }
    
    [ConsoleCommand("css_spec", "Change a player to spec.")]
    [CommandHelper(1, "<#userid or name>")]
    [RequiresPermissions("@css/kick")]
    public void OnSpecCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (!GetTarget(info, out var player)) 
            return;
        
        player!.ChangeTeam(CsTeam.Spectator);
        
        if (!Config.HideActivity)
            Server.PrintToChatAll(FormatAdminMessage($"{caller!.PlayerName} moved {player.PlayerName} to spec."));
    }
    
    // [ConsoleCommand("css_respawn", "Respawn a dead player.")]
    // [CommandHelper(1, "<#userid or name>")]
    // [RequiresPermissions("@css/kick")]
    // public void OnRespawnCommand(CCSPlayerController? caller, CommandInfo info)
    // {
    //     if (!ServerUtils.GetTarget(info.GetArg(1), out var player))
    //     {
    //         info.ReplyToCommand(FormatMessage($"Target {info.GetArg(1)} not found."));
    //         return;
    //     }
    //     
    //     player!.DispatchSpawn();
    //     
    //     if (Config.HideActivity)
    //         Server.PrintToChatAll(FormatAdminMessage($"{caller!.PlayerName} respawned {player!.PlayerName}."));
    // }
    
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
        if (!GetTarget(info, out var player)) 
            return;

        var range = info.GetArg(0).Length + info.GetArg(1).Length + 2;
        var message = info.GetCommandString[range..];
        
        info.ReplyToCommand(FormatAdminMessage($"({player!.PlayerName}) {message}"));
        player.PrintToChat(FormatAdminMessage($"({caller!.PlayerName}) {message}"));
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
    }
    
    [ConsoleCommand("css_bury", "Bury a player.")]
    [CommandHelper(1, "<#userid or name>")]
    [RequiresPermissions("@css/ban")]
    public void OnBuryCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (!GetTarget(info, out var player)) 
            return;

        var newPos = new Vector(player!.Pawn.Value.AbsOrigin!.X, player.Pawn.Value.AbsOrigin.Y,
            player.Pawn.Value.AbsOrigin.Z - 10f);
        
        player.Pawn.Value.Teleport(newPos, player.AbsRotation!, player.AbsVelocity);
        
        if (!Config.HideActivity)
            Server.PrintToChatAll(FormatAdminMessage($"{caller!.PlayerName} buried {player.PlayerName}."));
    }
    
    [ConsoleCommand("css_unbury", "Unbury a player.")]
    [CommandHelper(1, "<#userid or name>")]
    [RequiresPermissions("@css/ban")]
    public void OnUnburyCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (!GetTarget(info, out var player)) 
            return;

        var newPos = new Vector(player!.Pawn.Value.AbsOrigin!.X, player.Pawn.Value.AbsOrigin.Y,
            player.Pawn.Value.AbsOrigin!.Z + 15f);
        
        player.Pawn.Value.Teleport(newPos, player.AbsRotation!, player.AbsVelocity);
        
        if (!Config.HideActivity)
            Server.PrintToChatAll(FormatAdminMessage($"{caller!.PlayerName} unburied {player.PlayerName}."));
    }
    
    [ConsoleCommand("css_disarm", "Disarm a player.")]
    [CommandHelper(1, "<#userid or name>")]
    [RequiresPermissions("@css/ban")]
    public void OnDisarmCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (!GetTarget(info, out var player)) 
            return;
        
        player!.RemoveWeapons();
        
        if (!Config.HideActivity)
            Server.PrintToChatAll(FormatAdminMessage($"{caller!.PlayerName} disarmed {player.PlayerName}."));
    }
    
    [ConsoleCommand("css_hp", "Change a player's HP.")]
    [CommandHelper(2, "<#userid or name> <health>")]
    [RequiresPermissions("@css/slay")]
    public void OnHealthCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (!GetTarget(info, out var player) || !int.TryParse(info.GetArg(2), out var health))
        {
            info.ReplyToCommand(FormatMessage($"Target {info.GetArg(1)} not found or is not buried."));
            return;
        }

        player!.Pawn.Value.Health = health;
        
        if (!Config.HideActivity)
            Server.PrintToChatAll(FormatAdminMessage($"{caller!.PlayerName} changed {player.PlayerName}'s health to {health}."));
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
    }
    
    // [ConsoleCommand("css_vote", "Respawn a dead player.")]
    // [CommandHelper(1, "<#userid or name>")]
    // // [RequiresPermissions("@css/vote")]
    // public void OnVoteCommand(CCSPlayerController? caller, CommandInfo info)
    // {
    //     if (!ServerUtils.GetTarget(info.GetArg(1), out var player))
    //     {
    //         info.ReplyToCommand(FormatMessage($"Target {info.GetArg(1)} not found."));
    //         return;
    //     }
    //     
    //     player!.PlayerPawn.Value.LifeState = (int) LifeState_t.LIFE_ALIVE;;
    // }

    private static bool GetTarget(CommandInfo info, out CCSPlayerController? player)
    {
        var matches = ServerUtils.GetTarget(info.GetArg(1), out player);

        switch (matches)
        {
            case TargetResult.None:
                info.ReplyToCommand(FormatMessage($"Target {info.GetArg(1)} not found."));
                return false;
            case TargetResult.Multiple:
                info.ReplyToCommand(FormatMessage($"Multiple targets found for \"{info.GetArg(1)}\"."));
                return false;
        }

        return true;
    }
    
    private static string FormatMessage(string message) => $" {ChatColors.Lime}[BasicAdmin]{ChatColors.Default} {message}";
    private string FormatAdminMessage(string message) => $" {Config.AdminTag} {message}";
}