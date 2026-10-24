using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.OpEdSync.Web;

/// <summary>
/// Registers <see cref="IndexHtmlTransformation"/> with the File Transformation plugin, through reflection
/// so that plugin stays optional.
/// </summary>
public sealed class ExoPlayerFixRegistration : IHostedService
{
    private const string TransformationId = "0b8f2a64-5d1e-4c7b-9a3f-6e2d8c1b7f40";

    private readonly ILogger<ExoPlayerFixRegistration> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExoPlayerFixRegistration"/> class.
    /// </summary>
    /// <param name="logger">Logger.</param>
    public ExoPlayerFixRegistration(ILogger<ExoPlayerFixRegistration> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (TryRegister())
        {
            _logger.LogInformation("OpEdSync: ExoPlayer replay fix injected into the web client");
        }
        else
        {
            _logger.LogWarning("OpEdSync: File Transformation plugin not found, the ExoPlayer replay fix (Android app) is not active");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private bool TryRegister()
    {
        try
        {
            var assembly = AssemblyLoadContext.All
                .SelectMany(context => context.Assemblies)
                .FirstOrDefault(a => a.GetName().Name == "Jellyfin.Plugin.FileTransformation");
            var register = assembly?
                .GetType("Jellyfin.Plugin.FileTransformation.PluginInterface")?
                .GetMethod("RegisterTransformation", BindingFlags.Static | BindingFlags.Public);
            if (register is null)
            {
                return false;
            }

            // The parameter is a Newtonsoft JObject, built by reflection to avoid a compile-time reference.
            var payloadType = register.GetParameters()[0].ParameterType;
            var payload = Activator.CreateInstance(payloadType)!;
            var indexer = payloadType.GetProperty("Item", [typeof(string)])!;
            var jValueType = payloadType.Assembly.GetType("Newtonsoft.Json.Linq.JValue")!;

            void Set(string key, string value) =>
                indexer.SetValue(payload, Activator.CreateInstance(jValueType, value), [key]);

            Set("id", TransformationId);
            Set("fileNamePattern", "index.html");
            Set("callbackAssembly", typeof(IndexHtmlTransformation).Assembly.FullName!);
            Set("callbackClass", typeof(IndexHtmlTransformation).FullName!);
            Set("callbackMethod", nameof(IndexHtmlTransformation.Transform));

            register.Invoke(null, [payload]);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpEdSync: registration with File Transformation failed");
            return false;
        }
    }
}
