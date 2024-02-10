using System.Data;
using CounterStrikeSharp.API;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace BasicAdmin.Utils;

internal sealed class Database
{
    private MySqlConnection _conn { get; }
    private BasicAdmin _context { get; }
    private string _prefix { get; }
    
    public Database(BasicAdmin context)
    {
        _conn = new MySqlConnection(context.Config.Database.GetDslString());
        _context = context;
        _prefix = context.Config.Database.TablePrefix;
    }
    
    public async Task Load()
    {
        if (_conn.State == ConnectionState.Open)
            return;
        
        await _conn.OpenAsync();

        if (_conn.State != ConnectionState.Open)
        {
            _context.Logger.LogError("Could not connect to database.");
            Server.NextFrame(() => Server.PrintToConsole(BasicAdmin.FormatMessage("Could not connect to database.")));
        }
        
        CreateTablesIfNotExists();
    }
    
    public async Task Unload()
    {
        if (_conn.State == ConnectionState.Closed)
            return;
        
        await _conn.CloseAsync();
        
        if (_conn.State != ConnectionState.Closed)
        {
            _context.Logger.LogError("Error closing database connection.");
            Server.NextFrame(() => Server.PrintToConsole(BasicAdmin.FormatMessage("Error closing database connection.")));
        }
    }
    
    public MySqlConnection GetConnection()
    {
        return _conn;
    }
    
    private async void CreateTablesIfNotExists()
    {
        await using var command = new MySqlCommand(
            $"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = @tableName", _conn);
        command.Parameters.AddWithValue("@tableName", $"{_prefix}punishments");

        if (Convert.ToInt32(command.ExecuteScalar()) > 0)
            return;
        
        var filePath = Path.Combine(_context.ModuleDirectory, "schema.sql");
        var sql = (await File.ReadAllTextAsync(filePath)).Replace("{prefix}", _prefix);
        await using var cmd = new MySqlCommand(sql, _conn);
        await cmd.ExecuteNonQueryAsync();
    }
}
