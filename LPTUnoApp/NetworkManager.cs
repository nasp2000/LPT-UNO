using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPTUnoApp
{
    public class NetworkManager : IConnectionSource
    {
        private TcpClient? _client;
        private NetworkStream? _stream;
        private CancellationTokenSource? _cts;
        private Task? _readTask;

        public event Action<string>? DataReceived;
        public event Action<bool, string?>? ConnectionChanged;

        public bool IsOpen => _client?.Connected ?? false;
        public string? ConnectionInfo { get; private set; }

        public void Open(string connectionString)
        {
            // Accepts: IP, IP:PORT, [IPv6]:PORT, IPv6%scope:PORT
            const int defaultPort = 2323;
            string ipPart = connectionString;
            string portPart = null;

            if (connectionString.StartsWith("["))
            {
                // Bracketed IPv6: [address]:port or just [address]
                int end = connectionString.IndexOf(']');
                if (end == -1) throw new ArgumentException("Invalid bracketed IPv6 address");
                ipPart = connectionString.Substring(1, end - 1);
                if (connectionString.Length > end + 1 && connectionString[end + 1] == ':')
                    portPart = connectionString.Substring(end + 2);
            }
            else
            {
                int lastColon = connectionString.LastIndexOf(':');
                if (lastColon > 0)
                {
                    // Split on last colon so IPv6 addresses without brackets still work
                    ipPart = connectionString.Substring(0, lastColon);
                    portPart = connectionString.Substring(lastColon + 1);
                }
            }

            int port = defaultPort;
            if (!string.IsNullOrEmpty(portPart)) port = int.Parse(portPart);

            Close();

            try
            {
                _client = new TcpClient();
                // Try to parse IP to handle IPv6 scope ids properly
                try
                {
                    var ipAddr = System.Net.IPAddress.Parse(ipPart);
                    _client.Connect(ipAddr, port);
                }
                catch
                {
                    // Fallback to DNS/host connect
                    _client.Connect(ipPart, port);
                }

                _stream = _client.GetStream();
                ConnectionInfo = connectionString;

                _cts = new CancellationTokenSource();
                _readTask = Task.Run(() => ReadLoop(_cts.Token));
                
                ConnectionChanged?.Invoke(true, connectionString);
            }
            catch (Exception)
            {
                Close();
                throw;
            }
        }

        public void Close()
        {
            _cts?.Cancel();
            try { _client?.Close(); } catch { }
            
            _client = null;
            _stream = null;
            
            if (ConnectionInfo != null)
            {
                ConnectionChanged?.Invoke(false, ConnectionInfo);
                ConnectionInfo = null;
            }
            
             // _readTask?.Wait(); // Avoid deadlocking UI thread if called from UI
             _readTask = null;
             _cts?.Dispose();
             _cts = null;
        }

        public async Task WriteAsync(string data)
        {
            if (_stream != null && _client != null && _client.Connected)
            {
                byte[] bytes = Encoding.ASCII.GetBytes(data);
                await _stream.WriteAsync(bytes, 0, bytes.Length);
            }
        }

        private async Task ReadLoop(CancellationToken ct)
        {
            byte[] buffer = new byte[1024];
            try
            {
                while (!ct.IsCancellationRequested && _stream != null && _client != null && _client.Connected)
                {
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, ct);
                    if (bytesRead == 0) break; // Disconnected

                    string text = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    DataReceived?.Invoke(text);
                }
            }
            catch (Exception)
            {
                // Disconnected or cancelled
            }
            finally
            {
                // Ensure closed if loop exits unexpectedly
                if (IsOpen) 
                {
                    // If we are here, it means we lost connection
                    // Dispatch invoke required? No, events are usually handled on UI thread by logic
                     _client?.Close();
                     // We might want to notify disconnection, but careful about threads
                }
            }
        }

        public void Dispose()
        {
            Close();
        }
    }
}
