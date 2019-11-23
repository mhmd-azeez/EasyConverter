using EasyConverter.LibreOffice;
using System;
using System.Diagnostics;

namespace EasyConverter.Cli
{
    class Program
    {
        static void Main(string[] args)
        {
            var watch = Stopwatch.StartNew();
            Converter.Convert(@"D:\introduction-to-github.pptx", FileType.Pdf, @"D:\out");
            watch.Stop();
            Console.WriteLine($"Done: {watch.ElapsedMilliseconds:N0} ms");
        }
    }
}
