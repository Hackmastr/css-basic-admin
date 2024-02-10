namespace BasicAdmin.Backends;

public interface IBackend
{
    /// <summary>
    /// Load the backend.
    /// </summary>
    /// <returns>True if the backend was loaded successfully, false otherwise.</returns>
    Task<bool> Load();
}
