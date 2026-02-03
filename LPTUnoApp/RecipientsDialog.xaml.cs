using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace LPTUnoApp
{
    public partial class RecipientsDialog : Window
    {
        public ObservableCollection<Recipient> Recipients { get; set; }
        public string ScriptUrl { get; private set; }
        public string SenderName { get; private set; }

        public RecipientsDialog(string url, string senderName, List<Recipient> recipients)
        {
            InitializeComponent();
            ScriptUrl = url;
            SenderName = senderName;
            Recipients = new ObservableCollection<Recipient>(recipients.Select(r => new Recipient { Name = r.Name, Email = r.Email }));
            
            TxtScriptUrl.Text = ScriptUrl;
            TxtSenderName.Text = SenderName;
            GridRecipients.ItemsSource = Recipients;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            ScriptUrl = TxtScriptUrl.Text;
            SenderName = TxtSenderName.Text;
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
