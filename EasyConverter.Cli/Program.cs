using EasyConverter.LibreOffice;
using System;

namespace EasyConverter.Cli
{
    class Program
    {
        static void Main(string[] args)
        {
            var result = Converter.Convert(@"F:\tusfiles\b7ef9bb526bc49078339331e2e26fa9b", "docx", @"D:\out");
            if (result.TimedOut)
            {
                Console.WriteLine($"Timed out after: {result.Time.TotalMilliseconds:N0} ms.");
            }
            else if (!result.Successful)
            {
                Console.WriteLine($"Unable to convert file: {result.Output}. Took {result.Time.TotalMilliseconds:N0} ms.");
            }
            else
            {
                Console.WriteLine($"Conversion successful: {result.OutputFile}. Took {result.Time.TotalMilliseconds:N0} ms.");
            }
        }
    }
}
