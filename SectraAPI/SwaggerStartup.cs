using System;
using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace HeartProviderAdults.WebService; 

public static class SwaggerStartup {
    /// <summary>
    /// Add swagger generation
    /// </summary>
    public static void AddSwagger(this IServiceCollection services) {
        services.AddSwaggerGen(c => {
            c.SwaggerDoc("v1", new OpenApiInfo {
                Title = "HeartProviderAdults API",
                Version = "v1.0",
                Contact = new OpenApiContact {
                    Name = "Example",
                    Email = "support@example.com"
                },
            });

            var xmlFiles = Directory.GetFiles(AppContext.BaseDirectory, "HeartProviderAdults.*.xml", SearchOption.TopDirectoryOnly);
            foreach (var filePath in xmlFiles) {
                c.IncludeXmlComments(filePath);
            }

            c.CustomOperationIds(apiDesc => apiDesc.TryGetMethodInfo(out MethodInfo methodInfo) ? methodInfo.Name : null);
        });
        services.AddSwaggerGenNewtonsoftSupport(); // explicit opt-in - needs to be placed after AddSwaggerGen()
    }
    
    /// <summary>
    /// Enables endpoints for generating OpenAPI specifications and for viewing/trying the API out.
    /// </summary>
    public static void UseApiDocumentation(this IApplicationBuilder app) {
        // Enable middleware to serve generated Swagger as a JSON endpoint.
        app.UseSwagger(c => {
            c.RouteTemplate = "docs/{documentName}/swagger.json";
        });

        // Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.), specifying the Swagger JSON endpoint.
        app.UseSwaggerUI(c => {
            c.SwaggerEndpoint("v1/swagger.json", "HeartProviderAdults API");
            c.RoutePrefix = "docs";
        });
    }
}

