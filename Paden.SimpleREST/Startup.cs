﻿using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Paden.ImperfectDollop;
using Paden.ImperfectDollop.Broker.RabbitMQ;
using Paden.ImperfectDollop.Broker.Redis;
using Paden.SimpleREST.Data;
using Paden.SimpleREST.Prometheus;
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

            var config = new ConfigurationBuilder()
                            .AddJsonFile("appsettings.json", false, false)
                            .AddEnvironmentVariables()
                            .Build();

            services.Configure<Settings>(config.GetSection("Settings"));

            services.AddSingleton<IBroker, RabbitMQBroker>(sp =>
            {
                var settings = sp.GetService<IOptions<Settings>>();
                return new RabbitMQBroker(settings.Value.RabbitMQ);
            });
            //services.AddSingleton<IBroker, RedisBroker>(sp =>
            //{
            //    var settings = sp.GetService<IOptions<Settings>>();
            //    return new RedisBroker(settings.Value.Redis);
            //});
            services.AddSingleton<StudentRepository>();
            services.AddSingleton<StudentRepositoryMetrics>();

            services.BuildServiceProvider().GetService<StudentRepositoryMetrics>();
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
