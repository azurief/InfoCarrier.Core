namespace InfoCarrier.Core.Client
{
    using Infrastructure.Internal;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors;
    using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors.Internal;
    using Microsoft.EntityFrameworkCore.Storage;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Query.ExpressionVisitors.Internal;
    using Query.Internal;
    using Storage.Internal;
    using ValueGeneration.Internal;

    public static class InfoCarrierServiceCollectionExtensions
    {
        public static IServiceCollection AddEntityFrameworkInfoCarrierBackend(this IServiceCollection services)
        {
            services.AddEntityFramework();

            services.Replace(
                new ServiceDescriptor(
                    typeof(IMemberAccessBindingExpressionVisitorFactory),
                    typeof(InfoCarrierMemberAccessBindingExpressionVisitorFactory),
                    ServiceLifetime.Scoped));

            services.Replace(
                new ServiceDescriptor(
                    typeof(IProjectionExpressionVisitorFactory),
                    typeof(InfoCarrierProjectionExpressionVisitorFactory),
                    ServiceLifetime.Scoped));

            services.TryAddEnumerable(ServiceDescriptor
                .Singleton<IDatabaseProvider, DatabaseProvider<InfoCarrierDatabaseProviderServices, InfoCarrierOptionsExtension>>());

            services.TryAdd(new ServiceCollection()
                .AddSingleton<InfoCarrierValueGeneratorCache>()
                .AddSingleton<InfoCarrierModelSource>()
                .AddScoped<InfoCarrierValueGeneratorSelector>()
                .AddScoped<InfoCarrierDatabaseProviderServices>()
                .AddScoped<IInfoCarrierDatabase, InfoCarrierDatabase>()
                .AddScoped<InfoCarrierTransactionManager>()
                .AddScoped<InfoCarrierDatabaseCreator>()
                .AddQuery());

            return services;
        }

        private static IServiceCollection AddQuery(this IServiceCollection serviceCollection)
            => serviceCollection
                .AddScoped<InfoCarrierQueryCompilationContextFactory>()
                .AddScoped<InfoCarrierQueryContextFactory>()
                .AddScoped<InfoCarrierQueryModelVisitorFactory>()
                .AddScoped<InfoCarrierEntityQueryableExpressionVisitorFactory>();
    }
}