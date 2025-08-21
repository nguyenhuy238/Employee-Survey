namespace Employee_Survey.Infrastructure
{
    public interface IRepository<T>
    {
        Task<List<T>> GetAllAsync();
        Task<T?> FirstOrDefaultAsync(Func<T, bool> predicate);
        Task InsertAsync(T entity);
        Task UpsertAsync(Func<T, bool> predicate, T entity);
        Task DeleteAsync(Func<T, bool> predicate);
    }
}
