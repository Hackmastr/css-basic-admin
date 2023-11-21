using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Utils;

namespace BasicAdmin;


internal static class ServerUtils
{
    private static readonly Dictionary<TargetFilter, Predicate<CCSPlayerController>> FilterPredicates = new()
    {
        { TargetFilter.CounterTerrorist, player => player.TeamNum == (int)CsTeam.CounterTerrorist },
        { TargetFilter.Terrorist, player => player.TeamNum == (int)CsTeam.Terrorist },
        { TargetFilter.Alive, player => player.TeamNum == (int)LifeState_t.LIFE_ALIVE },
        { TargetFilter.Admin, player => AdminManager.PlayerHasPermissions(player, "@css/chat") },
        { TargetFilter.Dead, player => player.LifeState == (int)LifeState_t.LIFE_DEAD },
        { TargetFilter.Spec, player => player.TeamNum == (int)CsTeam.Spectator },
    };
    
    private static readonly Dictionary<int, TargetFilter> TeamTargetFilter = new()
    {
        {(int) CsTeam.CounterTerrorist, TargetFilter.CounterTerrorist},
        {(int) CsTeam.Terrorist, TargetFilter.Terrorist},
        {(int) CsTeam.Spectator, TargetFilter.Spec},
    };
    
    public static List<CCSPlayerController> GetPlayerFromName(string name)
    {
        return Utilities.GetPlayers().FindAll(x => x.PlayerName.Contains(name, StringComparison.OrdinalIgnoreCase));
    }
    
    public static TargetResult GetTarget(string target, out CCSPlayerController? player)
    {
        player = null;
        
        if (target.StartsWith("#") && int.TryParse(target.AsSpan(1), out var userid))
        {
            player = Utilities.GetPlayerFromUserid(userid);
        }
        else
        {
            var matches = GetPlayerFromName(target);
            if (matches.Count > 1)
                return TargetResult.Multiple;
            
            player = matches.FirstOrDefault();
        }

        return player?.IsValid == true ? TargetResult.Single : TargetResult.None;
    }
    
    public static void KickPlayer(int? userId, string? reason = null)
    {
        Server.ExecuteCommand($"kickid {userId} {reason}");
    }

    public static void PrintToCenterAll(string message)
    {
        Utilities.GetPlayers().ForEach(controller =>
        {
            controller.PrintToCenter(message);
        });
    }

    public static void PrintToChatTeam(int team, string message)
    {
        Utilities.GetPlayers().FindAll(FilterPredicates[TeamTargetFilter[team]]).ForEach(controller =>
        {
            controller.PrintToChat(message);
        });
    }

    public static void PrintToChatTeam(TargetFilter filter, string message)
    {
        Utilities.GetPlayers().FindAll(FilterPredicates[filter]).ForEach(controller =>
        {
            controller.PrintToChat(message);
        });
    }
}
