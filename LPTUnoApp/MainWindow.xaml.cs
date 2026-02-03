using System;
using System.IO;
using System.Windows;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;
using Newtonsoft.Json;

namespace LPTUnoApp
{
    public partial class MainWindow : Window
    {
        private NotifyIcon _notifyIcon;
        private IntPtr _trayIconHandle = IntPtr.Zero;

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);
        private readonly string _configPath;
        private bool _autoPrintEnabled = false; // Restored
        
        private readonly SerialManager _serialManager = new SerialManager();
        private readonly NetworkManager _networkManager = new NetworkManager();
        private IConnectionSource _activeConnection;

        private CancellationTokenSource? _networkDiscoveryCts;
        private bool _scanInProgress = false;

        private PrintManager? _printManager;
        private MoveManager? _moveManager;
        private bool _moveMonitorRunning = false;
        private System.Timers.Timer? _reconnectTimer;
        private readonly string _dataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LPT-UNO", "DATA");
        private readonly string _downloadsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        private readonly string _autoprintFlagPath;
        
        // New Components
        private AppConfig _config = new AppConfig();
        private DataProcessor _dataProcessor;
        private EmailService _emailService = new EmailService();

        public MainWindow()
        {
            InitializeComponent();

            // --- Startup debug helper: writes to %APPDATA% and shows a MessageBox to confirm process lives ---
            string startupLogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LPT-UNO", "startup_debug.log");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(startupLogPath)!);
                bool isElevated = new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent()).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
                File.AppendAllText(startupLogPath, $"{DateTime.Now:O} Startup begin. User={Environment.UserName} Elevated={isElevated} CMD={Environment.CommandLine}\n");

                // Show a blocking MessageBox so the user (or we) can confirm the app started under the current user
                System.Windows.MessageBox.Show($"LPT-UNO starting\nUser: {Environment.UserName}\nElevated: {isElevated}\n\nIf this window does not appear when you run the EXE, the process is likely exiting before showing UI.\nLog: {startupLogPath}", "LPT-UNO Debug Startup", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                try { File.AppendAllText(startupLogPath, $"{DateTime.Now:O} Startup exception: {ex}\n"); } catch { }
            }

