using System.Net.Http.Headers;
using System.Text;
using Sinch.MessageRouter.Core.Configuration;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Sinch.MessageRouter.Gateway.Configuration;

/// <summary>
/// Configures YARP reverse proxy transforms to inject Sinch API authentication
/// headers and rewrite paths for the Sinch Conversation API.
/// </summary>
public sealed class SinchAuthTransformProvider : ITransformProvider
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SinchAuthTransformProvider> _logger;

    public SinchAuthTransformProvider(IConfiguration configuration, ILogger<SinchAuthTransformProvider> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public void ValidateRoute(TransformRouteValidationContext context)
    {
        // No additional validation needed
    }

    public void ValidateCluster(TransformClusterValidationContext context)
    {
        // No additional validation needed
    }

    public void Apply(TransformBuilderContext context)
    {
        // Only apply transforms to routes targeting the Sinch API cluster
        if (context.Route.ClusterId != "sinch-api")
        {
            return;
        }

        var sinchOptions = _configuration.GetSection(SinchOptions.SectionName).Get<SinchOptions>();
        if (sinchOptions is null)
        {
            _logger.LogWarning("Sinch options not configured; YARP auth transform will be skipped.");
            return;
        }

        context.AddRequestTransform(transformContext =>
        {
            // Inject Basic auth header using Sinch access key credentials
            if (!string.IsNullOrEmpty(sinchOptions.AccessKeyId) &&
                !string.IsNullOrEmpty(sinchOptions.AccessKeySecret))
            {
                var credentials = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{sinchOptions.AccessKeyId}:{sinchOptions.AccessKeySecret}"));
                transformContext.ProxyRequest.Headers.Authorization =
                    new AuthenticationHeaderValue("Basic", credentials);
            }

            // Replace the {ProjectId} placeholder in the path with the actual project ID
            if (!string.IsNullOrEmpty(sinchOptions.ProjectId))
            {
                var uri = transformContext.ProxyRequest.RequestUri;
                if (uri is not null)
                {
                    var path = uri.AbsolutePath.Replace("{ProjectId}", sinchOptions.ProjectId);
                    transformContext.ProxyRequest.RequestUri = new UriBuilder(uri)
                    {
                        Path = path
                    }.Uri;
                }
            }

            return ValueTask.CompletedTask;
        });
    }
}

/// <summary>
/// Extension methods for registering YARP configuration.
/// </summary>
public static class YarpConfigExtensions
{
    /// <summary>
    /// Registers the Sinch auth transform provider with the YARP reverse proxy.
    /// </summary>
    public static IReverseProxyBuilder AddSinchTransforms(this IReverseProxyBuilder builder)
    {
        builder.Services.AddSingleton<ITransformProvider, SinchAuthTransformProvider>();
        return builder;
    }
}
