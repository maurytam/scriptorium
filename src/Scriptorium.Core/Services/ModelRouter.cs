using Scriptorium.Core.Ai;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Interfaces;
using Scriptorium.Core.Results;

namespace Scriptorium.Core.Services;

/// <summary>
/// The only place that decides whether a question is answered by the local model or the cloud model.
/// It compares facts and hands a provider back; it never calls one.
/// </summary>
public sealed class ModelRouter
{
    private const string CloudNotAvailableMessage =
        "The cloud model is not available. It is switched off or not set up. Choose Auto or Local to be answered on this computer.";

    private const string PrivateRefusedMessage =
        "This document is private, so it can only be answered on this computer. Choose Auto or Local.";

    private readonly IAIProvider _local;
    private readonly ICloudAIProvider _cloud;
    private readonly RoutingOptions _options;

    public ModelRouter(IAIProvider local, ICloudAIProvider cloud, RoutingOptions options)
    {
        _local = local;
        _cloud = cloud;
        _options = options;
    }

    // The cloud can be used only when it is switched on and it has what it needs to call the service.
    private bool CloudUsable => _options.CloudEnabled && _cloud.IsConfigured;

    public RoutingResult Route(RoutingRequest request)
    {
        if (request.IsPrivate)
        {
            return RoutePrivate(request.Preference);
        }

        return request.Preference switch
        {
            ModelPreference.Local => UseLocal(),
            ModelPreference.Cloud => RouteCloud(),
            _ => RouteAuto(request.ComplexityScore)
        };
    }

    private RoutingResult RoutePrivate(ModelPreference preference) =>
        preference == ModelPreference.Cloud
            ? RoutingResult.Failure(RoutingFailureKind.PrivateDocumentCloudRefused, PrivateRefusedMessage)
            : UseLocal();

    private RoutingResult RouteCloud() =>
        CloudUsable
            ? UseCloud()
            : RoutingResult.Failure(RoutingFailureKind.CloudNotAvailable, CloudNotAvailableMessage);

    private RoutingResult RouteAuto(int complexityScore) =>
        CloudUsable && complexityScore > _options.ComplexityThreshold
            ? UseCloud()
            : UseLocal();

    private RoutingResult UseLocal() => RoutingResult.Success(ModelBackend.Local, _local);

    private RoutingResult UseCloud() => RoutingResult.Success(ModelBackend.Cloud, _cloud);
}
