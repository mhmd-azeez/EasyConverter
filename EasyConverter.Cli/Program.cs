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
            Converter.Convert(@"D:\in\2.docx", FileType.Pdf, @"D:\out");
            watch.Stop();
            Console.WriteLine($"Done: {watch.ElapsedMilliseconds:N0} ms");
        }
    }
}
