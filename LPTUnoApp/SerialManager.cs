using System;
using System.IO.Ports;

namespace LPTUnoApp
{
    public class SerialManager : IDisposable
    {
        private SerialPort? _port;
        public event Action<string>? DataReceived;

        public bool IsOpen => _port?.IsOpen ?? false;
        public string? CurrentPortName => _port?.PortName;

        public string[] GetPorts() => SerialPort.GetPortNames();

        public void Open(string portName, int baudRate = 115200)
        {
            Close();
            _port = new SerialPort(portName, baudRate)
            {
                ReadTimeout = 500,
                WriteTimeout = 500,
                NewLine = "\n"
            };
            _port.DataReceived += OnDataReceived;
            _port.Open();
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
                _port.Dispose();
                _port = null;
            }
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

        public void Dispose() => Close();
    }
}