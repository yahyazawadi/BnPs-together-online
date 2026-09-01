using System.Windows;
using BnPRelay.Sync;

namespace BnPRelay
{
    public partial class RestoreSaveWindow : Window
    {
        public RestoreSaveWindow()
        {
            InitializeComponent();
            // Load backups for file0 (the primary save slot)
            ListBackups.ItemsSource = SaveFileMirror.ListBackups("file0");
        }

        private void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            if (ListBackups.SelectedItem is SaveBackupEntry entry)
            {
                entry.RestoreTo("file0");
                MessageBox.Show("Save restored! Restart Undertale to load it.",
                    "BnP Together ONLINE", MessageBoxButton.OK, MessageBoxImage.None);
                Close();
            }
            else
            {
                // Flash the list border red if nothing selected — Undertale-style feedback
                MessageBox.Show("* Choose a timeline first.", "BnP Together ONLINE",
                    MessageBoxButton.OK, MessageBoxImage.None);
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e) => Close();
    }
}
