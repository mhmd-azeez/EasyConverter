using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace EasyConverter.LibreOffice
{
    public static class Converter
    {
        private static object _extensions;

        public static void Convert(
            string inputFile,
            FileType outputType,
            string outputFolder
            )
        {
            var guid = Guid.NewGuid();
            var convertToParam = $"--convert-to {GetWriterName(outputType)}";
            var outputFolderParam = $"--outdir {WrapInQuotes(outputFolder)}";
            var userInstallationFolder = Path.Combine(Path.GetTempPath(), guid.ToString("N"));
            var userInstallationParam = $"file:///{userInstallationFolder.Replace('\\', '/')}";
            var envParam = WrapInQuotes($"-env:UserInstallation={userInstallationParam}");
            var silentParams = "--headless --nofirststartwizard";

            var process = new Process();

            process.StartInfo.FileName = Path.Combine(GetInstallationPath(), "soffice.bin");
            process.StartInfo.Arguments = $"{convertToParam} {outputFolderParam} {WrapInQuotes(inputFile)} {envParam} {silentParams}";

            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.LoadUserProfile = false;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
            process.StartInfo.ErrorDialog = false;

            Directory.CreateDirectory(userInstallationFolder);
            Directory.CreateDirectory(outputFolder);

            process.Start();
            process.WaitForExit(25_000);

            // Kill the process if it hasn't finished in the specified time
            process.Kill();
            process.Dispose();

            var fileName = inputFile
                .Split('/', '\\')
                .Last()
                .Split('.')
                .First();

            //var outputExtension = _extensions[outputType];

            Directory.Delete(userInstallationFolder, true);
        }

        private static void WriteDataToConsole(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine(e.Data);
        }

        private static string WrapInQuotes(string text)
        {
            if (text.StartsWith("\"") == false)
                text = "\"" + text;

            if (text.EndsWith("\"") == false)
                text += "\"";

            return text;
        }

        private static string GetWriterName(FileType fileType)
        {
            switch (fileType)
            {
                case FileType.Pdf:
                    return "pdf";

                case FileType.Word2007:
                    break;
                case FileType.Word2003:
                    break;
                case FileType.PowerPoint2007:
                    break;
                case FileType.PowerPoint2003:
                    break;
                case FileType.Excel2007:
                    break;
                case FileType.Excel2003:
                    break;
                default:
                    break;
            }

            throw new ArgumentOutOfRangeException(nameof(fileType));
        }

        private static string GetInstallationPath()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return @"C:\Program Files\LibreOffice\program";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return @"/usr/bin/soffice";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return @"/Applications/LibreOffice.app/Contents/MacOS/soffice";
            }
            else
            {
                throw new PlatformNotSupportedException();
            }
        }
    }
}
