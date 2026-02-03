using System;
using System.IO;
using System.Text;
using System.Timers;

namespace LPTUnoApp
{
    public class DataProcessor
    {
        private readonly StringBuilder _buffer = new StringBuilder();
        private readonly System.Timers.Timer _timer;
        private readonly string _dataFolder;
        private readonly object _lock = new object();
        public event EventHandler<string>? FileSaved;
        public event EventHandler<string>? LogMessage;

        public DataProcessor(string dataFolder, int timeoutSeconds)
        {
            _dataFolder = dataFolder;
            _timer = new System.Timers.Timer(timeoutSeconds * 1000);
            _timer.AutoReset = false;
            _timer.Elapsed += OnTimerElapsed;
        }

        public void UpdateTimeout(int seconds)
        {
            _timer.Interval = seconds * 1000;
        }

        public void AddData(string data)
        {
            lock (_lock)
            {
                _buffer.Append(data);
                _timer.Stop();
                _timer.Start();
            }
        }

        private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            string content;
            lock (_lock)
            {
                content = _buffer.ToString();
                if (string.IsNullOrEmpty(content)) return;
                _buffer.Clear();
            }

            ProcessData(content);
        }

        private void ProcessData(string content)
        {
            try
            {
                Directory.CreateDirectory(_dataFolder);
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                // Base filename, might be decorated later if we had logic for it, 
                // but for now we just save it as lpt-uno_timestamp.txt like the original
                // Wait, original JS updated filename if recipient matched.
                // We will save a "clean" copy here first.
                
                string fileName = Path.Combine(_dataFolder, $"lpt-uno_{timestamp}.txt");
                File.WriteAllText(fileName, content);

                LogMessage?.Invoke(this, $"Data saved to {fileName} ({content.Length} bytes)");
                
                // Fire event so MainWindow can handle Email/Print
                FileSaved?.Invoke(this, fileName);
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"Error processing data: {ex.Message}");
            }
        }
    }
}
