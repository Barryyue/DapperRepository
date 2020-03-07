using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace CoreDapperRepository.Web.Infrastructure
{
    public static class ApplicationBuilderExtensions
    {
        public static void ConfigureRequestPipeline(this IApplicationBuilder application, IHostingEnvironment env)
        {
            UseSampleExceptionHandler(application, env);
            UsePageNotFound(application);
            UseBadRequestResult(application);

            UseSampleResponseCompression(application);

            application.UseStaticFiles();

            //application.UseSession();

            UseSampleMvc(application);
        }

        #region Error Handler

        public static void UseSampleExceptionHandler(this IApplicationBuilder application, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                application.UseDeveloperExceptionPage();
            }
            else
            {
                application.UseExceptionHandler("/ErrorPage.htm");
            }

            application.UseExceptionHandler(handler =>
            {
                handler.Run(context =>
                {
                    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
                    if (exception == null)
                        return Task.CompletedTask;

                    try
                    {
                        // log error
                    }
                    finally
                    {
                        throw exception;
                    }
                });
            });
        }

        public static void UsePageNotFound(this IApplicationBuilder application)
        {
            application.UseStatusCodePages(async context =>
            {
                if (context.HttpContext.Response.StatusCode == StatusCodes.Status404NotFound)
                {
                    // get original path and query
                    var originalPath = context.HttpContext.Request.Path;
                    var originalQueryString = context.HttpContext.Request.QueryString;

                    context.HttpContext.Features.Set<IStatusCodeReExecuteFeature>(new StatusCodeReExecuteFeature
                    {
                        OriginalPathBase = context.HttpContext.Request.PathBase.Value,
                        OriginalPath = originalPath.Value,
                        OriginalQueryString = originalQueryString.HasValue ? originalQueryString.Value : null
                    });

                    // get new path
                    context.HttpContext.Request.Path = "/PageNoFound.html";
                    context.HttpContext.Request.QueryString = QueryString.Empty;

                    try
                    {
                        await context.Next(context.HttpContext);
                    }
                    finally
                    {
                        context.HttpContext.Request.QueryString = originalQueryString;
                        context.HttpContext.Request.Path = originalPath;
                        context.HttpContext.Features.Set<IStatusCodeReExecuteFeature>(null);
                    }
                }
            });
        }

        public static void UseBadRequestResult(this IApplicationBuilder application)
        {
            application.UseStatusCodePages(context =>
            {
                //handle 400 (Bad request)
                if (context.HttpContext.Response.StatusCode == StatusCodes.Status400BadRequest)
                {
                    // log something
                }

                return Task.CompletedTask;
            });
        }

        #endregion

        #region Db Context



        #endregion

        #region Common

        public static void UseSampleResponseCompression(this IApplicationBuilder application)
        {
            // whether to use compression (gzip by default)
            application.UseResponseCompression();
        }

        #endregion

        #region Authorization



        #endregion

        #region MVC

        public static void UseSampleMvc(this IApplicationBuilder application)
        {
            application.UseMvc(routes =>
            {
                //areas
                routes.MapRoute(name: "areaRoute", template: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

                routes.MapRoute(
                    name: "default",
                    template: "{controller=Customer}/{action=Index}/{id?}");
            });
        }

        #endregion
    }
}
