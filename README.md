<p style="text-align: center;font-size: 2em;font-weight: bold">
    An admin for <a href="https://docs.cssharp.dev" target="_blank">CSSharp</a>
</p>

## Requirements
* CSSharp >= v65

## Configuration
* **admin_tag**: Tag that's shown in the chat.
* **hide_activity**: Hide admin activity from chat.
* **admin_list_min_flag**: Required flag to be included in the list of admins.
* **admin_list_req_flag**: If not left empty, all players can see a lit of admins.
* **freeze_duration**: Default freeze duration when using the freeze command.

## Available commands
| Command                       | Flags Required                  |
|-------------------------------|---------------------------------|
| css_admins                    | no flags required (can be changed in config) |
| css_admin_help                | @css/generic                    |
| css_bury/css_unbury           | @css/ban                        |
| css_cvar                      | @css/cvar                       |
|                               | changing sv_cheats requires @css/cheats flag |
|                               |                                 |
| css_say                       |                                 |
| @ (alias)                     |                                 |
| @ (in team chat, admins only) |                                 |
| css_csay                      |                                 |
| css_hsay                      |                                 |
| css_psay                      |                                 |
|                               | @css/chat                       |
|                               |                                 |
| css_disarm                    | @css/ban           |
| css_freeze/css_unfreeze       | @css/slay          |
| css_extend                    | @css/changemap     |
| css_forcespec                 | @css/kick          |
| css_give                      | @css/cvar          |
| css_kick                      | @css/kick          |
| css_noclip                    | @css/cheats        |
| css_map                       | @css/changemap     |
| css_restartgame               |                                 |
| css_rr (alias)                |                                 |
|                               | @css/changemap     |
|                               |                                 |
| css_hp                        |           |
| css_slay                      | @css/slay          |
| css_slap                      | @css/kick          |
| css_swap                      | @css/kick          |
| css_workshop                  |                                 |
| css_wsmap (alias)             | @css/changemap     |



## To-do
* Voting
* Respawn

