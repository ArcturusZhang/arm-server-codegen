using Asp.Versioning;
using Asp.Versioning.Conventions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using System.Reflection;

namespace AzureSqlVersioningDemo.Infrastructure;

/// <summary>
/// A versioning convention that implements Azure SQL-style version fallback.
/// 
/// At startup, this convention scans all controllers and their actions. For each route
/// template + HTTP method combination, it finds which API versions are explicitly supported.
/// If a newer version doesn't have an action that an older version does (same route + method),
/// the older controller's action is registered to also handle the newer version.
/// 
/// This eliminates the need to duplicate unchanged actions across version controllers.
/// 
/// Example: If V20251101 has DELETE but V20251201 doesn't, the convention registers
/// V20251101's DELETE to also handle api-version "2025-12-01", while GET and PUT
/// remain mapped only to "2025-11-01" (since V20251201 has its own GET and PUT).
/// 
/// Rules:
/// - Stable versions only fall back to stable versions.
/// - Preview versions can fall back to both preview and stable versions.
/// </summary>
public class VersionFallbackConvention : IControllerConvention
{
    private Dictionary<string, ControllerFallbackInfo>? _fallbackMap;
    private readonly object _lock = new();

    /// <summary>
    /// Called by the versioning library for each controller during API version discovery.
    /// On the first call, scans all controllers to build the fallback map.
    /// Returns true for controllers that need additional fallback versions.
    /// </summary>
    public bool Apply(IControllerConventionBuilder builder, ControllerModel model)
    {
        lock (_lock)
        {
            _fallbackMap ??= BuildFallbackMap(model.Application!);
        }

        var key = model.ControllerType.FullName ?? model.ControllerName;
        if (!_fallbackMap.TryGetValue(key, out var info))
            return false; // No fallback needed — use normal attribute discovery

        // Declare all versions this controller handles (original + fallback)
        foreach (var v in info.AllVersions)
            builder.HasApiVersion(v);

        // Map each action to its specific versions to avoid ambiguous matches.
        // Actions that provide fallback get the additional version(s);
        // actions that DON'T provide fallback only get the original version(s).
        foreach (var action in model.Actions)
        {
            if (info.ActionVersionMap.TryGetValue(action.ActionMethod, out var versions))
            {
                var actionBuilder = builder.Action(action.ActionMethod);
                foreach (var v in versions)
                    actionBuilder.MapToApiVersion(v);
            }
        }

        return true; // We handled this controller's version discovery
    }

