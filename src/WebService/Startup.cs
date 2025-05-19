using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace ExampleFormsDataProvider.WebService;

internal class Startup {
    /// <summary>
    /// Called by the runtime. Used to add services to the container.
    /// </summary>
    public void ConfigureServices(IServiceCollection services) {
        services.AddControllers()
            .AddNewtonsoftJson();

        services.AddSwagger();
    }

    /// <summary>
    /// Used to configure the HTTP request pipeline.
    /// </summary>
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
