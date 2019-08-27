using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Paden.ImperfectDollop;
using Paden.ImperfectDollop.Broker.RabbitMQ;
using Paden.ImperfectDollop.Broker.Redis;
using Paden.ImperfectDollop.Prometheus;
using Paden.SimpleREST.Data;
using Paden.SimpleREST.TimedLogger;
using Prometheus;

namespace Paden.SimpleREST
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
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);

            services.Replace(ServiceDescriptor.Transient(typeof(ILogger<>), typeof(TimedLogger<>)));

            var config = new ConfigurationBuilder()
                            .AddJsonFile("appsettings.json", false, false)
                            .AddEnvironmentVariables()
                            .Build();

            services.Configure<Settings>(config.GetSection("Settings"));

            services.AddSingleton<IBroker, RabbitMQBroker>(sp =>
            {
                var settings = sp.GetService<IOptions<Settings>>();
                return new RabbitMQBroker(settings.Value.RabbitMQ, sp.GetService<ILogger<RabbitMQBroker>>());
            });
            //services.AddSingleton<IBroker, RedisBroker>(sp =>
            //{
            //    var settings = sp.GetService<IOptions<Settings>>();
            //    return new RedisBroker(settings.Value.Redis, sp.GetService<ILogger<RedisBroker>>());
            //});
            services.AddSingleton<StudentRepository>();
            services.AddSingleton(sp => new RepositoryMetrics<StudentRepository, Student, int>(sp.GetService<StudentRepository>()));
            services.BuildServiceProvider().GetService<RepositoryMetrics<StudentRepository, Student, int>>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseMetricServer();

            app.UseHttpsRedirection();
            app.UseMvc();
        }
    }
}
