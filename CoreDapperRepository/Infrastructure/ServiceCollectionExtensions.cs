using System;
using System.Reflection;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using CoreDapperRepository.Core;
using CoreDapperRepository.Core.Cache;
using CoreDapperRepository.Core.Configuration;
using CoreDapperRepository.Core.Constants;
using CoreDapperRepository.Core.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Newtonsoft.Json.Serialization;

namespace CoreDapperRepository.Web.Infrastructure
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceProvider ConfigureApplicationServices(this IServiceCollection services,IHostingEnvironment hostingEnvironment,
            IConfiguration configuration)
        {
            services.ConfigureStartupConfig<AppConfig>(configuration.GetSection("App"));

            CommonHelper.DefaultFileProvider = new AppFileProvider(hostingEnvironment.ContentRootPath);

            //AddHttpContextAccessor(services);

            services.AddResponseCompression();

            services.AddOptions();

            //services.AddMemoryCache();

            //services.AddDistributedMemoryCache();

            //AddHttpSession(services);

            AddAntiForgery(services);

            // Authorization

            AddSampleMvc(services);

            var service = AddRegisterServices(services, configuration);

            return service;
        }

        public static TConfig ConfigureStartupConfig<TConfig>(this IServiceCollection services, IConfiguration configuration) where TConfig : class, new()
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            //create instance of config
            var config = new TConfig();

            //bind it to the appropriate section of configuration
            configuration.Bind(config);

            //and register it as a service
            services.AddSingleton(config);

            return config;
        }

        public static void AddHttpContextAccessor(this IServiceCollection services)
        {
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        }

        public static IServiceProvider AddRegisterServices(this IServiceCollection services, IConfiguration configuration)
        {
            //services.AddScoped<ICustomerService, CustomerService>();
            //services.AddScoped<ICustomerRoleService, CustomerRoleService>();
            //services.AddScoped<ICustomerRepository, CustomerRepository>();
            //services.AddScoped<ICustomerRoleRepository, CustomerRoleRepository>();

            // Create the container builder.
            var builder = new ContainerBuilder();

            // Register dependencies, populate the services from
            // the collection, and build the container.
            //
            // Note that Populate is basically a foreach to add things
            // into Autofac that are in the collection. If you register
            // things in Autofac BEFORE Populate then the stuff in the
            // ServiceCollection can override those things; if you register
            // AFTER Populate those registrations can override things
            // in the ServiceCollection. Mix and match as needed.
            builder.Populate(services);

            if (bool.Parse(configuration.GetSection("AppConfig:RedisEnabled").Value))
            {
                builder.RegisterType<RedisConnectionWrapper>()
                    .As<ILocker>()
                    .As<IRedisConnectionWrapper>()
                    .SingleInstance();

                RegisterMore(builder, x =>
                {
                    x.RegisterType<RedisCacheManager>().As<IStaticCacheManager>().InstancePerLifetimeScope();
                });
            }

            switch (configuration.GetSection("AppConfig:CurrentDbTypeName").Value)
            {
                case ConnKeyConstants.Mssql:

                    // Repositories
                    RegisterRepository(builder, ConnKeyConstants.Mssql);

                    // Services
                    RegisterService(builder, ConnKeyConstants.Mssql);

                    /* // we're using mssql currently,but if we wanna get the customer roles from mysql db
                    RegisterMore(builder, (b) =>
                    {
                        b.RegisterType<Data.Repositories.Mysql.Customers.CustomerRoleRepository>()
                        .As<Data.Repositories.BaseInterfaces.ICustomerRoleRepository>()
                        .InstancePerLifetimeScope();

                        b.RegisterType<Services.Mysql.Customers.CustomerRoleService>()
                       .As<Services.BaseInterfaces.ICustomerRoleService>()
                       .InstancePerLifetimeScope();
                    });
                    */

                    break;

                case ConnKeyConstants.Mysql:

                    // Repositories
                    RegisterRepository(builder, ConnKeyConstants.Mysql);

                    // Services
                    RegisterService(builder, ConnKeyConstants.Mysql);

                    break;

                case ConnKeyConstants.Oracle:

                    // configure it by yourself if you wanna use oracle

                    break;
            }

            var container = builder.Build();

            // Create the IServiceProvider based on the container.
            return new AutofacServiceProvider(container);
        }

        #region for auofac dependency

        private static void RegisterRepository(ContainerBuilder builder, string registerDbTypeName)
        {
            string namespacePrefix = $"CoreDapperRepository.Data.Repositories.{registerDbTypeName}";

            builder.RegisterAssemblyTypes(Assembly.Load("CoreDapperRepository.Data"))
                            .Where(x => x.Namespace != null && x.Namespace.StartsWith(namespacePrefix)
                                && x.Name.EndsWith("Repository"))
                            .AsImplementedInterfaces()
                            .InstancePerLifetimeScope();
        }

        private static void RegisterService(ContainerBuilder builder, string registerDbTypeName)
        {
            string namespacePrefix = $"CoreDapperRepository.Services.{registerDbTypeName}";

            builder.RegisterAssemblyTypes(Assembly.Load("CoreDapperRepository.Services"))
                            .Where(x => x.Namespace != null && x.Namespace.StartsWith(namespacePrefix)
                                && x.Name.EndsWith("Service"))
                            .AsImplementedInterfaces()
                            .InstancePerLifetimeScope();
        }

        /// <summary>
        /// 注册更多（比如当前如果使用的MSSQL数据库，那有可能还需要MYSQL数据库的相关操作，这里就可以作为扩展注册）
        /// </summary>
        private static void RegisterMore(ContainerBuilder builder, Action<ContainerBuilder> register = null)
        {
            register?.Invoke(builder);
        }

        #endregion

        public static void AddHttpSession(this IServiceCollection services)
        {
            services.AddSession(option =>
            {
                option.Cookie.Name = ".Sample.Session";
                option.Cookie.HttpOnly = true;
                option.Cookie.SecurePolicy = CookieSecurePolicy.None; // for http request
            });
        }

        public static void AddAntiForgery(this IServiceCollection services)
        {
            services.AddAntiforgery(options =>
            {
                options.Cookie.Name = ".Sample.Antiforgery";
                options.Cookie.SecurePolicy = CookieSecurePolicy.None;
            });
        }

        public static IMvcBuilder AddSampleMvc(this IServiceCollection services)
        {
            var mvcBuilder = services.AddMvc();

            mvcBuilder.SetCompatibilityVersion(CompatibilityVersion.Version_2_1);

            //mvcBuilder.AddCookieTempDataProvider(options =>
            //{
            //    options.Cookie.Name = ".Sample.TempData";
            //    options.Cookie.SecurePolicy = CookieSecurePolicy.None;
            //});

            mvcBuilder.AddJsonOptions(options => options.SerializerSettings.ContractResolver = new DefaultContractResolver());

            return mvcBuilder;
        }
    }
}
