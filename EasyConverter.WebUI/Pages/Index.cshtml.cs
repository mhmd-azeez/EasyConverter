using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using EasyConverter.Shared.Storage;
using Hangfire;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace EasyConverter.WebUI.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly IStorageProvider _storageProvider;
        private readonly IBackgroundJobClient _backgroundJobs;

        public IndexModel(
            ILogger<IndexModel> logger,
            IStorageProvider storageProvider,
            IBackgroundJobClient backgroundJobs)
        {
            _logger = logger;
            _storageProvider = storageProvider;
            _backgroundJobs = backgroundJobs;
        }

        public void OnGet()
        {

        }

        public string Message { get; set; }

        [Display(Name = "Target type")]
        [BindProperty, Required, StringLength(3)]
        public string TargetType { get; set; }

        [Display(Name = "Email")]
        [BindProperty, Required, EmailAddress]
        public string EmailAddress { get; set; }

        [BindProperty, Required]
        [Display(Name = "File")]
        public IFormFile UploadedFile { get; set; }

        public async Task<IActionResult> OnPost()
        {
            if (!IsValidOriginalType(UploadedFile.ContentType))
            {
                Message = "Unsupported source content type.";
                return Page();
            }

            if (!IsValidTargetType(UploadedFile.ContentType, TargetType))
            {
                Message = "Unsupported target type.";
                return Page();
            }

            var id = Guid.NewGuid().ToString("N");

            await _storageProvider.UploadObject(UploadedFile.OpenReadStream(),
                    EasyConverter.Shared.Constants.Buckets.Original,
                    id,
                    UploadedFile.ContentType ?? "application/octet-stream",
                    new Dictionary<string, string>
                    {
                        { EasyConverter.Shared.Constants.Metadata.ConvertTo, TargetType },
                        { EasyConverter.Shared.Constants.Metadata.EmailAddress, EmailAddress },
                        { EasyConverter.Shared.Constants.Metadata.FileType, UploadedFile.FileName.Split(".").LastOrDefault() ?? string.Empty }
                    });

            _backgroundJobs.Enqueue<Jobs.ConvertDocumentJob>(job => job.Run(id));

            return RedirectToPage(pageName: "conversion", routeValues: new
            {
                id = id
            });
        }

        private bool IsValidTargetType(string sourceType, string targetType)
        {
            if (string.IsNullOrWhiteSpace(targetType))
                return false;

            // TODO: Implement this
            return targetType == "pdf";
        }

        private static bool IsValidOriginalType(string fileType)
        {
            return true;
            var validSourceTypes = new string[]
            {
                 "application/pdf", // .pdf
                 "application/msword", // .doc
                 "application/vnd.openxmlformats-officedocument.wordprocessingml.document", // .docx
                 "application/vnd.ms-excel", // .xls
                 "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", // .xlsx
                 "application/vnd.ms-powerpoint", // .ppt
                 "application/vnd.openxmlformats-officedocument.presentationml.presentation", // pptx
                 "application/vnd.oasis.opendocument.text", // .odt
                 "application/vnd.oasis.opendocument.spreadsheet", // .ods
                 "application/vnd.oasis.opendocument.presentation", // .odp
            };

            return validSourceTypes.Contains(fileType);
        }
    }
}
