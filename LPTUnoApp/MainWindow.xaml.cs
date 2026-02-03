using System;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;
using Newtonsoft.Json;

namespace LPTUnoApp
{
    public partial class MainWindow : Window
    {
        private NotifyIcon _notifyIcon;
        private bool _autoPrintEnabled = false;
        private readonly string _configPath;
        private readonly SerialManager _serialManager = new SerialManager();
        private PrintManager? _printManager;
        private MoveManager? _moveManager;
        private bool _moveMonitorRunning = false;
        private System.Timers.Timer? _reconnectTimer;
        private readonly string _dataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LPT-UNO", "DATA");
        private readonly string _downloadsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        private readonly string _autoprintFlagPath;

        public MainWindow()
        {
            InitializeComponent();
            _configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LPT-UNO", "config.json");
            InitializeTray();
            _serialManager.DataReceived += OnSerialDataReceived;
            _autoprintFlagPath = Path.Combine(Path.GetDirectoryName(_configPath) ?? _dataFolder, ".autoprint_enabled");
            RefreshPorts();
            LoadConfig();
            InitializePrintManager();
            InitializeReconnectTimer();
            StatusText.Text = "Pronto";
        }

        private void InitializePrintManager()
        {
            _printManager = new PrintManager(_dataFolder);
            RefreshPrinters();
        }

        private void InitializeMoveManager()
        {
            if (_moveManager == null)
            {
                _moveManager = new MoveManager(_downloadsFolder, _dataFolder);
                _moveManager.FileMoved += (f) => Dispatcher.Invoke(() => { AppendLog($"Moved file to DATA: {f}"); });
                _moveManager.Error += (e) => Dispatcher.Invoke(() => { AppendLog($"MoveManager error: {e}"); });
            }
        }

        private void StartMoveMonitor()
        {
            InitializeMoveManager();
            if (_moveManager != null && !_moveMonitorRunning)
            {
                _moveManager.Start();
                _moveMonitorRunning = true;
                BtnToggleMoveMonitor.Content = "Parar Monitor Downloads";
                AppendLog("Move monitor started");
            }
        }

        private void StopMoveMonitor()
        {
            if (_moveManager != null && _moveMonitorRunning)
            {
                _moveManager.Stop();
                _moveMonitorRunning = false;
                BtnToggleMoveMonitor.Content = "Iniciar Monitor Downloads";
                AppendLog("Move monitor stopped");
            }
        }

        private void InitializeReconnectTimer()
        {
            _reconnectTimer = new System.Timers.Timer(5000);
            _reconnectTimer.Elapsed += (s, e) =>
            {
                if (!_serialManager.IsOpen)
                {
                    var cfgPort = GetSavedSerialPort();
                    if (!string.IsNullOrEmpty(cfgPort))
                    {
                        var available = _serialManager.GetPorts();
                        foreach (var p in available)
                        {
                            if (string.Equals(p, cfgPort, StringComparison.OrdinalIgnoreCase))
                            {
                                try
                                {
                                    _serialManager.Open(p);
                                    Dispatcher.Invoke(() =>
                                    {
                                        BtnConnectPort.Content = "Desconectar";
                                        AppendLog($"Reconnected serial to {p}");
                                    });
                                    break;
                                }
                                catch { }
                            }
                        }
                    }
                }
            };
            _reconnectTimer.Start();
        }

        private void InitializeTray()
        {
            _notifyIcon = new NotifyIcon();
            try
            {
                _notifyIcon.Icon = new System.Drawing.Icon("TrayIcon.ico");
            }
            catch { }
            _notifyIcon.Visible = true;
            _notifyIcon.Text = "LPT-UNO";

            var contextMenu = new System.Windows.Forms.ContextMenuStrip();
            var openItem = new ToolStripMenuItem("Abrir");
            openItem.Click += (s, e) => Dispatcher.Invoke(ShowMainWindow);
            contextMenu.Items.Add(openItem);

            var toggleAutoPrintItem = new ToolStripMenuItem("Ativar AutoPrint");
            toggleAutoPrintItem.Click += (s, e) => Dispatcher.Invoke(() => ToggleAutoPrint(!_autoPrintEnabled));
            contextMenu.Items.Add(toggleAutoPrintItem);

            var toggleMoveMonitorItem = new ToolStripMenuItem("Iniciar Monitor Downloads");
            toggleMoveMonitorItem.Click += (s, e) => Dispatcher.Invoke(() => { if (_moveMonitorRunning) StopMoveMonitor(); else StartMoveMonitor(); });
            contextMenu.Items.Add(toggleMoveMonitorItem);

            var exitItem = new ToolStripMenuItem("Sair");
            exitItem.Click += (s, e) => Dispatcher.Invoke(ExitApp);
            contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.DoubleClick += (s, e) => Dispatcher.Invoke(ShowMainWindow);

            this.StateChanged += (s, e) =>
            {
                if (this.WindowState == WindowState.Minimized)
                    this.Hide();
            };
            this.Closing += (s, e) =>
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _serialManager.Dispose();
            };
        }

