using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ExampleFormsDataProvider.WebService; 

namespace ExampleFormsDataProvider.WebService;

internal class Startup {
    private readonly IConfiguration _configuration;

    public Startup(IConfiguration configuration) {
        _configuration = configuration;
    }

    public void ConfigureServices(IServiceCollection services) {
        services.AddControllers()
            .AddNewtonsoftJson();

        services.AddSwagger(_configuration); // pass the config
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IServiceProvider services) {
        app.UseApiDocumentation();

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints => {
            endpoints.MapControllers();
        });
    }
}
