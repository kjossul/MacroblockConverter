using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;

namespace MacroblockConverter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<string> selectedFiles = new List<string>();
        private List<CheckBox> convertOptions = new List<CheckBox>();
        private Converter converter = new Converter();
        private bool itemsPresent = false;
        public ObservableCollection<string> LogMessages { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            LogMessages = new ObservableCollection<string>();
            convertOptions = new List<CheckBox> { TrackWallCheckbox, DecoWallCheckbox, DecoHillCheckbox, SnowRoadCheckbox, RallyCastleCheckbox, RallyRoadCheckbox, TransitionsCheckbox};
            logBox.ItemsSource = LogMessages;
            CheckItems();
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

        private async void CheckItems() {
            itemsPresent = await Task.Run(() => converter.CheckItems());
            if (itemsPresent)
            {
                itemsStatusText.Text = "All items found.";
                itemsStatusText.Foreground = new SolidColorBrush(Colors.Green);
                convertBlocksToItems.IsEnabled = true;
                convertBlocksToItems_Clicked(null, null);
            } else
            {
                itemsStatusText.Text = "Missing items, please use download button.";
                itemsStatusText.Foreground = new SolidColorBrush(Colors.Red);
                downloadItemsBtn.IsEnabled = true;
            }
        }

        private void Button_File_Click(object sender, RoutedEventArgs e)
        { 
            OpenFileDialog ofd = new OpenFileDialog();
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string defaultPath = Path.Combine(documentsPath, @"Trackmania\Blocks\Stadium\");
            ofd.InitialDirectory = Directory.Exists(defaultPath) ? defaultPath : documentsPath;
            ofd.Multiselect = true;
            ofd.Filter = "Macroblocks|*.Macroblock.Gbx";
            LogMessages.Clear();
            selectedFiles.Clear();
            if (ofd.ShowDialog().GetValueOrDefault())
            {
                selectedFiles.AddRange(ofd.FileNames);
                convertButton.IsEnabled = true;
                Log($"Selected {selectedFiles.Count} macroblock(s).");
            }
            else
            {
                convertButton.IsEnabled = false;
            }
        }

        private void Button_Folder_Click(object sender, RoutedEventArgs e)
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
            Log("=== Starting Conversion ===");
            convertButton.IsEnabled = false;
            List<string> opts = convertOptions.Where(checkBox => checkBox.IsChecked.Value).Select(checkBox => checkBox.Content.ToString()).ToList();
            bool preserveTrimmed = preserveTrimmedCheckbox.IsChecked ?? false;
            bool nullifyVariants = nullifyVariantsCheckbox.IsChecked ?? false;
            bool createConverted = createConvertedFolderCheckbox.IsChecked ?? false;
            bool convertBlocks = convertBlocksToItems.IsEnabled && (convertBlocksToItems.IsChecked ?? false);

            await Task.Run(() => converter.Convert(
                selectedFiles,
                preserveTrimmed,
                nullifyVariants,
                createConverted,
                convertBlocks,
                opts,
                Log
            ));
            convertButton.IsEnabled = true;
            Log("=== Conversion Complete ===");
            Log("Remember to restart your game!");
        }

        private void convertBlocksToItems_Clicked(object sender, RoutedEventArgs e)
        {
            foreach (CheckBox cb in convertOptions)
            {
                cb.IsEnabled = convertBlocksToItems.IsChecked.GetValueOrDefault();
            }
        }

        private void OpenGithub(object sender, RequestNavigateEventArgs e)
        {

            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
    }
}