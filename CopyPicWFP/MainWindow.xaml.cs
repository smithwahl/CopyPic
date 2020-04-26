using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.IO;
using CopyPic;

namespace CopyPicWFP
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private int FileCount = 0;
        public MainWindow()
        {
            InitializeComponent();

            if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.Source))
            {
                txtSource.Text = Properties.Settings.Default.Source;
            }
            if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.Destination))
            {
                txtDestination.Text = Properties.Settings.Default.Destination;
            }
        }

        private void btnSource_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
            folderBrowserDialog.ShowNewFolderButton = false;
            if (Directory.Exists(txtSource.Text))
                folderBrowserDialog.SelectedPath = txtSource.Text;
            if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                txtSource.Content(folderBrowserDialog.SelectedPath);

        }

        private void btnDestination_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
            folderBrowserDialog.ShowNewFolderButton = true;
            if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                txtDestination.Content(folderBrowserDialog.SelectedPath);
        }

        private async void btnCopy_Click(object sender, RoutedEventArgs e)
        {
            txtOutput.Text = "";
            try
            {
                var originalConsoleOut = Console.Out; // preserve the original stream
                using (var writer = new ConsoleWriter())
                {
                    writer.WriteEvent += consoleWriter_WriteEvent;
                    writer.WriteLineEvent += consoleWriter_WriteLineEvent;

                    Console.SetOut(writer);

                    Progress<ProgressStatus> progress = new Progress<ProgressStatus>(UpdateProgress);

                    bool recursive = chkRecursive.IsChecked ?? false;
                    bool deleteOnCopy = chkDelete.IsChecked ?? false;
                    string source = txtSource.Text;
                    string destination = txtDestination.Text;
                    string searchPattern = txtSearch.Text;

                    EnableButtons(false);
                    await Task.Run(() => FileCount = CopyPic.CopyPic.Copy(source, destination, searchPattern, progress, recursive, deleteOnCopy));
                    System.Windows.MessageBox.Show($"Copied {FileCount} Files.");
                    EnableButtons(true);

                }

                Console.SetOut(originalConsoleOut); // restore Console.Out
            }
            catch (ArgumentNullException s)
            {
                System.Windows.MessageBox.Show(s.Message);
            }
            catch (DirectoryNotFoundException x)
            {
                System.Windows.MessageBox.Show(x.Message);
            }
            catch (Exception m)
            {
                throw;
            }
        }

        private void EnableButtons(bool enable)
        {
            btnCopy.IsEnabled = enable;
            btnDestination.IsEnabled = enable;
            btnSource.IsEnabled = enable;
        }

        private async void UpdateProgress(ProgressStatus update)
        {
            if (update.StatusCode == StatusCodeEnum.Initialize)
            {
                progressBar.Maximum = update.FileCount;
                progressBar.Value = 1;
                progressBar.Visibility = Visibility.Visible;
                lblSearchPattern.Visibility = txtSearch.Visibility = Visibility.Hidden;
            }
            if (update.StatusCode == StatusCodeEnum.Complete)
            {
                progressBar.Visibility = Visibility.Hidden;
                lblSearchPattern.Visibility = txtSearch.Visibility = Visibility.Visible;
                progressBar.Maximum = update.FileCount;
                progressBar.Value = 1;
            }
            if (update.StatusCode == StatusCodeEnum.FileCopy)
            {
                progressBar.Value++;
            }

            Console.WriteLine(update.Status);
        }

        private void consoleWriter_WriteLineEvent(object sender, ConsoleWriterEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtOutput.Text))
                txtOutput.AppendText(Environment.NewLine);
            txtOutput.AppendText(e.value);
            txtOutput.ScrollToEnd();
        }

        private void consoleWriter_WriteEvent(object sender, ConsoleWriterEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtOutput.Text))
                txtOutput.AppendText(Environment.NewLine);
        }

        private void txtSource_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                HandleDrops(txtSource, e);
            }
        }

        private void txtSource_PreviewDragOver(object sender, System.Windows.DragEventArgs e)
        {
            e.Handled = true;
        }

        private void txtDestination_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                HandleDrops(txtDestination, e);
            }
        }

        private void HandleDrops(System.Windows.Controls.TextBox source, System.Windows.DragEventArgs e)
        {
            // Note that you can have more than one file.
            string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            string directory = files[0];

            if (File.Exists(files[0]))
            {
                directory = Path.GetDirectoryName(files[0]);

            }

            if (directory != files[0])
                txtOutput.Text = $"That was a file that you dropped. I can't use that so I selected the path: {directory}";

            source.Content(directory);

        }

        private void txtDestination_PreviewDragOver(object sender, System.Windows.DragEventArgs e)
        {
            e.Handled = true;
        }
    }
}
