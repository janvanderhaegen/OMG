using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Diagnostics;

internal static class ResourceBuilderExtensions
{
    extension<T>(IResourceBuilder<T> builder) where T : IResourceWithEndpoints
    {
        internal IResourceBuilder<T> WithSwaggerUI()
        {
            return builder.WithOpenApiDocs("swagger-ui-docs", "Swagger API Documentation", "swagger");
        }

        internal IResourceBuilder<T> WithScalar()
        {
            return builder.WithOpenApiDocs("scalar-docs", "Scalar API Documentation", "scalar/v1");
        }

        private IResourceBuilder<T> WithOpenApiDocs(
            string name,
            string displayName,
            string openApiUiPath)
        {

            return builder.WithCommand(name, displayName, async (_) =>
            {
                try
                {
                    // Base URL
                    var endpoint = builder.GetEndpoint("https");

                    var url = $"{endpoint.Url}/{openApiUiPath}";

                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

                    return new ExecuteCommandResult { Success = true };
                }
                catch (Exception e)
                {
                    return new ExecuteCommandResult { Success = false, ErrorMessage = e.ToString() };
                }
            }, new CommandOptions
            {

                UpdateState =
                    context => context.ResourceSnapshot.HealthStatus == HealthStatus.Healthy ?
                    ResourceCommandState.Enabled : ResourceCommandState.Disabled,
                IconVariant = IconVariant.Filled,
            });
        }
    }
}