namespace EasyConverter.Shared
{
    public enum JobType
    {
        Unknown = 0,
        ConvertDocument = 1,
        NotifyUser = 2,
    }

    public interface IJob
    {
        public string Name { get; }
        public JobType Type { get; }
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
        public string FileName { get; set; }
        public string Name => $"Notify User File '{FileName}' is successfuly converted.";

        public JobType Type => JobType.NotifyUser;
    }
}
