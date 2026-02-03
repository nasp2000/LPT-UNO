using System.Collections.Generic;

namespace LPTUnoApp
{
    public class AppConfig
    {
        public bool AutoPrint { get; set; } = false;
        public string? SerialPort { get; set; }
        public string? PrinterName { get; set; }
        public bool AutoConnect { get; set; } = false;
        public bool AutoNetworkConnect { get; set; } = true; // Auto-scan and connect to R4
        public int AutoSaveTime { get; set; } = 10; // Seconds
        public string ConnectionMode { get; set; } = "Serial"; // "Serial" or "Network"
        public string IpAddress { get; set; } = "192.168.4.1:2323";

        // Minimize behavior: when true, app will hide on minimize and remain in tray
        public bool MinimizeToTray { get; set; } = false;

        // Email / Google Apps Script Configuration
        public string? GoogleAppsScriptUrl { get; set; }
        public string SenderName { get; set; } = "LPT-UNO";
        public List<Recipient> Recipients { get; set; } = new List<Recipient>();
    }

    public class Recipient
    {
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
    }
}
