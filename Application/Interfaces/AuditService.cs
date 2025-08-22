using Employee_Survey.Application;
using System.Text.Json;

namespace Employee_Survey.Infrastructure;

public class AuditService : IAuditService
{
    private readonly string _path;
    private static readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _opt = new() { WriteIndented = true };

    public AuditService(IConfiguration cfg)
    {
        var folder = cfg["DataFolder"] ?? "App_Data";
        Directory.CreateDirectory(folder);
        _path = Path.Combine(folder, $"AuditLog.json");
        if (!File.Exists(_path)) File.WriteAllText(_path, "[]");
    }

    public async Task LogAsync(string actor, string action, string entityId, object? before, object? after)
    {
        await _lock.WaitAsync();
        try
        {
            var list = JsonSerializer.Deserialize<List<AuditEntry>>(await File.ReadAllTextAsync(_path)) ?? new();
            list.Add(new AuditEntry { At = DateTime.UtcNow, Actor = actor, Action = action, EntityId = entityId, Before = before, After = after });
            await File.WriteAllTextAsync(_path, JsonSerializer.Serialize(list, _opt));
        }
        finally { _lock.Release(); }
    }

    private class AuditEntry
    {
        public DateTime At { get; set; }
        public string Actor { get; set; } = "";
        public string Action { get; set; } = "";
        public string EntityId { get; set; } = "";
        public object? Before { get; set; }
        public object? After { get; set; }
    }
}