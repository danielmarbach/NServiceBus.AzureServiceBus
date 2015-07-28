﻿// ReSharper disable MemberHidesStaticFromOuterClass
namespace NServiceBus
{
    static class WellKnownConfigurationKeys
    {
        public static class Topology
        {
            public static class Resources
            {
                public const string QueueDescriptionsFactory = "AzureServiceBus.Settings.Topology.Resources.QueueDescriptionsFactory";
                public const string TopicDescriptionsFactory = "AzureServiceBus.Settings.Topology.Resources.TopicDescriptionsFactory";
                public const string SubscriptionDescriptionsFactory = "AzureServiceBus.Settings.Topology.Resources.SubscriptionDescriptionsFactory";
            }

            public static class Addressing
            {
                public const string Strategy = "AzureServiceBus.Settings.Topology.Addressing.Strategy";

                public static class Partitioning
                {
                    public const string Strategy = "AzureServiceBus.Settings.Topology.Addressing.Partitioning.Strategy";
                    public const string Namespaces = "AzureServiceBus.Settings.Topology.Addressing.Partitioning.Namespaces";
                }

                public static class Composition
                {
                    public const string Strategy = "AzureServiceBus.Settings.Topology.Addressing.Composition.Strategy";
                }

                public static class Validation
                {
                    public const string Strategy = "AzureServiceBus.Settings.Topology.Addressing.Validation.Strategy";
                }

                public static class Sanitization
                {
                    public const string Strategy = "AzureServiceBus.Settings.Topology.Addressing.Sanitization.Strategy";
                }

                public static class Individualization
                {
                    public const string Strategy = "AzureServiceBus.Settings.Topology.Addressing.Individualization.Strategy";
                }
            }
        }

        public static class Connectivity
        {
            public const string NumberOfMessagingFactoriesPerNamespace = "AzureServiceBus.Connectivity.NumberOfMessagingFactoriesPerNamespace";
            public const string NumberOfMessageReceiversPerEntity = "AzureServiceBus.Connectivity.NumberOfMessageReceiversPerEntity";
            public const string MessagingFactorySettingsFactory = "AzureServiceBus.Connectivity.MessagingFactorySettingsFactory";
        }

    }
}