namespace Employee_Survey.Application;
public interface IAuditService
{
    Task LogAsync(string actor, string action, string entityId, object? before, object? after);
}