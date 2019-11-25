namespace EasyConverter.Shared
{
    public enum JobType
    {
        Unknown = 0,
        ConvertDocument = 1,
    }

    public interface IJob
    {
        public string Name { get; }
        public string FileId { get; }
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
}
