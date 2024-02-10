using System.Text.Json;
using BasicAdmin.Backends;
using BasicAdmin.Config;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Config;
using CounterStrikeSharp.API.Modules.Cvars;
using Microsoft.Extensions.Logging;

namespace BasicAdmin;

partial class BasicAdmin
{
    [ConsoleCommand("sm_rehash")]
    [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
    public void OnRehashCommand(CCSPlayerController caller, CommandInfo info)
    {
        if (_backend == null)
        {
            Server.PrintToConsole("[BasicAdmin] No backend loaded.");
            return;
        }
        
        Task.Run(async () =>
        {
            await _backend.Load();
            Server.NextFrame(() =>
            {
                Server.ExecuteCommand("css_groups_reload");
                Server.ExecuteCommand("css_admins_reload");
            });
        });
    }
}
