namespace BimGuard.Infrastructure;

/// <summary>Пользовательские пути данных; не требуют прав администратора.</summary>
public static class AppPaths
{
    /// <summary>Общая история CLI, демоприложения и плагина для текущего пользователя.</summary>
    public static string Database => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BimGuard", "history.db");
}