            // Global exception handlers to capture crashes
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                try { File.AppendAllText(startupLogPath, $"{DateTime.Now:O} UnhandledException: {e.ExceptionObject}\n"); } catch { }
            };
            System.Windows.Application.Current.DispatcherUnhandledException += (s, e) =>
            {
                try { File.AppendAllText(startupLogPath, $"{DateTime.Now:O} DispatcherUnhandledException: {e.Exception}\n"); } catch { }
                e.Handled = true;
            };
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                try { File.AppendAllText(startupLogPath, $"{DateTime.Now:O} TaskSchedulerException: {e.Exception}\n"); } catch { }
            };

            _configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LPT-UNO", "config.json");
            
            // Initialize DataProcessor
            _dataProcessor = new DataProcessor(_dataFolder, 10);
            _dataProcessor.FileSaved += OnDataProcessorFileSaved;
            _dataProcessor.LogMessage += (s, msg) => AppendLog(msg);

            InitializeTray();
            
            _activeConnection = _serialManager;
            _serialManager.DataReceived += OnSerialDataReceived;
            _networkManager.DataReceived += OnSerialDataReceived;
            _networkManager.ConnectionChanged += (connected, info) => Dispatcher.Invoke(() =>
            {
                if (connected)
                {
                    BtnConnectNetwork.Content = "Desconectar";
                    TxtIpAddress.Text = info;
                    RbNetwork.IsChecked = true;
                    AppendLog($"Network connected: {info}");
                }
                else
                {
                    BtnConnectNetwork.Content = "Conectar";
                    AppendLog("Network disconnected");
                }
                SaveConfig();
            });

            _autoprintFlagPath = Path.Combine(Path.GetDirectoryName(_configPath) ?? _dataFolder, ".autoprint_enabled");
            RefreshPorts();
            LoadConfig();
            // Start background discovery loop if enabled
            StartNetworkDiscoveryLoop();
            InitializePrintManager();
            InitializeReconnectTimer();
            InitializeAutoConnectTimer();
            StartDataWatcher();
            RefreshDataFiles();
            StatusText.Text = "Pronto";

            // Ensure main window is shown on startup (useful when app starts minimized to tray)
            try { ShowMainWindow(); } catch { }

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
                bool isSerialMode = false;
                Dispatcher.Invoke(() => isSerialMode = RbSerial.IsChecked == true);

                if (isSerialMode && !_serialManager.IsOpen)
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
                // Ensure images folder and a printer icon exist. If not, create a small placeholder PNG from embedded base64.
                var imagesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images");
                Directory.CreateDirectory(imagesDir);
                var imgPath = Path.Combine(imagesDir, "printer.png");
                if (!File.Exists(imgPath))
                {
                    // Create a simple printer icon programmatically and save it as PNG
                    try
                    {
                        using var generatedBmp = new System.Drawing.Bitmap(32, 32);
                        using (var g = System.Drawing.Graphics.FromImage(generatedBmp))
                        {
                            g.Clear(System.Drawing.Color.Transparent);

                            // Paper
                            using var paperBrush = new System.Drawing.SolidBrush(System.Drawing.Color.White);
                            g.FillRectangle(paperBrush, 6, 4, 20, 10);
                            using var pen = new System.Drawing.Pen(System.Drawing.Color.Black, 1);
                            g.DrawRectangle(pen, 6, 4, 20, 10);
                            g.DrawLine(pen, 8, 7, 18, 7);
                            g.DrawLine(pen, 8, 9, 14, 9);

                            // Printer body
                            using var bodyBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(60, 60, 60));
                            g.FillRectangle(bodyBrush, 4, 12, 24, 12);
                            g.DrawRectangle(pen, 4, 12, 24, 12);

                            // Control indicator
                            using var indicatorBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(0, 120, 215));
                            g.FillEllipse(indicatorBrush, 8, 18, 4, 4);
                        }

                        try { generatedBmp.Save(imgPath, System.Drawing.Imaging.ImageFormat.Png); } catch { }
                    }
                    catch { }
                }

                if (File.Exists(imgPath))
                {
                    using var bmp = new System.Drawing.Bitmap(imgPath);
                        var hIcon = bmp.GetHicon();
                        _notifyIcon.Icon = System.Drawing.Icon.FromHandle(hIcon);
                        _trayIconHandle = hIcon;

                        // Also ensure a TrayIcon.ico exists for external use
                        var icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TrayIcon.ico");
                        if (!File.Exists(icoPath))
                        {
                            try
                            {
                                using var iconForSave = System.Drawing.Icon.FromHandle(hIcon);
                                using var fs = File.Create(icoPath);
                                iconForSave.Save(fs);
                            }
                            catch { }
                        }
                }
                else if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TrayIcon.ico")))
                {
                    _notifyIcon.Icon = new System.Drawing.Icon("TrayIcon.ico");
                }
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
                {
                    // If user enabled minimize-to-tray, hide the window and show a brief balloon tip
                    if (ChkMinimizeToTray != null && ChkMinimizeToTray.IsChecked == true)
                    {
                        try
                        {
                            this.Hide();
                            try { _notifyIcon.ShowBalloonTip(1000, "LPT-UNO", "Minimizado para tray (duplo clique para abrir)", ToolTipIcon.Info); } catch { }
                        }
                        catch { }
                    }
                }
                else if (this.WindowState == WindowState.Normal)
                {
                    try { this.Show(); } catch { }
                }
            };

            this.Closing += (s, e) =>
            {
                _notifyIcon.Visible = false;
                try { _notifyIcon.Dispose(); } catch { }
                if (_trayIconHandle != IntPtr.Zero)
                {
                    try { DestroyIcon(_trayIconHandle); } catch { }
                    _trayIconHandle = IntPtr.Zero;
                }
                _serialManager.Dispose();
                _networkManager.Dispose();
                StopNetworkDiscoveryLoop();
            };
        }

        private void ShowMainWindow()
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }

        private void ChkAutoNetwork_Checked(object sender, RoutedEventArgs e)
        {
            StartNetworkDiscoveryLoop();
            SaveConfig();
        }

        private void ChkAutoNetwork_Unchecked(object sender, RoutedEventArgs e)
        {
            StopNetworkDiscoveryLoop();
            SaveConfig();
        }

        private void ChkMinimizeToTray_Checked(object sender, RoutedEventArgs e)
        {
            SaveConfig();
        }

        private void ChkMinimizeToTray_Unchecked(object sender, RoutedEventArgs e)
        {
            SaveConfig();
        }

        private void ExitApp()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _serialManager.Dispose();
            _networkManager.Dispose();
            System.Windows.Application.Current.Shutdown();
        }

        private void BtnOpenDataFolder_Click(object sender, RoutedEventArgs e)
        {
            var dataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LPT-UNO", "DATA");
            Directory.CreateDirectory(dataFolder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo() { FileName = dataFolder, UseShellExecute = true });
            AppendLog($"Opened DATA folder: {dataFolder}");
            RefreshDataFiles();
        }

        private void BtnRefreshDataFiles_Click(object sender, RoutedEventArgs e)
        {
            RefreshDataFiles();
        }

        private void DataFilesList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                if (DataFilesList.SelectedItem is string name)
                {
                    var path = Path.Combine(_dataFolder, name);
                    if (File.Exists(path))
                    {
                        FilePreview.Text = File.ReadAllText(path);
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog($"DataFilesList selection error: {ex.Message}");
            }
        }

        private void BtnOpenDataFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DataFilesList.SelectedItem is string name)
                {
                    var path = Path.Combine(_dataFolder, name);
                    if (File.Exists(path)) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo() { FileName = path, UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Open file error: {ex.Message}");
            }
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

        private void RbConnection_Checked(object sender, RoutedEventArgs e)
        {
            if (PanelSerial == null || PanelNetwork == null) return;

            if (RbSerial.IsChecked == true)
            {
                PanelSerial.IsEnabled = true;
                PanelNetwork.IsEnabled = false;
                _activeConnection = _serialManager;
                
                // If network is open, close it?
                if (_networkManager.IsOpen) _networkManager.Close();
                BtnConnectNetwork.Content = "Conectar Rede";
            }
            else
            {
                PanelSerial.IsEnabled = false;
                PanelNetwork.IsEnabled = true;
                _activeConnection = _networkManager;

                // If serial is open, close it?
                if (_serialManager.IsOpen) _serialManager.Close();
                BtnConnectPort.Content = "Conectar Serial";
            }
            SaveConfig();
        }

        private async void BtnConfigWifi_Click(object sender, RoutedEventArgs e)
        {
            if (!_serialManager.IsOpen)
            {
                MessageBox.Show("Conecte ao Arduino via USB primeiro!", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new WifiSetupDialog();
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                // Send command
                string cmd = $"CMD:WIFI:{dialog.Ssid}:{dialog.Password}";
                try 
                {
                    await _serialManager.WriteAsync(cmd + "\n");
                    MessageBox.Show("Configuração enviada! O Arduino irá reiniciar e conectar ao WiFi.\nVerifique o Monitor Serial ou use o Scan.", "Sucesso");
                    AppendLog($"WiFi Config sent for SSID: {dialog.Ssid}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Erro ao enviar configuração: " + ex.Message);
                }
            }
        }

        private async void BtnScanWifi_Click(object sender, RoutedEventArgs e)
        {
            BtnScanWifi.IsEnabled = false;
            AppendLog("Scanning for LPT-UNO (UDP)...");
            try
            {
                string? ip = await ScanForDevice();
                if (ip != null)
                {
                    TxtIpAddress.Text = ip + ":2323";
                    AppendLog("Device found: " + ip);
                }
                else
                {
                    AppendLog("Device not found.");
                    MessageBox.Show("Nenhum R4 encontrado. Certifique-se que está na mesma rede e que o firmware 1.1 está rodando.", "Scan");
                }
            }
            catch (Exception ex)
            {
                AppendLog("Scan error: " + ex.Message);
            }
            finally
            {
                BtnScanWifi.IsEnabled = true;
            }
        }

        private async Task<string?> ScanForDevice()
        {
            if (_scanInProgress) return null;
            _scanInProgress = true;
            try
            {
                // Log start
                try { Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LPT-UNO")); File.AppendAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LPT-UNO", "discovery.log"), $"{DateTime.Now:O} Scan started\n"); } catch { }

                // Simple UDP listener setup
                using (var udpClient = new UdpClient())
                {
                    // Enable broadcast reception
                    udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, 2324));

                    try
                    {
                        var task = udpClient.ReceiveAsync();
                        // Wait up to 6 seconds (Arduino broadcasts every 5s)
                        if (await Task.WhenAny(task, Task.Delay(6000)) == task)
                        {
                            var result = task.Result;
                            string msg = Encoding.ASCII.GetString(result.Buffer);
                            try { File.AppendAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LPT-UNO", "discovery.log"), $"{DateTime.Now:O} Received UDP: '{msg}' from {result.RemoteEndPoint.Address}\n"); } catch { }
                            if (msg.StartsWith("LPT-UNO"))
                            {
                                // Return IPv6 addresses in bracketed form so callers can append :port safely (e.g., [fe80::1%12]:2323)
                                var addr = result.RemoteEndPoint.Address;
                                if (addr.AddressFamily == AddressFamily.InterNetworkV6)
                                    return $"[{addr}]";
                                return addr.ToString();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        AppendLog("UDP Receive Error: " + ex.Message);
                        try { File.AppendAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LPT-UNO", "discovery.log"), $"{DateTime.Now:O} UDP Error: {ex.Message}\n"); } catch { }
                    }
                }
            }
            finally
            {
                _scanInProgress = false;
            }
            return null;
        }

        private void StartNetworkDiscoveryLoop()
        {
            if (_networkDiscoveryCts != null) return;
            try { Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LPT-UNO")); File.AppendAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LPT-UNO", "discovery.log"), $"{DateTime.Now:O} Discovery loop starting\n"); } catch { }
            _networkDiscoveryCts = new CancellationTokenSource();
            var ct = _networkDiscoveryCts.Token;

            Task.Run(async () =>
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        try { File.AppendAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LPT-UNO", "discovery.log"), $"{DateTime.Now:O} Discovery iteration\n"); } catch { }
                        if (!_networkManager.IsOpen && _config.AutoNetworkConnect)
                        {
                            try { File.AppendAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LPT-UNO", "discovery.log"), $"{DateTime.Now:O} Calling ScanForDevice\n"); } catch { }
                            var ip = await ScanForDevice();
                            if (!string.IsNullOrEmpty(ip))
                            {
                                try
                                {
                                    Dispatcher.Invoke(() => TxtIpAddress.Text = ip + ":2323");
                                    AppendLog($"Auto-discovered device {ip}, attempting connection...");
                                    await Task.Run(() => _networkManager.Open(ip + ":2323"));
                                    _activeConnection = _networkManager;

                                    // If serial was open, close it so network becomes primary
                                    if (_serialManager.IsOpen)
                                    {
                                        try { _serialManager.Close(); Dispatcher.Invoke(() => BtnConnectPort.Content = "Conectar"); } catch { }
                                    }

                                    Dispatcher.Invoke(() => RbNetwork.IsChecked = true);
                                    AppendLog($"Auto-connected to {ip}");
                                    SaveConfig();
                                    try { File.AppendAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LPT-UNO", "discovery.log"), $"{DateTime.Now:O} Auto-connected to {ip}\n"); } catch { }
                                }
                                catch (Exception ex)
                                {
                                    AppendLog($"Auto-connect failed: {ex.Message}");
                                    try { File.AppendAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LPT-UNO", "discovery.log"), $"{DateTime.Now:O} Auto-connect failed: {ex.Message}\n"); } catch { }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        AppendLog("Discovery loop error: " + ex.Message);
                        try { File.AppendAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LPT-UNO", "discovery.log"), $"{DateTime.Now:O} Discovery loop error: {ex.Message}\n"); } catch { }
                    }

                    try { await Task.Delay(3000, ct); } catch { }
                }
            }, ct);
        }

        private void StopNetworkDiscoveryLoop()
        {
            try
            {
                _networkDiscoveryCts?.Cancel();
                _networkDiscoveryCts?.Dispose();
            }
            catch { }
            _networkDiscoveryCts = null;
        }

        private async void BtnConnectNetwork_Click(object sender, RoutedEventArgs e)
        {
            if (_networkManager.IsOpen)
            {
                _networkManager.Close();
                BtnConnectNetwork.Content = "Conectar Rede";
                AppendLog("Rede: desconectado");
                SaveConfig();
                return;
            }

            string ipPort = TxtIpAddress.Text.Trim();
            if (string.IsNullOrEmpty(ipPort))
            {
                MessageBox.Show("Digite o endereço IP:Porta (ex: 192.168.1.100:2323)");
                return;
            }

            try
            {
                BtnConnectNetwork.Content = "Conectando...";
                await Task.Run(() => _networkManager.Open(ipPort));
                BtnConnectNetwork.Content = "Desconectar";
                AppendLog($"Rede: conectado a {ipPort}");
                SaveConfig();
            }
            catch (Exception ex)
            {
                BtnConnectNetwork.Content = "Conectar Rede";
                AppendLog($"Rede: falha ao conectar {ex.Message}");
                MessageBox.Show($"Erro de conexão: {ex.Message}");
            }
        }

        private void ChkAutoConnect_Checked(object sender, RoutedEventArgs e)
        {
            AppendLog("Auto-Connect enabled");
            _autoConnectTimer?.Start();
            SaveConfig();
        }

        private void ChkAutoConnect_Unchecked(object sender, RoutedEventArgs e)
        {
            AppendLog("Auto-Connect disabled");
            _autoConnectTimer?.Stop();
            SaveConfig();
        }

        private System.Timers.Timer? _autoConnectTimer;
        private FileSystemWatcher? _dataWatcher;

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

        private void InitializeAutoConnectTimer()
        {
            _autoConnectTimer = new System.Timers.Timer(5000);
            _autoConnectTimer.Elapsed += async (s, e) =>
            {
                bool isSerialMode = false;
                Dispatcher.Invoke(() => isSerialMode = RbSerial.IsChecked == true);

                if (isSerialMode && ChkAutoConnect.IsChecked == true && !_serialManager.IsOpen)
                {
                    AppendLog("Auto-Connect: scanning ports...");
                    var ok = await _serialManager.ProbeAndOpenAsync();
                    if (ok) Dispatcher.Invoke(() =>
                    {
                        RefreshPorts();
                        BtnConnectPort.Content = "Desconectar";
                        AppendLog("Auto-Connect: device found and connected");
                    });
                }
            };
        }

        private void StartDataWatcher()
        {
            try
            {
                if (_dataWatcher != null) return;
                Directory.CreateDirectory(_dataFolder);
                _dataWatcher = new FileSystemWatcher(_dataFolder, "*.txt") { NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite, EnableRaisingEvents = true };
                _dataWatcher.Created += (s, e) => Dispatcher.Invoke(() => RefreshDataFiles());
                _dataWatcher.Deleted += (s, e) => Dispatcher.Invoke(() => RefreshDataFiles());
                _dataWatcher.Renamed += (s, e) => Dispatcher.Invoke(() => RefreshDataFiles());
            }
            catch { }
        }

        private void StopDataWatcher()
        {
            try
            {
                _dataWatcher?.Dispose();
                _dataWatcher = null;
            }
            catch { }
        }

        private void RefreshDataFiles()
        {
            try
            {
                Directory.CreateDirectory(_dataFolder);
                DataFilesList.Items.Clear();
                var files = Directory.GetFiles(_dataFolder, "*.txt").OrderByDescending(f => File.GetCreationTimeUtc(f));
                foreach (var f in files) DataFilesList.Items.Add(Path.GetFileName(f));
                AppendLog($"Data files refreshed ({DataFilesList.Items.Count})");
            }
            catch (Exception ex)
            {
                AppendLog($"RefreshDataFiles error: {ex.Message}");
            }
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
            // Use DataProcessor to buffer and handle timeout
            _dataProcessor.AddData(data);
        }

        private async void OnDataProcessorFileSaved(object? sender, string filePath)
        {
            AppendLog($"File saved: {filePath}");
            
            // 1. Check and Send Emails
            try 
            {
                string content = File.ReadAllText(filePath);
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"); 
                // Note: file name has timestamp already, but we can regenerate valid one
                
                // Extract timestamp from filename if possible or just use Now
                var parts = fileName.Split('_');
                if (parts.Length >= 2) timestamp = parts[1] + "_" + parts[2]; // crude approximation

                int sent = await _emailService.CheckAndSendEmails(content, timestamp, "lpt-uno", _config);
                if (sent > 0) AppendLog($"Sent {sent} emails via Google Apps Script.");
            }
            catch (Exception ex)
            {
                AppendLog($"Email error: {ex.Message}");
            }

            // 2. Auto Printing
            if (_config.AutoPrint)
            {
                // PrintManager is a FileSystemWatcher. If it's running (started in LoadConfig/Toggle),
                // it will automatically pick up the new file from _dataFolder and print it.
                if (_printManager == null)
                {
                     AppendLog("Warning: AutoPrint enabled but PrintManager is not initialized.");
                }
                else
                {
                    AppendLog("File queued for AutoPrint (PrintManager watcher).");
                }
            }
        }

        private void AppendLog(string text)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    if (LogTextBox != null)
                    {
                        LogTextBox.AppendText($"{DateTime.Now:HH:mm:ss} {text}\n");
                        LogTextBox.ScrollToEnd();
                    }
                    else
                    {
                        // Fallback to disk if UI not initialized yet
                        try { File.AppendAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LPT-UNO", "discovery.log"), $"{DateTime.Now:O} {text}\n"); } catch { }
                    }
                });
            }
            catch
            {
                try { File.AppendAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LPT-UNO", "discovery.log"), $"{DateTime.Now:O} {text}\n"); } catch { }
            }
        }

        private string? GetSavedSerialPort()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    var cfg = JsonConvert.DeserializeObject<AppConfig>(json);
                    return cfg?.SerialPort;
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
                    _config = JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
                }
                else
                {
                    _config = new AppConfig();
                }

                _autoPrintEnabled = _config.AutoPrint;
                BtnToggleAutoPrint.Content = _autoPrintEnabled ? "AutoPrint: ON" : "AutoPrint: OFF";

                ChkAutoConnect.IsChecked = _config.AutoConnect;
                if (_config.AutoConnect) _autoConnectTimer?.Start();

                // Restore IP
                if (!string.IsNullOrEmpty(_config.IpAddress))
                {
                    TxtIpAddress.Text = _config.IpAddress;
                }

                // Restore Mode
                if (_config.ConnectionMode == "Network")
                {
                    RbNetwork.IsChecked = true;
                }
                else
                {
                    RbSerial.IsChecked = true;
                }

                // Restore Auto-Network Connect
                ChkAutoNetwork.IsChecked = _config.AutoNetworkConnect;

                // Apply UI state
                RbConnection_Checked(null, null);

                if (_config.AutoNetworkConnect)
                {
                    StartNetworkDiscoveryLoop();
                }

                if (!string.IsNullOrEmpty(_config.PrinterName))
                {
                    RefreshPrinters();
                    foreach (var item in PrinterCombo.Items)
                    {
                        if (item is string s && s == _config.PrinterName)
                        {
                            PrinterCombo.SelectedItem = s;
                            break;
                        }
                    }
                }

                // If serial mode selected, try to restore port
                if (_config.ConnectionMode != "Network" && !string.IsNullOrEmpty(_config.SerialPort))
                {
                    RefreshPorts();
                    foreach (var item in PortCombo.Items)
                    {
                        if (item is string s && s == _config.SerialPort)
                        {
                            PortCombo.SelectedItem = s;
                            // Attempt to open if strictly desired or rely on AutoConnect timer?
                            // Legacy code tried to open immediately.
                            try 
                            { 
                                _serialManager.Open(s); 
                                BtnConnectPort.Content = "Desconectar";
                                AppendLog($"Serial connected to {s}");
                            } 
                            catch {}
                            break;
                        }
                    }
                }

                if (_config.AutoPrint) _printManager?.Start();
                else _printManager?.Stop();

                if (_dataProcessor != null) _dataProcessor.UpdateTimeout(_config.AutoSaveTime);

                // Minimize-to-tray preference
                try { ChkMinimizeToTray.IsChecked = _config.MinimizeToTray; } catch { }
            }
            catch (Exception ex)
            {
                AppendLog($"Error loading config: {ex.Message}");
            }
        }

        private void SaveConfig()
        {
            try
            {
                _config.AutoPrint = _autoPrintEnabled;
                
                // Serial config
                _config.SerialPort = _serialManager.CurrentPortName; 
                if (string.IsNullOrEmpty(_config.SerialPort)) _config.SerialPort = PortCombo.SelectedItem as string;
                
                // Network config
                _config.IpAddress = TxtIpAddress.Text;
                _config.ConnectionMode = (RbNetwork.IsChecked == true) ? "Network" : "Serial";
                _config.AutoNetworkConnect = ChkAutoNetwork.IsChecked == true;

                _config.PrinterName = PrinterCombo.SelectedItem as string;
                _config.AutoConnect = ChkAutoConnect.IsChecked == true;
                _config.MinimizeToTray = ChkMinimizeToTray.IsChecked == true;
                
                var json = JsonConvert.SerializeObject(_config, Formatting.Indented);
                Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
                File.WriteAllText(_configPath, json);
                // AppendLog("Config saved"); // Spam reduction
            }
            catch (Exception ex)
            {
                AppendLog($"Error saving config: {ex.Message}");
            }
        }

        private void BtnConfigEmails_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new RecipientsDialog(
                _config.GoogleAppsScriptUrl ?? "",
                _config.SenderName,
                _config.Recipients
            );
            
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                _config.GoogleAppsScriptUrl = dialog.ScriptUrl;
                _config.SenderName = dialog.SenderName;
                _config.Recipients = dialog.Recipients.ToList();
                SaveConfig();
                AppendLog("Email configuration updated.");
            }
        }
    }
}