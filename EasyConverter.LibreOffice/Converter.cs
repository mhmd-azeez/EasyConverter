using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace EasyConverter.LibreOffice
{
    public class LibreOfficeException : Exception
    {
        public int ExitCode { get; }
        public bool TerminatedPrematurely { get; }
        public LibreOfficeException(int exitCode = 0, bool terminated = false)
        {
            ExitCode = exitCode;
            TerminatedPrematurely = terminated;
        }

        public LibreOfficeException(string message, int exitCode = 0, bool terminated = false) : base(message)
        {
            ExitCode = exitCode;
            TerminatedPrematurely = terminated;
        }

        public LibreOfficeException(string message, Exception innerException, int exitCode = 0, bool terminated = false) : base(message, innerException)
        {
            ExitCode = exitCode;
            TerminatedPrematurely = terminated;
        }
    }

    public class ConversionResult
    {
        public static ConversionResult CreateTimedOut(string output, TimeSpan time)
        {
            return new ConversionResult(false, null, -1, true, output, time);
        }

        public static ConversionResult CreateSucessful(string outputFile, string output, TimeSpan time)
        {
            return new ConversionResult(true, outputFile, 0, false, output, time);
        }

        public static ConversionResult CreateError(int exitCode, string output, TimeSpan time)
        {
            return new ConversionResult(false, null, exitCode, false, output, time);
        }

        public ConversionResult(bool suceeded, string outputFile, int exitCode, bool timedOut, string output, TimeSpan time)
        {
            Successful = suceeded;
            OutputFile = outputFile;
            ExitCode = exitCode;
            Output = output;
            TimedOut = timedOut;
            Time = time;
        }

        public bool Successful { get; }
        public string OutputFile { get; }
        public int ExitCode { get; }
        public string Output { get; }
        public bool TimedOut { get; }

        public TimeSpan Time { get; set; }
    }

    public static class Converter
    {
        private const int TimeOut = 60 * 1000;

        public static ConversionResult Convert(
            string inputFile,
            string ouputExtension,
            string outputFolder
            )
        {
            var timer = Stopwatch.StartNew();

            var guid = Guid.NewGuid();
            var convertToParam = $"--convert-to {ouputExtension}";
            var outputFolderParam = $"--outdir {WrapInQuotes(UnifySlashes(outputFolder))}";
            var userInstallationFolder = Path.Combine(Path.GetTempPath(), guid.ToString("N"));
            var userInstallationParam = $"file:///{userInstallationFolder.Replace('\\', '/')}";
            var envParam = WrapInQuotes($"-env:UserInstallation={UnifySlashes(userInstallationParam)}");
            var silentParams = "--headless --nofirststartwizard";
            var inputFileParam = WrapInQuotes(UnifySlashes(inputFile));

            var process = new Process();
            var builder = new StringBuilder();
            string output;

            try
            {
                process.StartInfo.FileName = GetExePath();
                process.StartInfo.Arguments = $"{convertToParam} {outputFolderParam} {inputFileParam} {envParam} {silentParams}";

                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                process.EnableRaisingEvents = false;
                process.OutputDataReceived += (sender, eventArgs) => builder.AppendLine(eventArgs.Data);
                process.ErrorDataReceived += (sender, eventArgs) => builder.AppendLine(eventArgs.Data);

                Directory.CreateDirectory(userInstallationFolder);
                Directory.CreateDirectory(outputFolder);

                timer.Start();

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                var processExited = process.WaitForExit(TimeOut);
                output = builder.ToString();

                if (!processExited)
                {
                    timer.Stop();
                    // Kill the process if it hasn't finished in the specified time
                    process.Kill();
                    return ConversionResult.CreateTimedOut(builder.ToString(), timer.Elapsed);
                }
                else if (process.ExitCode != 0 || output.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    timer.Stop();
                    return ConversionResult.CreateError(process.ExitCode, builder.ToString(), timer.Elapsed);
                }
            }
            finally
            {
                Directory.Delete(userInstallationFolder, true);
                process.Close();
            }

            timer.Stop();
            return ConversionResult.CreateSucessful(GetFileNameFromOutput(output), output, timer.Elapsed);
        }

        private static void WriteDataToConsole(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine(e.Data);
        }

        private static string UnifySlashes(string text)
        {
            return text.Replace('\\', '/');
        }

        private static string WrapInQuotes(string text)
        {
            if (text.StartsWith("\"") == false)
                text = "\"" + text;

            if (text.EndsWith("\"") == false)
                text += "\"";

            return text;
        }

        private static string GetFileNameFromOutput(string output)
        {
            // sample output:
            // {convert D:\file.pptx -> D:\out\file.pdf using filter : impress_pdf_Export
            // Overwriting: D:\out\file.pdf
            // }
            var arrowIndex = output.IndexOf("->") + 2; // the + 2 is to take the arrow into account also
            var usingFilterIndex = output.IndexOf("using filter", arrowIndex);

            return output.Substring(arrowIndex, usingFilterIndex - arrowIndex).Trim();
        }

        private static string GetExePath()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return @"C:\Program Files\LibreOffice\program\soffice.exe";
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
