using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace BasicAdmin;


internal static class ServerUtils
{
    public static CCSPlayerController? GetPlayerFromName(string name)
    {
        return Utilities.GetPlayers().FirstOrDefault(x => x.PlayerName.Equals(name));
    }
    
    public static bool GetTarget(string target, out CCSPlayerController? player)
    {
        player = null;
        
        if (target.StartsWith("#") && int.TryParse(target.AsSpan(1), out var userid))
        {
            player = Utilities.GetPlayerFromUserid(userid);
        }
        else
        {
            player = GetPlayerFromName(target);
        }

        return player?.IsValid == true;
    }
    
    public static void KickPlayer(string playerName, string? reason = null)
    {
        Server.ExecuteCommand($"kick {playerName} {reason}");
    }

    public static void PrintToCenterAll(string message)
    {
        Utilities.GetPlayers().ForEach(controller =>
        {
            controller.PrintToCenter(message);
        });
    }
}
