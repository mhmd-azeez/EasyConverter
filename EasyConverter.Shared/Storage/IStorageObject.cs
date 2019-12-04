using System;
using System.Collections.Generic;
using System.IO;

namespace EasyConverter.Shared.Storage
{
    public interface IStorageObject
    {
        string ContentType { get; set; }
        Stream Data { get; set; }
        DateTime LastModified { get; set; }
        Dictionary<string, string> Metadata { get; set; }
        string Name { get; set; }
        long Size { get; set; }
    }
}
