using System.Text.Json;

namespace Employee_Survey.Infrastructure
{
    public class JsonFileRepository<T> : IRepository<T>
    {
        private readonly string _path;
        private readonly JsonSerializerOptions _opt = new() { WriteIndented = true };
        private static readonly SemaphoreSlim _lock = new(1, 1);

        public JsonFileRepository(IConfiguration cfg)
        {
            var folder = cfg["DataFolder"] ?? "App_Data";
            Directory.CreateDirectory(folder);
            _path = Path.Combine(folder, $"{typeof(T).Name}.json");
            if (!File.Exists(_path)) File.WriteAllText(_path, "[]");
        }

        private async Task<List<T>> ReadAsync()
        {
            var json = await File.ReadAllTextAsync(_path);
            return JsonSerializer.Deserialize<List<T>>(json) ?? new();
        }
        private async Task WriteAsync(List<T> data)
        {
            var json = JsonSerializer.Serialize(data, _opt);
            await File.WriteAllTextAsync(_path, json);
        }

        public async Task<List<T>> GetAllAsync()
        {
            await _lock.WaitAsync();
            try { return await ReadAsync(); }
            finally { _lock.Release(); }
        }

        public async Task<T?> FirstOrDefaultAsync(Func<T, bool> predicate)
        {
            await _lock.WaitAsync();
            try { return (await ReadAsync()).FirstOrDefault(predicate); }
            finally { _lock.Release(); }
        }

        public async Task InsertAsync(T entity)
        {
            await _lock.WaitAsync();
            try { var list = await ReadAsync(); list.Add(entity); await WriteAsync(list); }
            finally { _lock.Release(); }
        }

        public async Task UpsertAsync(Func<T, bool> predicate, T entity)
        {
            await _lock.WaitAsync();
            try
            {
                var list = await ReadAsync();
                var i = list.FindIndex(x => predicate(x));
                if (i >= 0) list[i] = entity; else list.Add(entity);
                await WriteAsync(list);
            }
            finally { _lock.Release(); }
        }

        public async Task DeleteAsync(Func<T, bool> predicate)
        {
            await _lock.WaitAsync();
            try
            {
                var list = await ReadAsync();
                list.RemoveAll(x => predicate(x));
                await WriteAsync(list);
            }
            finally { _lock.Release(); }
        }
    }
}
