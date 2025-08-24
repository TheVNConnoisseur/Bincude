using Microsoft.Win32;
using System.IO;
using System.Net.Mail;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Bincude
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        String[] FilePaths = Array.Empty<string>();
        String[] FileNames = Array.Empty<string>();

        private void RadioButton_Compress_Checked(object sender, RoutedEventArgs e)
        {
            Button_Original_File.IsEnabled = true;
            Button_Convert.IsEnabled = false;

            if (RadioButton_Compress.IsChecked == true)
            {
                ComboBox_Version_Selector.IsEnabled = true;
                ComboBox_Version_Selector.SelectedIndex = 0;
                RadioButton_Uncompress.IsChecked = false;
            }
        }

        private void RadioButton_Uncompress_Checked(object sender, RoutedEventArgs e)
        {
            Button_Original_File.IsEnabled = true;
            Button_Convert.IsEnabled = false;

            if (RadioButton_Uncompress.IsChecked == true)
            {
                ComboBox_Version_Selector.IsEnabled = false;
                ComboBox_Version_Selector.SelectedIndex = -1;
                RadioButton_Compress.IsChecked = false;
            }
        }

        private void Button_Original_File_Click(object sender, RoutedEventArgs e)
        {
            if (RadioButton_Uncompress.IsChecked == true)
            {
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.FileName = "";
                ofd.Multiselect = true;
                ofd.Filter = "Bin files (*.bin)|*.bin";
                ofd.FilterIndex = ofd.Filter.Length;
                Nullable<bool> result = ofd.ShowDialog();

                if (result == true)
                {
                    try
                    {
                        //Copy the values for the selected files to an array in order to manage the files later on
                        FilePaths = (string[])ofd.FileNames.Clone();
                        FileNames = new string[FilePaths.Length];
                        for (int CurrentFile = 0; CurrentFile < ofd.FileNames.Length; CurrentFile++)
                        {
                            FileNames[CurrentFile] = System.IO.Path.GetFileName(ofd.FileNames[CurrentFile]);
                        }

                        Button_Convert.IsEnabled = true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }

            else if (RadioButton_Compress.IsEnabled == true)
            {
                OpenFolderDialog ofd = new OpenFolderDialog();
                Nullable<bool> result = ofd.ShowDialog();

                if (result == true)
                {
                    try
                    {
                        FilePaths = Directory.GetFiles(ofd.FolderName, "*.*", SearchOption.AllDirectories);

                        //Check if the base folder path ends with \\, if not, we add it
                        string BaseFolder = System.IO.Path.GetFullPath(ofd.FolderName);
                        if (!BaseFolder.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString()))
                            BaseFolder += System.IO.Path.DirectorySeparatorChar;

                        //Calculate relative paths (file name + subfolders) with single backslash separator
                        FileNames = new string[FilePaths.Length];
                        for (int CurrentFile = 0; CurrentFile < FilePaths.Length; CurrentFile++)
                        {
                            //Remove base folder path prefix
                            string RelativePath = FilePaths[CurrentFile].Substring(BaseFolder.Length);

                            //Ensure path separators are backslashes, not double
                            FileNames[CurrentFile] = RelativePath.Replace(System.IO.Path.DirectorySeparatorChar, '\\');
                        }

                        Button_Convert.IsEnabled = true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void Button_Convert_Click(object sender, RoutedEventArgs e)
        {
            if (RadioButton_Compress.IsChecked == true)
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.AddExtension = true;
                sfd.DefaultExt = ".bin";
                sfd.Filter = "Bin files (*.bin)|*.bin";
                Nullable<bool> result = sfd.ShowDialog();
                if (result == true)
                {
                    try
                    {
                        List<Helper.FileInfo> SelectedFiles = new List<Helper.FileInfo>();

                        for (int CurrentFile = 0; CurrentFile < FilePaths.Length; CurrentFile++)
                        {
                            SelectedFiles.Add(new Helper.FileInfo
                            {
                                Name = FileNames[CurrentFile],
                                Data = File.ReadAllBytes(FilePaths[CurrentFile])
                            });
                        }

                        //Sort the files by name, ignoring case
                        SelectedFiles.Sort((x, y) => string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase));

                        if (ComboBox_Version_Selector.Text == "ESC-ARC1")
                        {
                            MessageBox.Show($"Version 1 of .BIN files are not supported.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }

                        File.WriteAllBytes(sfd.FileName, Bin.Compile(SelectedFiles, ComboBox_Version_Selector.Text));
                        MessageBox.Show($"Process completed successfully.", "Conversion completed.", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else if (RadioButton_Uncompress.IsChecked == true)
            {
                OpenFolderDialog ofd = new OpenFolderDialog();
                Nullable<bool> result = ofd.ShowDialog();

                if (result == true)
                {
                    try
                    {
                        List<Helper.FileInfo> SelectedFiles = new List<Helper.FileInfo>();

                        for (int CurrentFile = 0; CurrentFile < FilePaths.Length; CurrentFile++)
                        {
                            SelectedFiles.Add(new Helper.FileInfo
                            {
                                Name = FileNames[CurrentFile],
                                Data = File.ReadAllBytes(FilePaths[CurrentFile])
                            });
                        }

                        foreach (var file in SelectedFiles)
                        {
                            List<Helper.FileInfo> UncompressedFiles = Bin.Decompile(file);

                            string BaseFolder = System.IO.Path.GetFileNameWithoutExtension(file.Name);
                            string BaseOutputPath = System.IO.Path.Combine(ofd.FolderName, BaseFolder);

                            foreach (var UncompressedFile in UncompressedFiles)
                            {
                                string OutputFilePath = System.IO.Path.Combine(BaseOutputPath, UncompressedFile.Name);

                                //Create the folder if it does not exist
                                if (!Directory.Exists(
                                    System.IO.Path.GetDirectoryName(OutputFilePath)))
                                {
                                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(OutputFilePath));
                                }

                                //Write the file to the output folder
                                File.WriteAllBytes(OutputFilePath, UncompressedFile.Data);
                            }
                        }

                        MessageBox.Show($"Process completed successfully.", "Conversion completed.", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }

            RadioButton_Compress.IsChecked = false;
            RadioButton_Uncompress.IsChecked = false;
            ComboBox_Version_Selector.IsEnabled = false;
            ComboBox_Version_Selector.SelectedIndex = -1;
            Button_Convert.IsEnabled = false;
        }
    }
}