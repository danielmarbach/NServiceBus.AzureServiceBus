namespace NServiceBus.AzureServiceBus
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Addressing;
    using NServiceBus.Routing;
    using NServiceBus.Routing.MessageDrivenSubscriptions;
    using NServiceBus.Transports;
    using Settings;

    public class StandardTopologySectionManager : ITopologySectionManager
    {
        SettingsHolder settings;
        ITransportPartsContainer container;

        readonly ConcurrentDictionary<Type, TopologySection> subscriptions = new ConcurrentDictionary<Type, TopologySection>();

        public StandardTopologySectionManager(SettingsHolder settings, ITransportPartsContainer container)
        {
            this.settings = settings;
            this.container = container;
        }

        public TopologySection DetermineReceiveResources(string inputQueue)
        {
            var partitioningStrategy = (INamespacePartitioningStrategy)container.Resolve(typeof(INamespacePartitioningStrategy));
            var sanitizationStrategy = (ISanitizationStrategy)container.Resolve(typeof(ISanitizationStrategy));

            var namespaces = partitioningStrategy.GetNamespaces(inputQueue, PartitioningIntent.Receiving).ToArray();

            var inputQueuePath = sanitizationStrategy.Sanitize(inputQueue, EntityType.Queue);
            var entities = namespaces.Select(n => new EntityInfo { Path = inputQueuePath, Type = EntityType.Queue, Namespace = n }).ToList();

            return new TopologySection()
            {
                Namespaces = namespaces,
                Entities = entities.ToArray()
            };
        }

        public TopologySection DetermineResourcesToCreate()
        {
            // computes the topologySectionManager

            var endpointName = settings.EndpointName();

            var partitioningStrategy = (INamespacePartitioningStrategy)container.Resolve(typeof(INamespacePartitioningStrategy));
            var sanitizationStrategy = (ISanitizationStrategy)container.Resolve(typeof(ISanitizationStrategy));
            
            var namespaces = partitioningStrategy.GetNamespaces(endpointName.ToString(), PartitioningIntent.Creating).ToArray();

            var inputQueuePath = sanitizationStrategy.Sanitize(endpointName.ToString(), EntityType.Queue);
            var entities = namespaces.Select(n => new EntityInfo { Path = inputQueuePath, Type = EntityType.Queue, Namespace = n }).ToList();
            
            var topicPath = sanitizationStrategy.Sanitize(endpointName + ".events", EntityType.Topic);
            var topics =
                namespaces.Select(n => new EntityInfo {Path = topicPath, Type = EntityType.Topic, Namespace = n})
                    .ToArray();
            entities.AddRange(topics);

            if (settings.HasExplicitValue<QueueBindings>())
            {
                var queueBindings = settings.Get<QueueBindings>();
                foreach (var n in namespaces)
                {
                    entities.AddRange(queueBindings.ReceivingAddresses.Select(p => new EntityInfo
                    {
                        Path = p,
                        Type = EntityType.Queue,
                        Namespace = n
                    }));
                    
                    // assumed errorq and auditq are in here
                    entities.AddRange(queueBindings.SendingAddresses.Select(p => new EntityInfo
                    {
                        Path = p,
                        Type = EntityType.Queue,
                        Namespace = n
                    }));
                }
            }

            return new TopologySection()
            {
                Namespaces = namespaces,
                Entities = entities.ToArray()
            };
        }

        public TopologySection DeterminePublishDestination(Type eventType)
        {
            var endpointName = settings.EndpointName();

            var partitioningStrategy = (INamespacePartitioningStrategy)container.Resolve(typeof(INamespacePartitioningStrategy));
            var sanitizationStrategy = (ISanitizationStrategy)container.Resolve(typeof(ISanitizationStrategy));

            var namespaces = partitioningStrategy.GetNamespaces(endpointName.ToString(), PartitioningIntent.Sending).ToArray();

            var topicPath = sanitizationStrategy.Sanitize(endpointName + ".events", EntityType.Topic);
            var topics = namespaces.Select(n => new EntityInfo { Path = topicPath, Type = EntityType.Topic, Namespace = n }).ToArray();
            
            return new TopologySection
            {
                Namespaces = namespaces,
                Entities = topics
            };
        }

        public TopologySection DetermineSendDestination(string destination)
        {
            var partitioningStrategy = (INamespacePartitioningStrategy)container.Resolve(typeof(INamespacePartitioningStrategy));
            var sanitizationStrategy = (ISanitizationStrategy)container.Resolve(typeof(ISanitizationStrategy));

            var namespaces = partitioningStrategy.GetNamespaces(destination, PartitioningIntent.Sending).ToArray();

            var inputQueuePath = sanitizationStrategy.Sanitize(destination, EntityType.Queue);
            var inputQueues = namespaces.Select(n => new EntityInfo { Path = inputQueuePath, Type = EntityType.Queue, Namespace = n }).ToArray();

            return new TopologySection
            {
                Namespaces = namespaces,
                Entities = inputQueues
            };
        }

        public TopologySection DetermineResourcesToSubscribeTo(Type eventType)
        {
            if (!subscriptions.ContainsKey(eventType))
            {
                subscriptions[eventType] = BuildSubscriptionHierarchy(eventType);
            }

            return (subscriptions[eventType]);
        }

        TopologySection BuildSubscriptionHierarchy(Type eventType)
        {
            var partitioningStrategy = (INamespacePartitioningStrategy) container.Resolve(typeof(INamespacePartitioningStrategy));
            var endpointName = settings.EndpointName();
            var namespaces = partitioningStrategy.GetNamespaces(endpointName.ToString(), PartitioningIntent.Creating).ToArray();
            var sanitizationStrategy = (ISanitizationStrategy) container.Resolve(typeof(ISanitizationStrategy));

            var topicPaths = DetermineTopicsFor(eventType);

            var subscriptionNameCandidateV6 = endpointName + "." + eventType.Name;
            var subscriptionNameV6 = sanitizationStrategy.Sanitize(subscriptionNameCandidateV6, EntityType.Subscription);
            var subscriptionNameCandidate = endpointName + "." + eventType.FullName;
            var subscriptionName = sanitizationStrategy.Sanitize(subscriptionNameCandidate, EntityType.Subscription);

            var topics = new List<EntityInfo>();
            var subs = new List<SubscriptionInfo>();
            foreach (var topicPath in topicPaths)
            {
                var path = sanitizationStrategy.Sanitize(topicPath, EntityType.Topic);
                topics.AddRange(namespaces.Select(ns => new EntityInfo()
                {
                    Namespace = ns,
                    Type = EntityType.Topic,
                    Path = path,
                }));

                subs.AddRange(namespaces.Select(ns =>
                {
                    var sub = new SubscriptionInfo
                    {
                        Namespace = ns,
                        Type = EntityType.Subscription,
                        Path = subscriptionNameV6,
                        Metadata = new SubscriptionMetadata
                        {
                            Description = endpointName + " subscribed to " + eventType.FullName,
                            SubscriptionNameBasedOnEventWithNamespace = subscriptionName
                        },
                        BrokerSideFilter = new SqlSubscriptionFilter(eventType)
                    };
                    sub.RelationShips.Add(new EntityRelationShipInfo
                    {
                        Source = sub,
                        Target = topics.First(t => t.Namespace == ns),
                        Type = EntityRelationShipType.Subscription
                    });
                    return sub;
                }));
            }
            return new TopologySection
            {
                Entities = subs,
                Namespaces = namespaces
            };
        }

        List<string> DetermineTopicsFor(Type eventType)
        {
            var result = new List<string>();

            // TODO: Hacked a way into this functionality, hope it becomes available from the core
            var publishers = settings.Get<Publishers>();
            var endpointInstances = settings.Get<EndpointInstances>();
            var transportAddresses = settings.Get<TransportAddresses>();

            var subscriptionRouterType = Type.GetType("NServiceBus.SubscriptionRouter, NServiceBus.Core");
            var subscriptionRouter = Activator.CreateInstance(subscriptionRouterType, new object[]
            {
                publishers,
                endpointInstances,
                transportAddresses
            });
            var getAddressesMethod = subscriptionRouterType.GetMethod("GetAddressesForEventType",BindingFlags.Instance | BindingFlags.Public);
            var addresses = ((Task<IEnumerable<string>>) getAddressesMethod.Invoke(subscriptionRouter, new object[]
            {
                eventType
            })).GetAwaiter().GetResult();


            foreach (var address in addresses)
            {
                var path = address + ".events";
                if (!result.Contains(path)) result.Add(path);
            }
            return result;
        }

        public TopologySection DetermineResourcesToUnsubscribeFrom(Type eventtype)
        {
            TopologySection result;

            if (!subscriptions.TryRemove(eventtype, out result))
            {
                result = new TopologySection
                {
                    Entities = new List<SubscriptionInfo>(),
                    Namespaces = new List<NamespaceInfo>()
                };
            }
           
            return result;
        }
    }
}