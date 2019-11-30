using System;
using System.Collections.Generic;
using System.Text;

namespace EasyConverter.Shared
{
    public class Helpers
    {
        public static string GetContentTypeFromExtension(string extension)
        {
            if (extension.StartsWith("."))
            {
                extension = extension.Substring(1);
            }

            switch (extension)
            {
                case "pdf":
                    return "application/pdf";
                case "html":
                    return "text/html";

                case "doc":
                    return "application/msword";
                case "docx":
                    return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

                case "xls":
                    return "application/vnd.ms-excel";
                case "xlsx":
                    return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

                case "ppt":
                    return "application/vnd.ms-powerpoint";
                case "pptx":
                    return "application/vnd.openxmlformats-officedocument.presentationml.presentation";

                default:
                    return "application/octet-stream";
            }
        }
    }
}
