using System.Windows;

namespace LPTUnoApp
{
    public partial class WifiSetupDialog : Window
    {
        public string Ssid { get; private set; } = "";
        public string Password { get; private set; } = "";

        public WifiSetupDialog()
        {
            InitializeComponent();
            TxtSsid.Focus();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            Ssid = TxtSsid.Text;
            Password = TxtPass.Password;

            if (string.IsNullOrWhiteSpace(Ssid))
            {
                System.Windows.MessageBox.Show("Digite o nome da rede (SSID).", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}