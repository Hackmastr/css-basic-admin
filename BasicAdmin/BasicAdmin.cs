using BasicAdmin.Backends;
using BasicAdmin.Config;
using BasicAdmin.Ents;
using BasicAdmin.Managers;
using BasicAdmin.Utils;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CSSTargetResult = CounterStrikeSharp.API.Modules.Commands.Targeting.TargetResult;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Timers;
using DatabaseAdmins.Backends;
using Microsoft.Extensions.Logging;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace BasicAdmin;

[MinimumApiVersion(126)]
public sealed partial class BasicAdmin : BasePlugin, IPluginConfig<BasicAdminConfig>
{
    public override string ModuleName => "BasicAdmin";
    public override string ModuleAuthor => "livevilog";
    public override string ModuleVersion => "1.7.0";

    public BasicAdminConfig Config {get; set;} = new ();
    public static BasicAdmin? Instance { get; private set; }

    private static readonly Dictionary<IntPtr, bool> ActiveGodMode = new ();
    private static readonly Dictionary<IntPtr, HashSet<ActivePunishment>> ActivePunishments = new ();
    private static readonly HashSet<int> MutedPlayers = new ();

    internal Database Database = null!;
    private Punishments _punishmentMgr = null!;
    // private Admins _adminMgr;
    
    private Timer _punishmentTimer = null!;

    private IBackend? _backend;
    
    public BasicAdmin()
    {
        Instance = this;
    }
    
    public void OnConfigParsed(BasicAdminConfig config)
    {
        this.Config = config;
        
        _backend = config.Backend switch
        {
            BackendType.MySql => new MySqlBackend(),
            BackendType.Sbpp => new SbppBackend(ConVar.Find("hostport")!.GetPrimitiveValue<int>()),
            _ => null
        };
    }
    
    public override void Load(bool hotReload)
    {
        Task.Run(Init);
        
        AddCommandListener("say", OnSayCommand);
        AddCommandListener("say_team", OnSayCommand);
        
        VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Hook(OnTakeDamage, HookMode.Pre);
        
        RegisterListener<Listeners.OnClientAuthorized>(OnClientAuthorized);
        RegisterListener<Listeners.OnClientVoice>(OnClientVoice);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
    }

    public override void Unload(bool hotReload)
    {
        VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Unhook(OnTakeDamage, HookMode.Pre);
        _punishmentTimer.Kill();
        
        Task.Run(async () =>
        {
             await Database.Unload();
        });
    }

    private async void Init()
    {
        try
        {
            Database = new Database(this);
            await Database.Load();
            _punishmentMgr = new Punishments(this);
            await _backend!.Load();
            
            Server.NextFrame(() =>
            {
                _punishmentTimer = AddTimer(60f, PunishmentTimer, TimerFlags.REPEAT);
            });
        } catch (Exception e)
        {
            Logger.LogError(e, "Error loading database. " + Config.Database.GetDslString());
            Server.NextFrame(() => Server.PrintToConsole(FormatMessage("Error loading database.")));
        }
    }

    [GameEventHandler]
    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        ActiveGodMode.Clear();
        
        return HookResult.Continue;
    }
    
    private void OnClientAuthorized(int slot, SteamID id)
    {
        if (!id.IsValid()) 
            return;
        
        Task.Run(async () =>
        {
            // await _adminMgr.LoadAdmin(id);
            if (!await KickPlayerIfBanned(slot, id))
                await FetchActivePunishments(slot, id);
        });
    }
    
    private void  OnClientVoice(int slot)
    {
        if (MutedPlayers.Contains(slot))
            Utilities.GetPlayerFromSlot(slot).VoiceFlags = VoiceFlags.Muted;
    }
    
    private HookResult OnPlayerDisconnect(EventPlayerDisconnect ev, GameEventInfo info)
    {
        var sid = ev.Userid.Handle;
        
        ActivePunishments.Remove(sid);

        return HookResult.Continue;
    }

    private CSSTargetResult? GetTarget(CommandInfo info, bool allowMultiple = true, bool noReply = false)
    {
        var matches = info.GetArgTargetResult(1);

        if (!matches.Any()) {
            if (!noReply)
                info.ReplyToCommand(FormatMessage(Localizer["ba.target.not_found", info.GetArg(1)]));
            return null;
        }

        if (!(matches.Count() > 1) || (info.GetArg(1).StartsWith('@') && allowMultiple)) 
            return matches;
        
        if (!noReply)
            info.ReplyToCommand(FormatMessage(Localizer["ba.target.multiple", info.GetArg(1)]));
        
        return null;
    }
    
    internal static string FormatMessage(string message) => $" {ChatColors.Lime}[BasicAdmin]{ChatColors.Default} {message}";
    private string FormatAdminMessage(string message) => $" {Config.AdminTag} {message}";
}