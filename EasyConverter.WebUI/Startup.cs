using System;
using System.Linq;
using EasyConverter.Shared;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Hangfire;
using Hangfire.PostgreSql;
using EasyConverter.Shared.Storage;
using EasyConverter.WebUI.Helpers;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

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
            services.AddHangfire(configuration => configuration
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(Configuration.GetConnectionString("HangfireConnection"))
                );

            services.AddHangfireServer();

            services.AddMediatR(typeof(Startup).Assembly);
            services.AddSingleton<MessageQueueService>();
            services.AddRazorPages()
                    .AddRazorRuntimeCompilation()
                    .AddRazorPagesOptions(options =>
                    {
                        options.Conventions.Add(new PageRouteTransformerConvention(new SlugifyParameterTransformer()));
                    });

            services.AddRouting(options =>
            {
                options.LowercaseUrls = true;
                options.ConstraintMap["slugify"] = typeof(SlugifyParameterTransformer);
            });

            services.AddSingleton<IStorageProvider, MinioStorageProvider>(
                (provider) => MinioStorageProviderFactory.Create(provider.GetService<IConfiguration>()));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IServiceProvider services)
        {
            var logger = services.GetService<ILogger<Startup>>();
            logger.LogInformation("Hello World! Current Date is {CurrentDate}", DateTime.UtcNow);

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
               // app.UseHsts();
            }

           // app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseSerilogRequestLogging();

            app.UseHangfireDashboard();

            app.UseRouting();

            app.UseAuthorization();

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
