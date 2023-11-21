using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace BasicAdmin;


internal static class ServerUtils
{
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
}

internal enum TargetResult
{
    None,
    Multiple,
    Single
}