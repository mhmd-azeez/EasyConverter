using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EasyConverter.Shared;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using tusdotnet;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;
using tusdotnet.Stores;

namespace EasyConverter.WebUI
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMediatR(typeof(Startup).Assembly);
            services.AddSingleton<MessageQueueService>();
            services.AddRazorPages()
                    .AddRazorRuntimeCompilation();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IServiceProvider services)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            var mediator = services.GetService<IMediator>();
            var messageQueue = services.GetService<MessageQueueService>();

            var minioStorageProvider = Shared.Storage.MinioStorageProviderFactory.Create();

            app.UseTus(context =>
            {
                return new DefaultTusConfiguration
                {
                    UrlPath = "/files",
                    Store = new Stores.StorageProviderTusStore(minioStorageProvider, Shared.Constants.Buckets.Original),
                    Events = new Events
                    {
                        OnFileCompleteAsync = ctx =>
                        {
                            var id = ctx.FileId;

                            var job = new StartConversionJob
                            {
                                FileId = ctx.FileId
                            };

                            messageQueue.QueueJob(job);

                            return Task.CompletedTask;
                        },
                        OnBeforeCreateAsync = ctx =>
                        {
                            var sourceFileType = ctx.Metadata.ContainsKey("filetype") ?
                                ctx.Metadata["filetype"].GetString(Encoding.UTF8) : null;

                            if (IsValidOriginalType(sourceFileType) == false)
                            {
                                ctx.FailRequest($"Unsupported source filetype: '{sourceFileType}'");
                            }

                            var conversionTarget = ctx.Metadata.ContainsKey("convert-to") ?
                                ctx.Metadata["convert-to"].GetString(Encoding.UTF8) : null;

                            if (IsValidDestinationType(sourceFileType, conversionTarget) == false)
                            {
                                ctx.FailRequest($"Unsupported destination filetype: '{conversionTarget}' from '{sourceFileType}'.");
                            }

                            return Task.CompletedTask;
                        }
                    }
                };
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages();
            });
        }

        private bool IsValidDestinationType(string sourceFileType, string conversionTarget)
        {
            return conversionTarget == "pdf";
        }

        private static bool IsValidOriginalType(string fileType)
        {
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