        private void ShowMainWindow()
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }

        private void ExitApp()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _serialManager.Dispose();
            Application.Current.Shutdown();
        }

        private void BtnOpenDataFolder_Click(object sender, RoutedEventArgs e)
        {
            var dataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LPT-UNO", "DATA");
            Directory.CreateDirectory(dataFolder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo() { FileName = dataFolder, UseShellExecute = true });
            AppendLog($"Opened DATA folder: {dataFolder}");
        }

        private void BtnToggleAutoPrint_Click(object sender, RoutedEventArgs e)
        {
            ToggleAutoPrint(!_autoPrintEnabled);
        }

        private void BtnToggleAutoPrintFlag_Click(object sender, RoutedEventArgs e)
        {
            // toggles the presence of .autoprint_enabled file (compatibility with scripts)
            if (File.Exists(_autoprintFlagPath))
            {
                try { File.Delete(_autoprintFlagPath); AppendLog("Removed .autoprint_enabled flag"); BtnToggleAutoPrintFlag.Content = "Ativar .autoprint_enabled"; }
                catch (Exception ex) { AppendLog($"Error removing flag: {ex.Message}"); }
            }
            else
            {
                try { File.WriteAllText(_autoprintFlagPath, "Auto-print ativado"); AppendLog("Created .autoprint_enabled flag"); BtnToggleAutoPrintFlag.Content = "Desativar .autoprint_enabled"; }
                catch (Exception ex) { AppendLog($"Error creating flag: {ex.Message}"); }
            }
        }

        private void ToggleAutoPrint(bool enable)
        {
            _autoPrintEnabled = enable;
            BtnToggleAutoPrint.Content = _autoPrintEnabled ? "AutoPrint: ON" : "AutoPrint: OFF";
            StatusText.Text = $"AutoPrint: {(_autoPrintEnabled ? "Ligado" : "Desligado")}";
            if (_printManager != null)
            {
                if (_autoPrintEnabled)
                {
                    _printManager.PrinterName = PrinterCombo.SelectedItem as string;
                    _printManager.Start();
                    AppendLog("AutoPrint watcher started");
                    // create compatibility flag file
                    try { File.WriteAllText(_autoprintFlagPath, "Auto-print ativado"); AppendLog("Created .autoprint_enabled flag"); } catch { }
                }
                else
                {
                    _printManager.Stop();
                    AppendLog("AutoPrint watcher stopped");
                    try { if (File.Exists(_autoprintFlagPath)) File.Delete(_autoprintFlagPath); AppendLog("Removed .autoprint_enabled flag"); } catch { }
                }
            }
            SaveConfig();
            AppendLog($"AutoPrint set to {_autoPrintEnabled}");
        }
        private void BtnRefreshPorts_Click(object sender, RoutedEventArgs e)
        {
            RefreshPorts();
        }

        private void BtnConnectPort_Click(object sender, RoutedEventArgs e)
        {
            if (_serialManager.IsOpen)
            {
                _serialManager.Close();
                BtnConnectPort.Content = "Conectar";
                AppendLog("Serial: desconectado");
                SaveConfig();
                return;
            }

            if (PortCombo.SelectedItem is string port)
            {
                try
                {
                    _serialManager.Open(port);
                    BtnConnectPort.Content = "Desconectar";
                    AppendLog($"Serial: conectado {port}");
                    SaveConfig();
                }
                catch (Exception ex)
                {
                    AppendLog($"Serial: falha ao conectar {ex.Message}");
                    MessageBox.Show($"Falha ao conectar: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void RefreshPorts()
        {
            try
            {
                PortCombo.Items.Clear();
                foreach (var p in _serialManager.GetPorts())
                    PortCombo.Items.Add(p);
                AppendLog("Ports refreshed");
            }
            catch { }
        }

        private void RefreshPrinters()
        {
            try
            {
                PrinterCombo.Items.Clear();
                foreach (var p in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
                    PrinterCombo.Items.Add(p);
                AppendLog("Printers refreshed");
            }
            catch { }
        }

        private void BtnRefreshPrinters_Click(object sender, RoutedEventArgs e)
        {
            RefreshPrinters();
        }

        private void BtnPrintNow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LPT-UNO", "DATA");
                var newest = Directory.GetFiles(dataFolder, "*.txt").OrderByDescending(f => File.GetCreationTimeUtc(f)).FirstOrDefault();
                if (newest == null) { MessageBox.Show("No files to print", "Info", MessageBoxButton.OK, MessageBoxImage.Information); return; }
                var pm = new PrintManager(dataFolder);
                pm.PrinterName = PrinterCombo.SelectedItem as string;
                pm.Start();
                MessageBox.Show($"Scheduled {Path.GetFileName(newest)} for printing.", "Print", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppendLog($"PrintNow error: {ex.Message}");
            }
        }

        private void BtnToggleMoveMonitor_Click(object sender, RoutedEventArgs e)
        {
            if (_moveMonitorRunning) StopMoveMonitor(); else StartMoveMonitor();
        }

        private void OnSerialDataReceived(string data)
        {
            AppendLog($"[RX] {data}");

            if (_autoPrintEnabled)
            {
                try
                {
                    var dataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LPT-UNO", "DATA");
                    Directory.CreateDirectory(dataFolder);
                    var fileName = Path.Combine(dataFolder, $"auto_{DateTime.Now:yyyyMMdd_HHmmss_fff}.txt");
                    File.AppendAllText(fileName, data);
                    AppendLog($"Saved incoming data to {fileName}");
                }
                catch (Exception ex)
                {
                    AppendLog($"Error saving data: {ex.Message}");
                }
            }
        }

        private void AppendLog(string text)
        {
            Dispatcher.Invoke(() =>
            {
                LogTextBox.AppendText($"{DateTime.Now:HH:mm:ss} {text}\n");
                LogTextBox.ScrollToEnd();
            });
        }

        private string? GetSavedSerialPort()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    dynamic cfg = JsonConvert.DeserializeObject(json);
                    return (string?)cfg?.serialPort;
                }
            }
            catch { }
            return null;
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    dynamic cfg = JsonConvert.DeserializeObject(json);
                    _autoPrintEnabled = cfg?.autoPrint ?? false;
                    BtnToggleAutoPrint.Content = _autoPrintEnabled ? "AutoPrint: ON" : "AutoPrint: OFF";

                    var serialPort = (string?)cfg?.serialPort;
                    var printer = (string?)cfg?.printer;
                    if (!string.IsNullOrEmpty(printer))
                    {
                        RefreshPrinters();
                        foreach (var item in PrinterCombo.Items)
                        {
                            if (item is string s && s == printer)
                            {
                                PrinterCombo.SelectedItem = s;
                                break;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(serialPort))
                    {
                        // attempt to select port
                        RefreshPorts();
                        foreach (var item in PortCombo.Items)
                        {
                            if (item is string s && s == serialPort)
                            {
                                PortCombo.SelectedItem = s;
                                try
                                {
                                    _serialManager.Open(s);
                                    BtnConnectPort.Content = "Desconectar";
                                    AppendLog($"Serial auto-connected to {s}");
                                }
                                catch { }
                                break;
                            }
                        }
                    }

                    if (_autoPrintEnabled && _printManager != null)
                    {
                        _printManager.PrinterName = PrinterCombo.SelectedItem as string;
                        _printManager.Start();
                        AppendLog("AutoPrint watcher started from config");
                    }
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "Erro ao carregar config";
                AppendLog($"LoadConfig error: {ex.Message}");
            }
        }

        private void SaveConfig()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_configPath));
                var cfg = new { autoPrint = _autoPrintEnabled, version = "1.0", serialPort = _serialManager.CurrentPortName, printer = PrinterCombo.SelectedItem as string };
                File.WriteAllText(_configPath, JsonConvert.SerializeObject(cfg, Formatting.Indented));
                AppendLog("Config saved");
            }
            catch (Exception ex)
            {
                AppendLog($"SaveConfig error: {ex.Message}");
            }
        }
    }
}