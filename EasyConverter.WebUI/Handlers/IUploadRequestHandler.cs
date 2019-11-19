using System.Threading.Tasks;
using tusdotnet.Models.Configuration;

namespace EasyConverter.WebUI.Handlers
{
    public interface IUploadRequestHandler
    {
        Task<bool> CanHandle(CreateCompleteContext context);
        Task HandleBeforeCreate(BeforeCreateContext context);
        Task HandleFileComplete(FileCompleteContext context);
    }

    public class DocumentUploadRequestHandler : IUploadRequestHandler
    {
        public Task HandleCreateComplete(CreateCompleteContext context)
        {
            return Task.CompletedTask;
        }

        public Task HandleBeforeCreate(BeforeCreateContext context)
        {
            return Task.CompletedTask;
        }

        public Task HandleFileComplete(FileCompleteContext context)
        {
            return Task.CompletedTask;
        }

        private static readonly Task<bool> _trueTask = Task.FromResult(true);
        private static readonly Task<bool> _falseTask = Task.FromResult(false);

        public Task<bool> CanHandle(CreateCompleteContext context)
        {
            return _trueTask;
        }
    }
}
