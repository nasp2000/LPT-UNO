using System;

namespace LPTUnoApp
{
    public interface IConnectionSource : IDisposable
    {
        event Action<string>? DataReceived;
        event Action<bool, string?>? ConnectionChanged;
        
        bool IsOpen { get; }
        string? ConnectionInfo { get; }
        
        void Open(string connectionString); // Use connection string (COM3 or 192.168.1.10:2323)
        void Close();
        Task WriteAsync(string data);
    }
}
