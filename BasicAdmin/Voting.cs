using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Plugin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace BasicAdmin;

public sealed class VoteResult
{
    private readonly Dictionary<int, int> _votes = new();
    
    public int this[int index]
    {
        get => _votes[index];
        set => _votes[index] = value;
    }

    public void Init(Dictionary<int, string> options)
    {
        foreach (var option in options)
        {
            _votes[option.Key] = 0;
        }
    }
    
    public int WinnerOption()
    {
        var max = 0;
        var winner = 0;
        
        foreach (var (key, value) in _votes)
        {
            if (value > max)
            {
                max = value;
                winner = key;
            }
        }

        return winner;
    }
}
    
public sealed class Vote
{
    private ChatMenu _menu;
    private Timer _timer;
    private BasePlugin _context;
    private VoteResult _result = new();
    private Dictionary<int, string> _options = new();
    private Delegate _callback;
    
    public Vote(BasePlugin context, CommandInfo info)
    {
        _context = context;
        _menu = new ChatMenu(info.GetArg(1));
        ChatMenuOption option;

        var argsIndex = 2;

        if (string.IsNullOrEmpty(info.GetArg(argsIndex)))
        {
            option = _menu.AddMenuOption("Yes", HandleCustomVote);
            _options.Add(option.GetHashCode(), option.Text);
            
            option = _menu.AddMenuOption("No", HandleCustomVote);
            _options.Add(option.GetHashCode(), option.Text);
            
            argsIndex = 4;
        }
        
        for (var i = argsIndex; i < info.ArgCount; i++)
        {
            option = _menu.AddMenuOption(info.GetArg(i), HandleCustomVote);
            _options.Add(option.GetHashCode(), option.Text);
        }
    }
    
    public void Start()
    {
        _context.AddTimer(15, VoteEnd);
        ServerUtils.OpenMenuAll(_menu);
    }
    
    private void VoteEnd()
    {
        _callback.DynamicInvoke(_result);
    }
    
    public void OnEnd(Delegate callback)
    {
        _callback = callback;
    }

    private void HandleCustomVote(CCSPlayerController player, ChatMenuOption option)
    {
        
        Server.PrintToChatAll($" {player.PlayerName} voted {option.Text}.");
    }
}

