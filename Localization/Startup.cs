using _999Space.BAL.API.CurrencyExchange;
using _999Space.BAL.API.TwilioMessaging;
using _999Space.BAL.API.YelpApi;
using _999Space.BAL.API.YellowPages;
using _999Space.BAL.BlobStorage;
using _999Space.BAL.Cookies;
using _999Space.BAL.Hubs;
using _999Space.BAL.Mapper;
using _999Space.BAL.ML.MLInterface;
using _999Space.BAL.ML.MLService;
using _999Space.BAL.ServiceInterfaces;
using _999Space.BAL.Services;
using _999Space.BAL.Session;
using _999Space.Common.ConfigurationModel;
using _999Space.DAL.DataModels;
using _999Space.DAL.Interfaces;
using _999Space.Payment.Interface;
using _999Space.Payment.Service;
using AutoMapper;
using Hangfire.MemoryStorage;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Stripe;
using System;
using System.Globalization;
using System.Threading.Tasks;
using SmartBreadcrumbs.Extensions;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Hangfire;
using Hangfire.SqlServer;

namespace _999Space
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            StripeConfiguration.ApiKey = Configuration.GetValue<string>("Stripe:SecretKey");
            StripeConfiguration.MaxNetworkRetries = 2;

        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors();

            
            services.AddControllersWithViews();

            services.AddMvc()
                   .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
                    .AddDataAnnotationsLocalization(options =>
                    {
                        options.DataAnnotationLocalizerProvider = (type, factory) =>
                            factory.Create(typeof(SharedResource));
                    });

            services.Configure<RequestLocalizationOptions>(options =>
            {
                var supportedCultures = new[]
                {
                     new CultureInfo("en-US"),
                     new CultureInfo("zh-Hans"),
                     new CultureInfo("zh-Hant"),
                     new CultureInfo("de"),
                     new CultureInfo("fr"),
                     new CultureInfo("es"),
                     new CultureInfo("ja")
                };
                options.DefaultRequestCulture = new RequestCulture(culture: "en-US", uiCulture: "en-US");
                options.SupportedCultures = supportedCultures;
                options.SupportedUICultures = supportedCultures;
            });

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();

            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }
            app.UseStaticFiles();

            var supportedCultures = new[] { "en-US", "zh-Hans", "zh-Hant", "de", "fr", "es", "ja" };
            var localizationOptions = new RequestLocalizationOptions().SetDefaultCulture(supportedCultures[0])
                .AddSupportedCultures(supportedCultures)
                .AddSupportedUICultures(supportedCultures);

            app.UseRequestLocalization(localizationOptions);

            app.UseRouting();
            app.UseCors(option => option.AllowAnyOrigin().AllowAnyMethod());
            app.UseSession();
            app.UseAuthentication();
            app.UseAuthorization();





            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
                endpoints.MapHub<MessageHub>("/messages");

                //endpoints.MapHub<MessageHub>("/messageHub");
            });


            app.Use(async (context, next) =>
            {
                var hubContext = context.RequestServices
                                        .GetRequiredService<IHubContext<MessageHub>>();
                //...

                if (next != null)
                {
                    await next.Invoke();
                }
            });

          
        }
    }
}


