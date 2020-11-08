using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace CopyPic
{
    /// <summary>
    /// Main object to copy the files to the location based on the file names or attributes
    /// </summary>
    public static class CopyPic
    {
        const int BYTES_TO_READ = sizeof(long);

        private static int FileCount { get; set; } = 0;
        private static bool Recursive { get; set; } = false;
        private static bool DeleteOnCopy { get; set; } = false;
        private static bool hasDateTaken = false;


        /// <summary>
        /// This is the entry point for the object. It will start the copy and send progress messages.
        /// It does some validation and starts the copy for each type of file.
        /// </summary>
        /// <param name="source">Location of the files to copy.</param>
        /// <param name="destination">When you want to copy the files.</param>
        /// <param name="searchPattern">The pattern to use when searching for the files. I.e. *.jpg;*.png;*.bmp;*.mp4;*.nar;*.mov</param>
        /// <param name="progress">Interface to the progress handler.</param>
        /// <param name="subFolders">Tells the object to process folders recursively.</param>
        /// <returns>The number of files copied.</returns>
        public static int Copy(string source, string destination, string searchPattern, IProgress<ProgressStatus> progress, bool subFolders = false, bool deleteOnCopy = false)
        {
            Recursive = subFolders;
            DeleteOnCopy = deleteOnCopy;
            FileCount = 0;
            if (string.IsNullOrWhiteSpace(source) && string.IsNullOrWhiteSpace(destination))
                throw new ArgumentNullException("You must tell me what to copy and where to copy it.");
            if (string.IsNullOrWhiteSpace(source))
                throw new ArgumentNullException("You must tell me what folder to search.");
            if (string.IsNullOrWhiteSpace(destination))
                throw new ArgumentNullException("You must tell me where to place the pictures.");
            if (!Verify(source))
                throw new DirectoryNotFoundException("The Source that you indicated does not exist.");
            if (!Verify(destination))
                throw new DirectoryNotFoundException("The Destination that you indicated does not exist.");

            var searchPatterns = searchPattern.Split(';');
            foreach (var pattern in searchPatterns)
            {
                CopyTheFiles(source, destination, pattern, progress);

            }
            if (FileCount > 0)
                UpdateProgress(StatusCodeEnum.Complete, $"Found {FileCount} Files.", FileCount, progress);

            return FileCount;
        }

        /// <summary>
        /// This method is responsible for copying the files.
        /// </summary>
        /// <param name="source">Location of the files to copy.</param>
        /// <param name="destination">When you want to copy the files.</param>
        /// <param name="searchPattern">The pattern to use when searching for the files. I.e. *.jpg;*.png;*.bmp;*.mp4;*.nar;*.mov</param>
        /// <param name="progress">Interface to the progress handler.</param>
        private static void CopyTheFiles(string source, string destination, string searchPattern, IProgress<ProgressStatus> progress)
        {
            try
            {
                var images = Recursive ?
                    Directory.EnumerateFiles(source, searchPattern, SearchOption.AllDirectories) :
                    Directory.EnumerateFiles(source, searchPattern);

                int totalFiles = Directory.GetFiles(source, searchPattern, SearchOption.AllDirectories).Length;
                string filePlurality = totalFiles > 1 ? "files" : "file";
                UpdateProgress(StatusCodeEnum.Initialize, $"Copying {totalFiles} {searchPattern} {filePlurality}.", totalFiles, progress);

                foreach (string image in images)
                {
                    bool copyImage = true;
                    DateTime fileDate = GetFileDate(image);
                    string destinationPath = Path.Combine(destination, fileDate.Year.ToString());
                    destinationPath = Path.Combine(destinationPath, fileDate.Month.ToString());
                    if (Directory.CreateDirectory(destinationPath).Exists)
                    {
                        string fileName = Path.GetFileName(image);
                        UpdateProgress(StatusCodeEnum.FileCopy, $"copy {fileName} to {Path.Combine(destinationPath, fileName)}", totalFiles, progress);
                        string destinationFile = Path.Combine(destinationPath, fileName);
                        if (File.Exists(destinationFile))
                        {
                            int potentialNumber = 1;
                            copyImage = false;
                            while (!FilesAreEqual(image, destinationFile))
                            {
                                destinationFile = Path.Combine(destinationPath, $"{Path.GetFileNameWithoutExtension(fileName)} ({potentialNumber}){Path.GetExtension(fileName)}");
                                potentialNumber++;
                                if (!File.Exists(destinationFile))
                                {
                                    copyImage = true;
                                    break;
                                }
                            }
                        }
                        int retryCount = 0;
                        while (copyImage)
                        {
                            if (3 == retryCount)
                                copyImage = false;

                            if (copyImage)
                            {
                                try
                                {
                                    if (File.Exists(image))
                                    {
                                        if (!File.Exists(destinationFile))
                                        {
                                            File.Copy(image, destinationFile);
                                        }
                                        if (DeleteOnCopy && File.Exists(destinationFile))
                                        {
                                            FileAttributes attributes = File.GetAttributes(image);
                                            if ((attributes & FileAttributes.ReadOnly) != FileAttributes.ReadOnly)
                                            {
                                                File.Delete(image);
                                            }
                                            else
                                            {
                                                UpdateProgress(StatusCodeEnum.Error, $"Cannot delete because it is read only: {image}", 0, progress);
                                            }
                                        }
                                        FileCount++;
                                        copyImage = false;

                                    }
                                }
                                catch (UnauthorizedAccessException u)
                                {
                                    UpdateProgress(StatusCodeEnum.Error, $"{u.Message} :: Retrying.", 0, progress);
                                    retryCount++;
                                    Thread.Sleep(3000);
                                }
                                catch (Exception e)
                                {
                                    throw;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                UpdateProgress(StatusCodeEnum.Error, e.Message, 0, progress);
            }

        }

        /// <summary>
        /// This method obtains the date for the image so that it can be placed in the right folder
        /// </summary>
        /// <param name="image">full path and name of the image/video</param>
        /// <returns>A DateTime value to be used to create the folder</returns>
        private static DateTime GetFileDate(string image)
        {
            const string wpPattern = "^[W][P]\\w([2][0]|[1][9])[0-9]{6}\\w[0-9]{2}\\w[0-9]{2}\\w[0-9]{2}";
            const string wpPattern2 = "^[W][P]\\w([2][0]|[1][9])[0-9]{6}\\w[0-9]{3}";
            const string androidPattern = "^[I][M][G]\\w([2][0]|[1][9])[0-9]{6}\\w[0-9]{6}";
            const string iosPattern = "(^[2][0]|^[1][9])[0-9]{6}\\w[0-9]{6}";

            DateTime fileDate = GetDateTaken(image);
            string fileName = Path.GetFileName(image);
            if (!hasDateTaken)
            {
                if (Regex.IsMatch(fileName, wpPattern) || Regex.IsMatch(fileName, wpPattern2))
                {
                    string year = fileName.Substring(3, 4);
                    string month = fileName.Substring(7, 2);
                    string day = fileName.Substring(9, 2);
                    fileDate = DateTime.Parse($"{month}/{day}/{year}");
                }
                if (Regex.IsMatch(fileName, androidPattern))
                {
                    string year = fileName.Substring(4, 4);
                    string month = fileName.Substring(8, 2);
                    string day = fileName.Substring(10, 2);
                    fileDate = DateTime.Parse($"{month}/{day}/{year}");

                }
                if (Regex.IsMatch(fileName, iosPattern))
                {
                    string year = fileName.Substring(0, 4);
                    string month = fileName.Substring(4, 2);
                    string day = fileName.Substring(6, 2);
                    fileDate = DateTime.Parse($"{month}/{day}/{year}");

                }

            }
            return fileDate;
        }

        /// <summary>
        /// This method get the date the picture was taken from the image metadata
        /// </summary>
        /// <param name="image">full path and name of the image/video</param>
        /// <returns>A DateTime value to be used to create the folder</returns>
        private static DateTime GetDateTaken(string image)
        {
            DateTime result = File.GetLastWriteTime(image);
            string extension = Path.GetExtension(image).ToLower();
            if (extension == ".jpg" || extension == ".png" || extension == ".bmp")
            {
                ASCIIEncoding encodings = new ASCIIEncoding();
                string dateTaken = string.Empty;
                hasDateTaken = false;
                using (Bitmap img = new Bitmap(image))
                {
                    PropertyItem item = img.PropertyItems[0];

                    if (img.PropertyIdList.Contains(36868))
                    {
                        hasDateTaken = true;
                        item = img.PropertyItems.First(x => x.Id == 36868);
                    }
                    else if (img.PropertyIdList.Contains(36867))
                    {
                        hasDateTaken = true;
                        item = img.PropertyItems.First(x => x.Id == 36867);
                    }
                    else if (img.PropertyIdList.Contains(306))
                    {
                        hasDateTaken = true;
                        item = img.PropertyItems.First(x => x.Id == 306);
                    }
                    else
                    {
                        hasDateTaken = false;
                    }

                    if (hasDateTaken)
                    {
                        dateTaken = encodings.GetString(item.Value);
                        string[] d = dateTaken.Split(' ');
                        DateTime.TryParse(d[0].Replace(':', '/'), out result);
                    }
                }

            }
            return result;
        }

        private static bool Verify(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException("Cannot continue with a NULL as an argument.");
            return Directory.Exists(path);
        }

        /// <summary>
        /// Compare two images
        /// </summary>
        /// <param name="sourceFile">full image path and file name</param>
        /// <param name="destinationFile">full image path and file name</param>
        /// <returns>True indicates that the files are equal.</returns>
        private static bool FilesAreEqual(string sourceFile, string destinationFile)
        {
            FileInfo first = new FileInfo(sourceFile);
            FileInfo second = new FileInfo(destinationFile);
            if (first.Length != second.Length)
                return false;

            int iterations = (int)Math.Ceiling((double)first.Length / BYTES_TO_READ);

            using (FileStream fs1 = first.OpenRead())
            using (FileStream fs2 = second.OpenRead())
            {
                byte[] one = new byte[BYTES_TO_READ];
                byte[] two = new byte[BYTES_TO_READ];

                for (int i = 0; i < iterations; i++)
                {
                    fs1.Read(one, 0, BYTES_TO_READ);
                    fs2.Read(two, 0, BYTES_TO_READ);

                    if (BitConverter.ToInt64(one, 0) != BitConverter.ToInt64(two, 0))
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Updates the UI
        /// </summary>
        /// <param name="statusCode">Code to tell the ui what to do.</param>
        /// <param name="status">Status to display</param>
        /// <param name="filetotal">Number of files copied</param>
        /// <param name="progress">Interface to progress handler</param>
        private static void UpdateProgress(StatusCodeEnum statusCode, string status, int filetotal, IProgress<ProgressStatus> progress)
        {
            if (progress != null)
            {
                ProgressStatus progressStatus = new ProgressStatus
                {
                    FileCount = filetotal,
                    StatusCode = statusCode,
                    Status = status
                };
                progress.Report(progressStatus);

            }

        }
    }
}
