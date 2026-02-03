using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPTUnoApp
{
    public class SerialManager : IConnectionSource
    {
        private SerialPort? _port;
        public event Action<string>? DataReceived;
        public event Action<bool, string?>? ConnectionChanged;

        public bool IsOpen => _port?.IsOpen ?? false;
        public string? ConnectionInfo => _port?.PortName;
        public string? CurrentPortName => _port?.PortName;

        public string[] GetPorts() => SerialPort.GetPortNames();

        // Interface implementation for Open(string)
        public void Open(string connectionString)
        {
            Open(connectionString, 115200);
        }

        public void Open(string portName, int baudRate = 115200)
        {
            Close();
            _port = new SerialPort(portName, baudRate)
            {
                ReadTimeout = 500,
                WriteTimeout = 500,
                NewLine = "\n",
                Encoding = Encoding.ASCII
            };
            _port.DataReceived += OnDataReceived;
            _port.Open();
            ConnectionChanged?.Invoke(true, portName);
        }

        public void Close()
        {
            if (_port != null)
            {
                try
                {
                    _port.DataReceived -= OnDataReceived;
                    if (_port.IsOpen) _port.Close();
                }
                catch { }
                var name = _port.PortName;
                _port.Dispose();
                _port = null;
                ConnectionChanged?.Invoke(false, name);
            }
        }

        public Task WriteAsync(string data)
        {
             if (IsOpen && _port != null)
             {
                 _port.Write(data);
                 return Task.CompletedTask;
             }
             return Task.FromException(new InvalidOperationException("Serial port not open"));
        }

        private void OnDataReceived(object? sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (_port != null && _port.IsOpen)
                {
                    var text = _port.ReadExisting();
                    DataReceived?.Invoke(text);
                }
            }
            catch (Exception ex)
            {
                DataReceived?.Invoke($"[serial error] {ex.Message}");
            }
        }

        /// <summary>
        /// Probes available ports by sending a probe command (default: 'V') and waiting for a response containing 'Firmware' or the expected substring.
        /// If a match is found, the port is left open and becomes the current connection.
        /// </summary>
        public async Task<bool> ProbeAndOpenAsync(string expectedResponseSubstring = "Firmware", string probeCommand = "V", int probeTimeoutMs = 1000)
        {
            var ports = GetPorts();
            foreach (var port in ports)
            {
                try
                {
                    using var temp = new SerialPort(port, 115200) { ReadTimeout = 200, WriteTimeout = 200, NewLine = "\n", Encoding = Encoding.ASCII };
                    var buffer = new StringBuilder();
                    var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                    SerialDataReceivedEventHandler handler = (s, e) =>
                    {
                        try
                        {
                            var sp = s as SerialPort;
                            if (sp != null && sp.IsOpen)
                            {
                                var txt = sp.ReadExisting();
                                buffer.Append(txt);
                                if (buffer.ToString().IndexOf(expectedResponseSubstring, StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    tcs.TrySetResult(true);
                                }
                            }
                        }
                        catch { }
                    };

                    temp.DataReceived += handler;
                    temp.Open();

                    // Send probe
                    try { temp.WriteLine(probeCommand); } catch { }

                    using var cts = new CancellationTokenSource(probeTimeoutMs);
                    using (cts.Token.Register(() => tcs.TrySetResult(false)))
                    {
                        var ok = await tcs.Task.ConfigureAwait(false);
                        temp.DataReceived -= handler;
                        if (ok)
                        {
                            // success: make this the main port
                            Open(port);
                            DataReceived?.Invoke($"[auto-connect] Connected to {port}");
                            return true;
                        }
                    }

                    temp.DataReceived -= handler;
                    if (temp.IsOpen) temp.Close();
                }
                catch { }
            }
            return false;
        }

        public void Dispose() => Close();
    }
}