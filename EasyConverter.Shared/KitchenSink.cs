using System;
using System.Collections.Generic;
using System.Text;

namespace EasyConverter.Shared
{
    public class KitchenSink
    {
        public static string GetContentTypeFromExtension(string extension)
        {
            if (extension.StartsWith("."))
            {
                extension = extension.Substring(1);
            }

            return extension switch
            {
                "pdf" => "application/pdf",
                "doc" => "application/msword",
                "docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "xls" => "application/vnd.ms-excel",
                "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "ppt" => "application/vnd.ms-powerpoint",
                "pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                "odt" => "application/vnd.oasis.opendocument.text",
                "ods" => "application/vnd.oasis.opendocument.spreadsheet",
                "odp" => "application/vnd.oasis.opendocument.presentation",
                _ => throw new IndexOutOfRangeException(),
            };
        }
    }
}
