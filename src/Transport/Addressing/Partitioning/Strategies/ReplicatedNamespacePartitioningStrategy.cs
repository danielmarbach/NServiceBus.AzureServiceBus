namespace NServiceBus.AzureServiceBus.Addressing
{
    using System.Collections.Generic;
    using System.Configuration;
    using System.Linq;
    using NServiceBus.Settings;

    public class ReplicatedNamespacePartitioningStrategy : INamespacePartitioningStrategy
    {
        readonly IReadOnlyCollection<string> _connectionstrings;

        public ReplicatedNamespacePartitioningStrategy(ReadOnlySettings settings)
        {
            List<string> connectionstrings;
            if (!settings.TryGet(WellKnownConfigurationKeys.Topology.Addressing.Partitioning.Namespaces, out connectionstrings))
            {
                connectionstrings = new List<string>();
            }

            if (connectionstrings.Count <= 1)
            {
                throw new ConfigurationErrorsException("The 'Replicated' namespace partitioning strategy requires more than one namespace, please configure additional connection strings");
            }
            
            _connectionstrings = connectionstrings;
        }

        public IEnumerable<NamespaceInfo> GetNamespaces(string endpointName, PartitioningIntent partitioningIntent)
        {
            return _connectionstrings.Select(connectionstring => new NamespaceInfo(connectionstring, NamespaceMode.Active));
        }
    }
}