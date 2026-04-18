using System.Text.RegularExpressions;
using Yarp.ReverseProxy.Configuration;

namespace AutoNext.Plotform.App.Backoffice.Gateway
{
    public class YarpEnvironmentVariablesConfigFilter : IProxyConfigFilter
    {
        private static readonly Regex _envPattern = new(@"\${([^}]+)}", RegexOptions.Compiled);

        public ValueTask<ClusterConfig> ConfigureClusterAsync(ClusterConfig cluster, CancellationToken cancellationToken)
        {
            if (cluster.Destinations == null)
                return new ValueTask<ClusterConfig>(cluster);

            var newDestinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase);

            foreach (var dest in cluster.Destinations)
            {
                var originalAddress = dest.Value.Address;
                if (_envPattern.IsMatch(originalAddress))
                {
                    var match = _envPattern.Match(originalAddress);
                    var envVarName = match.Groups[1].Value;
                    var resolvedAddress = Environment.GetEnvironmentVariable(envVarName);

                    if (string.IsNullOrWhiteSpace(resolvedAddress))
                    {
                        throw new InvalidOperationException(
                            $"Environment variable '{envVarName}' not found for destination '{dest.Key}'");
                    }

                    var modifiedDest = dest.Value with { Address = resolvedAddress };
                    newDestinations.Add(dest.Key, modifiedDest);
                }
                else
                {
                    newDestinations.Add(dest.Key, dest.Value);
                }
            }

            return new ValueTask<ClusterConfig>(cluster with { Destinations = newDestinations });
        }

        public ValueTask<RouteConfig> ConfigureRouteAsync(RouteConfig route, ClusterConfig? cluster, CancellationToken cancellationToken)
        {
            return new ValueTask<RouteConfig>(route);
        }
    }
}
