using System.Diagnostics.CodeAnalysis;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Utils;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Memory;

namespace BasicAdmin;

internal struct OriginalVec
{
    internal Vector Position;
    internal QAngle Rotation;
    internal Vector Velocity;
}

public class BasicAdmin : BasePlugin, IPluginConfig<BasicAdminConfig>
{
    public override string ModuleName => "BasicAdmin";
    public override string ModuleAuthor => "livevilog";
    public override string ModuleVersion => "0.1.0";
    
    public BasicAdminConfig Config {get; set;} = new ();
    
    private static readonly Dictionary<CCSPlayerController, OriginalVec> OriginalPositions = new ();   
    

    public void OnConfigParsed(BasicAdminConfig config)
    {
        this.Config = config;
    }

    public override void Load(bool hotReload)
    { }
    
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
        
        Server.PrintToChatAll(FormatMessage($"Changing map to {map}."));
        
        AddTimer(5f, () =>
        {
            Server.ExecuteCommand($"changelevel {map}");
        });
    }
    
    [ConsoleCommand("css_kick", "Kicks a player from the server.")]
    [CommandHelper(1, "<#userid or name> [reason]")]
    [RequiresPermissions("@css/kick")]
    public void OnKickCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (!ServerUtils.GetTarget(info.GetArg(1), out var player))
        {
            info.ReplyToCommand(FormatMessage($"Target {info.GetArg(1)} not found."));
            return;
        }
        
        var reason = info.GetArg(2);
        
        ServerUtils.KickPlayer(player!.PlayerName, reason);
    }
    
    [ConsoleCommand("css_slay", "Slay a player.")]
    [CommandHelper(1, "<#userid or name>")]
    [RequiresPermissions("@css/slay")]
    public void OnSlayCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (!ServerUtils.GetTarget(info.GetArg(1), out var player))
        {
            info.ReplyToCommand(FormatMessage($"Target {info.GetArg(1)} not found."));
            return;
        }
        
        player!.Pawn.Value.CommitSuicide(false, true);
        if (!Config.HideActivity)
            Server.PrintToChatAll($"{caller!.PlayerName} slayed {player!.PlayerName}.");
    }
    
    [ConsoleCommand("css_give", "Give a player an item.")]
    [CommandHelper(2, "<#userid or name> <item name>")]
    [RequiresPermissions("@css/kick")]
    public void OnGiveCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (!ServerUtils.GetTarget(info.GetArg(1), out var player))
        {
            info.ReplyToCommand(FormatMessage($"Target {info.GetArg(1)} not found."));
            return;
        }
        
        player!.GiveNamedItem(info.GetArg(2));
        
        if (!Config.HideActivity)
            Server.PrintToChatAll($"{caller!.PlayerName} gave {player!.PlayerName} {info.GetArg(2)}.");
    }
    
    [ConsoleCommand("css_swap", "Swap a player.")]
    [CommandHelper(1, "<#userid or name>")]
    [RequiresPermissions("@css/kick")]
    public void OnSwapCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (!ServerUtils.GetTarget(info.GetArg(1), out var player))
        {
            info.ReplyToCommand(FormatMessage($"Target {info.GetArg(1)} not found."));
            return;
        }
        
        if ((int) CsTeam.Spectator == player!.TeamNum)
        {
            info.ReplyToCommand(FormatMessage($"Target {info.GetArg(1)} is a spectator."));
            return;
        }
     
        var isCs = player!.TeamNum == (int) CsTeam.CounterTerrorist;
        
        player!.ChangeTeam(isCs ? CsTeam.Terrorist : CsTeam.CounterTerrorist);
        
        if (!Config.HideActivity)
            Server.PrintToChatAll($"{caller!.PlayerName} swapped {player!.PlayerName}.");
    }
    
    [ConsoleCommand("css_spec", "Change a player to spec.")]
    [CommandHelper(1, "<#userid or name>")]
    [RequiresPermissions("@css/kick")]
    public void OnSpecCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (!ServerUtils.GetTarget(info.GetArg(1), out var player))
        {
            info.ReplyToCommand(FormatMessage($"Target {info.GetArg(1)} not found."));
            return;
        }
        
        player!.ChangeTeam(CsTeam.Spectator);
        
        if (!Config.HideActivity)
            Server.PrintToChatAll($"{caller!.PlayerName} swapped {player!.PlayerName} to spec.");
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
    //     info.ReplyToCommand(FormatMessage("Not implemented yet."));
    // }
    
    [ConsoleCommand("css_say", "Say to all players.")]
    [CommandHelper(1, "<message>")]
    [RequiresPermissions("@css/vote")]
    public void OnAdminSayCommand(CCSPlayerController? caller, CommandInfo info)
    {
        Server.PrintToChatAll(FormatAdminMessage(info.GetCommandString[info.GetCommandString.IndexOf(' ')..]));
    }
    
    [ConsoleCommand("css_psay", "Say to all players.")]
    [CommandHelper(2, "<#userid or name> <message>")]
    [RequiresPermissions("@css/vote")]
    public void OnAdminPrivateSayCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (!ServerUtils.GetTarget(info.GetArg(1), out var player))
        {
            info.ReplyToCommand(FormatMessage($"Target {info.GetArg(1)} not found."));
            return;
        }

        var range = info.GetArg(1).Length + info.GetArg(2).Length + 3;
        var message = info.GetCommandString[range..];
        
        info.ReplyToCommand(FormatAdminMessage($"({player!.PlayerName}) {message}"));
        player!.PrintToChat(FormatAdminMessage($"({caller!.PlayerName}) {message}"));
    }
    
    [ConsoleCommand("css_csay", "Say to all players (in center).")]
    [CommandHelper(1, "<message>")]
    [RequiresPermissions("@css/vote")]
    public void OnAdminCenterSayCommand(CCSPlayerController? caller, CommandInfo info)
    {
        ServerUtils.PrintToCenterAll(FormatAdminMessage(info.GetCommandString[info.GetCommandString.IndexOf(' ')..]));
    }
    
    [ConsoleCommand("css_hsay", "Say to all players (in hud).")]
    [CommandHelper(1, "<message>")]
    [RequiresPermissions("@css/vote")]
    public void OnAdminHudSayCommand(CCSPlayerController? caller, CommandInfo info)
    {
        VirtualFunctions.ClientPrintAll(
            HudDestination.Alert, 
            FormatAdminMessage(info.GetCommandString[info.GetCommandString.IndexOf(' ')..]), 
            0, 0, 0, 0);
    }
    
    [ConsoleCommand("css_extend", "Respawn a dead player.")]
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
    [RequiresPermissions("@css/changemap")]
    public void OnBuryCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (!ServerUtils.GetTarget(info.GetArg(1), out var player) || player!.Pawn.Value.LifeState != (int) LifeState_t.LIFE_ALIVE)
        {
            info.ReplyToCommand(FormatMessage($"Target {info.GetArg(1)} not found."));
            return;
        }

        if (!OriginalPositions.ContainsKey(player!))
            OriginalPositions[player!] = new OriginalVec()
            {
                Position = player!.AbsOrigin!,
                Rotation = player!.AbsRotation!,
                Velocity = player!.AbsVelocity
            };

        var newPos = new Vector(player!.Pawn.Value.AbsOrigin!.X, player!.Pawn.Value.AbsOrigin!.Y,
            player!.Pawn.Value.AbsOrigin!.Z - 10f);
        
        player!.Pawn.Value.Teleport(newPos, player!.AbsRotation!, player!.AbsVelocity);
        
        if (!Config.HideActivity)
            Server.PrintToChatAll(FormatAdminMessage($"{caller!.PlayerName} buried {player!.PlayerName}."));
    }
    
    [ConsoleCommand("css_unbury", "Bury a player.")]
    [CommandHelper(1, "<#userid or name>")]
    [RequiresPermissions("@css/changemap")]
    public void OnUnburyCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (!ServerUtils.GetTarget(info.GetArg(1), out var player) || !OriginalPositions.ContainsKey(player!))
        {
            info.ReplyToCommand(FormatMessage($"Target {info.GetArg(1)} not found or is not buried."));
            return;
        }

        player!.Pawn.Value.Teleport(OriginalPositions[player!].Position, OriginalPositions[player!].Rotation, OriginalPositions[player!].Velocity);
        
        if (!Config.HideActivity)
            Server.PrintToChatAll(FormatAdminMessage($"{caller!.PlayerName} unburied {player!.PlayerName}."));
        
        OriginalPositions.Remove(player!);
    }
    
    [ConsoleCommand("css_disarm", "Disarm a player.")]
    [CommandHelper(1, "<#userid or name>")]
    [RequiresPermissions("@css/changemap")]
    public void OnDisarmCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (!ServerUtils.GetTarget(info.GetArg(1), out var player) || !OriginalPositions.ContainsKey(player!))
        {
            info.ReplyToCommand(FormatMessage($"Target {info.GetArg(1)} not found or is not buried."));
            return;
        }

        foreach (var weapon in player!.Pawn.Value.WeaponServices.MyWeapons)
        {
            weapon.Value.Remove();
        }
        
        if (!Config.HideActivity)
            Server.PrintToChatAll(FormatAdminMessage($"{caller!.PlayerName} disarmed {player!.PlayerName}."));
    }
    
    [ConsoleCommand("css_hp", "Change a player's HP.")]
    [CommandHelper(2, "<#userid or name> <health>")]
    [RequiresPermissions("@css/slay")]
    public void OnHealthCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (!ServerUtils.GetTarget(info.GetArg(1), out var player) || int.TryParse(info.GetArg(2), out var health))
        {
            info.ReplyToCommand(FormatMessage($"Target {info.GetArg(1)} not found or is not buried."));
            return;
        }

        player!.Pawn.Value.Health = health;
        
        if (!Config.HideActivity)
            Server.PrintToChatAll(FormatAdminMessage($"{caller!.PlayerName} changed {player!.PlayerName}'s health to {health}."));
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
    
    private static string FormatMessage(string message) => $" {ChatColors.Lime}[BasicAdmin]{ChatColors.Default} {message}";
    private string FormatAdminMessage(string message) => $" {Config.AdminTag} {message}";
}