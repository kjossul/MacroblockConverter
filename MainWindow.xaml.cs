using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace MacroblockConverter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<string> selectedFiles = new List<string>();
        public ObservableCollection<string> LogMessages { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            LogMessages = new ObservableCollection<string>();
            logBox.ItemsSource = LogMessages;
        }

        private void Log(string message)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => Log(message));
                return;
            }
            LogMessages.Add($"[{DateTime.Now:HH:mm:ss}] {message}");

            // Auto-scroll to the bottom
            if (logBox.Items.Count > 0)
            {
                logBox.ScrollIntoView(logBox.Items[logBox.Items.Count - 1]);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog ofd = new OpenFolderDialog();
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string defaultPath = Path.Combine(documentsPath, @"Trackmania\Blocks\Stadium\");
            ofd.InitialDirectory = Directory.Exists(defaultPath) ? defaultPath : documentsPath;
            ofd.ShowDialog();
            LogMessages.Clear();
            selectedFiles.Clear();
            foreach (string folder in ofd.FolderNames) 
            {
                Log($"Parsing {folder}");
                var files = Directory.GetFiles(folder, "*.Macroblock.Gbx", SearchOption.AllDirectories);
                selectedFiles.AddRange(files);
                Log($"Found {selectedFiles.Count} macroblocks.");
            }
            convertButton.IsEnabled = selectedFiles.Count > 0;
        }

        private async void convertButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            convertButton.IsEnabled = false;
            Converter converter = new Converter(selectedFiles, preserveTrimmedCheckbox.IsChecked ?? false, nullifyVariantsCheckbox.IsChecked ?? false, Log);
            Log("=== Starting Conversion ===");
            await Task.Run(() => converter.Convert());
            Log("=== Conversion Complete ===");
            Log("Remember to restart your game!");
        }
    }
}