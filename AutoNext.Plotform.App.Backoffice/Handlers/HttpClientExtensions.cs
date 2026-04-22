using AutoNext.Plotform.App.Backoffice.Models.Common;

namespace AutoNext.Plotform.App.Backoffice.Handlers
{
    public static class HttpClientExtensions
    {
        public static IHttpClientBuilder AddConfiguredHttpClient<TInterface, TImplementation>(
            this IServiceCollection services,
            string baseUrl,
            ApiGateway config)
            where TInterface : class
            where TImplementation : class, TInterface
        {
            return services.AddHttpClient<TInterface, TImplementation>(client =>
            {
                client.BaseAddress = new Uri(baseUrl);

                if (config?.TimeoutSeconds > 0)
                    client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
            });
        }
    }
}
