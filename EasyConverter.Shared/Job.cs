namespace EasyConverter.Shared
{
    public enum JobType
    {
        Unknown = 0,
        ConvertDocument = 1,
        NotifyUser = 2,
        StartConversion = 3,
    }

    public interface IJob
    {
        public string Name { get; }
        public JobType Type { get; }
    }

    public class StartConversionJob : IJob
    {
        public string Name => $"Start conversion of {FileId}";
        public JobType Type => JobType.StartConversion;

        public string FileId { get; set; }
    }

    public class ConvertDocumentJob : IJob
    {
        public string Name { get; set; }
        public string FileId { get; set; }
        public string OriginalExtension { get; set; }
        public string DesiredExtension { get; set; }
        public JobType Type => JobType.ConvertDocument;
    }

    public class NotifyUserJob : IJob
    {
        public bool IsSuccessful { get; set; }
        public string FileId { get; set; }
        public string Name => $"Notify User File '{FileId}' is successfuly converted.";
        public JobType Type => JobType.NotifyUser;
    }
}
