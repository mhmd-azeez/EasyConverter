using EasyConverter.Shared.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace EasyConverter.WebUI.Jobs
{
    public class NotifyConversionSuccessfulJob
    {
        private readonly ILogger<ConvertDocumentJob> _logger;
        private readonly IStorageProvider _provider;
        private readonly string _sendGridKey;

        public NotifyConversionSuccessfulJob(
            ILogger<ConvertDocumentJob> logger,
            IStorageProvider provider,
            IConfiguration configuration)
        {
            _logger = logger;
            _provider = provider;
            _sendGridKey = configuration["SendGrid:ApiKey"];
        }

        [DisplayName("Notify user that File ({0}) has been completed.")]
        public async Task Run(string fileId)
        {
            var link = await _provider.GetPresignedDownloadLink(
               Shared.Constants.Buckets.Result, fileId, TimeSpan.FromHours(24));

            var metadata = await _provider.GetObjectMetadata(
                Shared.Constants.Buckets.Result, fileId);

            var emailAddress = metadata.Metadata[Shared.Constants.Metadata.EmailAddress];
            if (string.IsNullOrWhiteSpace(emailAddress))
            {
                // TODO: Decide what to do!
                throw new JobFailedException("Email address can't be empty!");
            }

            var client = new SendGridClient(_sendGridKey);

            var message = new SendGridMessage();
            message.SetFrom(new EmailAddress("no-reply@easy-converter.com", "Easy Converter"));
            message.AddTo(emailAddress);
            message.SetTemplateId("d-aad5b0e03ad94172804fbf77fb301d3b");
            message.SetTemplateData(new
            {
                DownloadLink = link
            });

            var response = await client.SendEmailAsync(message);

            if (response.StatusCode != System.Net.HttpStatusCode.Accepted)
            {
                var responseBody = await response.Body.ReadAsStringAsync();
                _logger.LogError("SendGrid Error: {Error}.", responseBody);
                throw new JobFailedException($"SendGrid request failed: {(int)response.StatusCode} - '{responseBody}'");
            }
        }
    }
}