    /// <summary>
    /// Scans all controllers and builds per-action version mapping for controllers
    /// that need to handle additional (fallback) versions.
    /// </summary>
    private static Dictionary<string, ControllerFallbackInfo> BuildFallbackMap(ApplicationModel application)
    {
        // Step 1: Collect controller info
        var controllerInfos = new List<ControllerVersionInfo>();
        foreach (var controller in application.Controllers)
        {
            var versions = controller.Attributes
                .OfType<ApiVersionAttribute>()
                .SelectMany(a => a.Versions)
                .ToList();

            if (versions.Count == 0) continue;

            var routeTemplate = controller.Selectors
                .Select(s => s.AttributeRouteModel?.Template)
                .FirstOrDefault(t => t != null);

            if (routeTemplate == null) continue;

            controllerInfos.Add(new ControllerVersionInfo
            {
                Controller = controller,
                OriginalVersions = versions,
                RouteTemplate = NormalizeRouteTemplate(routeTemplate)
            });
        }

        // Step 2: Group by route template and build per-action fallback info
        var result = new Dictionary<string, ControllerFallbackInfo>();

        foreach (var group in controllerInfos.GroupBy(c => c.RouteTemplate, StringComparer.OrdinalIgnoreCase))
        {
            var controllers = group.OrderBy(c => c.OriginalVersions.First()).ToList();
            var allVersions = controllers.SelectMany(c => c.OriginalVersions).Distinct().OrderBy(v => v).ToList();

            foreach (var version in allVersions)
            {
                var isPreview = version.Status != null;

                // Find the controller that explicitly handles this version
                var exactController = controllers.FirstOrDefault(c => c.OriginalVersions.Contains(version));

                // Action signatures already covered for this version (HTTP method + route suffix)
                var coveredActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (exactController != null)
                    foreach (var action in exactController.Controller.Actions)
                        foreach (var sig in GetActionSignatures(action))
                            coveredActions.Add(sig);

                // Find older controllers that can provide missing actions
                var olderControllers = controllers
                    .Where(c => c.OriginalVersions.All(v => v < version))
                    .Where(c => !isPreview ? c.OriginalVersions.All(v => v.Status == null) : true)
                    .OrderByDescending(c => c.OriginalVersions.Max())
                    .ToList();

                foreach (var older in olderControllers)
                {
                    foreach (var action in older.Controller.Actions)
                    {
                        var signatures = GetActionSignatures(action).ToList();
                        bool actionNeeded = signatures.Any(s => !coveredActions.Contains(s));

                        if (actionNeeded)
                        {
                            var ctrlKey = older.Controller.ControllerType.FullName
                                          ?? older.Controller.ControllerName;

                            if (!result.TryGetValue(ctrlKey, out var info))
                            {
                                info = new ControllerFallbackInfo
                                {
                                    AllVersions = new HashSet<ApiVersion>(older.OriginalVersions),
                                    ActionVersionMap = new Dictionary<MethodInfo, HashSet<ApiVersion>>()
                                };
                                // Initialize ALL actions with their original versions
                                foreach (var a in older.Controller.Actions)
                                    info.ActionVersionMap[a.ActionMethod] = new HashSet<ApiVersion>(older.OriginalVersions);

                                result[ctrlKey] = info;
                            }

                            // Add the newer version to the controller's version set
                            info.AllVersions.Add(version);

                            // Add the newer version only to THIS action (not others)
                            if (info.ActionVersionMap.TryGetValue(action.ActionMethod, out var actionVersions))
                                actionVersions.Add(version);

                            // Mark action signatures as now covered
                            foreach (var s in signatures)
                                coveredActions.Add(s);
                        }
                    }
                }
            }
        }

        return result;
    }

    private static string NormalizeRouteTemplate(string template)
    {
        return System.Text.RegularExpressions.Regex.Replace(template, @"\{[^}]+\}", "{}");
    }

    private static IEnumerable<string> GetActionSignatures(ActionModel action)
    {
        var routeSuffix = action.Selectors
            .Select(s => s.AttributeRouteModel?.Template)
            .FirstOrDefault() ?? "";

        return GetHttpMethods(action).Select(m => $"{m}:{routeSuffix}");
    }

    private static IEnumerable<string> GetHttpMethods(ActionModel action)
    {
        var methods = new List<string>();
        foreach (var selector in action.Selectors)
            foreach (var constraint in selector.ActionConstraints.OfType<HttpMethodActionConstraint>())
                methods.AddRange(constraint.HttpMethods);

        foreach (var attr in action.Attributes)
        {
            if (attr is HttpGetAttribute) methods.Add("GET");
            else if (attr is HttpPostAttribute) methods.Add("POST");
            else if (attr is HttpPutAttribute) methods.Add("PUT");
            else if (attr is HttpDeleteAttribute) methods.Add("DELETE");
            else if (attr is HttpPatchAttribute) methods.Add("PATCH");
        }

        return methods.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private class ControllerVersionInfo
    {
        public required ControllerModel Controller { get; init; }
        public required List<ApiVersion> OriginalVersions { get; init; }
        public required string RouteTemplate { get; init; }
    }

    private class ControllerFallbackInfo
    {
        public required HashSet<ApiVersion> AllVersions { get; init; }
        public required Dictionary<MethodInfo, HashSet<ApiVersion>> ActionVersionMap { get; init; }
    }
}
