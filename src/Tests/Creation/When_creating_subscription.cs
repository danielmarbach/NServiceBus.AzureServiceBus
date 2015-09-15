﻿namespace NServiceBus.AzureServiceBus.Tests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.ServiceBus;
    using Microsoft.ServiceBus.Messaging;
    using NServiceBus.Azure.Transports.WindowsAzureServiceBus.Creation;
    using NServiceBus.Azure.WindowsAzureServiceBus.Tests;
    using NServiceBus.Settings;
    using NUnit.Framework;

    [TestFixture]
    [Category("AzureServiceBus")]
    public class When_creating_subscription
    {
        const string topicPath = "topic";

        [TestFixtureSetUp]
        public void TopicSetup()
        {
            var namespaceManager = new NamespaceManagerAdapter(NamespaceManager.CreateFromConnectionString(AzureServiceBusConnectionString.Value));
            if (!namespaceManager.TopicExistsAsync(topicPath).Result)
            {
                namespaceManager.CreateTopicAsync(new TopicDescription(topicPath)).Wait();
            }
        }

        [TestFixtureTearDown]
        public void TopicCleanUp()
        {
            var namespaceManager = new NamespaceManagerAdapter(NamespaceManager.CreateFromConnectionString(AzureServiceBusConnectionString.Value));
            namespaceManager.DeleteTopicAsync(topicPath).Wait();        
        }

        [Test]
        public async Task Should_properly_set_not_create_subsciption_when_topology_creation_is_turned_off()
        {
            var settings = new DefaultConfigurationValues().Apply(new SettingsHolder());
            settings.Set(WellKnownConfigurationKeys.Core.CreateTopology, false);

            const string subsciptionName = "mysubsciption1";
            //make sure there is no leftover from previous test
            var namespaceManager = new NamespaceManagerAdapter(NamespaceManager.CreateFromConnectionString(AzureServiceBusConnectionString.Value));
            await namespaceManager.DeleteSubsciptionAsync(topicPath, subsciptionName);

            var creator = new AzureServiceBusSubsciptionCreator(settings);

            await creator.CreateAsync(topicPath, subsciptionName, namespaceManager);

            Assert.IsFalse(await namespaceManager.TopicExistsAsync(subsciptionName));
        }

        [Test]
        public async Task Should_properly_set_use_subscription_description_defaults_if_user_does_not_provide_topic_description_values()
        {
            var settings = new DefaultConfigurationValues().Apply(new SettingsHolder());
            var namespaceManager = new NamespaceManagerAdapter(NamespaceManager.CreateFromConnectionString(AzureServiceBusConnectionString.Value));

            var creator = new AzureServiceBusSubsciptionCreator(settings);
            const string subsciptionName = "sub1";
            var subscriptionDescription = await creator.CreateAsync(topicPath, subsciptionName, namespaceManager);

            Assert.IsTrue(await namespaceManager.SubscriptionExistsAsync(topicPath, subsciptionName));
            Assert.AreEqual(TimeSpan.MaxValue, subscriptionDescription.AutoDeleteOnIdle);
            Assert.AreEqual(TimeSpan.MaxValue, subscriptionDescription.DefaultMessageTimeToLive);
            Assert.AreEqual(TimeSpan.FromMilliseconds(30000), subscriptionDescription.LockDuration);
            Assert.True(subscriptionDescription.EnableBatchedOperations);
            Assert.IsFalse(subscriptionDescription.EnableDeadLetteringOnFilterEvaluationExceptions);
            Assert.IsFalse(subscriptionDescription.EnableDeadLetteringOnMessageExpiration);
            Assert.IsFalse(subscriptionDescription.RequiresSession);
            Assert.AreEqual(6, subscriptionDescription.MaxDeliveryCount);
            Assert.IsNull(subscriptionDescription.ForwardDeadLetteredMessagesTo);
            Assert.IsNull(subscriptionDescription.ForwardTo);

            await namespaceManager.DeleteSubsciptionAsync(topicPath, subsciptionName);
        }


        [Test]
        public async Task Should_properly_set_use_subscription_description_provided_by_user()
        {
            var settings = new DefaultConfigurationValues().Apply(new SettingsHolder());
            var extensions = new TransportExtensions<AzureServiceBusTransport>(settings);
            var namespaceManager = new NamespaceManagerAdapter(NamespaceManager.CreateFromConnectionString(AzureServiceBusConnectionString.Value));

            const string subscriptionName = "sub2";
            var subscriptionDescription = new SubscriptionDescription(topicPath, subscriptionName)
            {
                LockDuration = TimeSpan.FromMinutes(3)
            };

            extensions.Topology().Resources().Subscriptions().DescriptionFactory((_topicPath, subName, _settings) => subscriptionDescription);

            var creator = new AzureServiceBusSubsciptionCreator(settings);

            var foundDescription = await creator.CreateAsync(topicPath, subscriptionName, namespaceManager);

            Assert.IsTrue(await namespaceManager.SubscriptionExistsAsync(topicPath, subscriptionName));
            Assert.AreEqual(subscriptionDescription, foundDescription);

            await namespaceManager.DeleteSubsciptionAsync(topicPath, subscriptionName);
        }

        [Test]
        public async Task Should_properly_set_AutoDeleteOnIdle_on_the_created_entity()
        {
            var namespaceManager = new NamespaceManagerAdapter(NamespaceManager.CreateFromConnectionString(AzureServiceBusConnectionString.Value));

            var settings = new DefaultConfigurationValues().Apply(new SettingsHolder());
            var extensions = new TransportExtensions<AzureServiceBusTransport>(settings);

            var autoDeleteTime = TimeSpan.FromDays(1);
            extensions.Topology().Resources().Subscriptions().AutoDeleteOnIdle(autoDeleteTime);

            var creator = new AzureServiceBusSubsciptionCreator(settings);

            const string subscriptionName = "sub3";
            await creator.CreateAsync(topicPath, subscriptionName, namespaceManager);
            var foundDescription = await namespaceManager.GetSubscriptionAsync(topicPath, subscriptionName);

            Assert.AreEqual(autoDeleteTime, foundDescription.AutoDeleteOnIdle);

            await namespaceManager.DeleteSubsciptionAsync(topicPath, subscriptionName);
        }

        [Test]
        public async Task Should_properly_set_DefaultMessageTimeToLive_on_the_created_entity()
        {
            var namespaceManager = new NamespaceManagerAdapter(NamespaceManager.CreateFromConnectionString(AzureServiceBusConnectionString.Value));

            var settings = new DefaultConfigurationValues().Apply(new SettingsHolder());
            var extensions = new TransportExtensions<AzureServiceBusTransport>(settings);

            var timeToLive = TimeSpan.FromDays(10);
            extensions.Topology().Resources().Subscriptions().DefaultMessageTimeToLive(timeToLive);

            var creator = new AzureServiceBusSubsciptionCreator(settings);

            const string subscriptionName = "sub4";
            await creator.CreateAsync(topicPath, subscriptionName, namespaceManager);

            var foundDescription = await namespaceManager.GetSubscriptionAsync(topicPath, subscriptionName);

            Assert.AreEqual(timeToLive, foundDescription.DefaultMessageTimeToLive);

            await namespaceManager.DeleteSubsciptionAsync(topicPath, subscriptionName);
        }

        [Test]
        public async Task Should_properly_set_EnableBatchedOperations_on_the_created_entity()
        {
            var namespaceManager = new NamespaceManagerAdapter(NamespaceManager.CreateFromConnectionString(AzureServiceBusConnectionString.Value));

            var settings = new DefaultConfigurationValues().Apply(new SettingsHolder());
            var extensions = new TransportExtensions<AzureServiceBusTransport>(settings);

            extensions.Topology().Resources().Subscriptions().EnableBatchedOperations(false);

            var creator = new AzureServiceBusSubsciptionCreator(settings);

            const string subscriptionName = "sub5";
            await creator.CreateAsync(topicPath, subscriptionName, namespaceManager);

            var foundDescription = await namespaceManager.GetSubscriptionAsync(topicPath, subscriptionName);

            Assert.IsFalse(foundDescription.EnableBatchedOperations);

            await namespaceManager.DeleteSubsciptionAsync(topicPath, subscriptionName);
        }

        [Test]
        public async Task Should_properly_set_EnableDeadLetteringOnFilterEvaluationExceptions_on_the_created_entity()
        {
            var namespaceManager = new NamespaceManagerAdapter(NamespaceManager.CreateFromConnectionString(AzureServiceBusConnectionString.Value));

            var settings = new DefaultConfigurationValues().Apply(new SettingsHolder());
            var extensions = new TransportExtensions<AzureServiceBusTransport>(settings);

            extensions.Topology().Resources().Subscriptions().EnableDeadLetteringOnFilterEvaluationExceptions(true);

            var creator = new AzureServiceBusSubsciptionCreator(settings);

            const string subscriptionName = "sub6";
            await creator.CreateAsync(topicPath, subscriptionName, namespaceManager);

            var foundDescription = await namespaceManager.GetSubscriptionAsync(topicPath, subscriptionName);

            Assert.IsTrue(foundDescription.EnableDeadLetteringOnFilterEvaluationExceptions);

            await namespaceManager.DeleteSubsciptionAsync(topicPath, subscriptionName);
        }

        [Test]
        public async Task Should_properly_set_EnableDeadLetteringOnMessageExpiration_on_the_created_entity()
        {
            var namespaceManager = new NamespaceManagerAdapter(NamespaceManager.CreateFromConnectionString(AzureServiceBusConnectionString.Value));

            var settings = new DefaultConfigurationValues().Apply(new SettingsHolder());
            var extensions = new TransportExtensions<AzureServiceBusTransport>(settings);

            extensions.Topology().Resources().Subscriptions().EnableDeadLetteringOnMessageExpiration(true);

            var creator = new AzureServiceBusSubsciptionCreator(settings);

            const string subscriptionName = "sub7";
            await creator.CreateAsync(topicPath, subscriptionName, namespaceManager);

            var foundDescription = await namespaceManager.GetSubscriptionAsync(topicPath, subscriptionName);

            Assert.IsTrue(foundDescription.EnableDeadLetteringOnMessageExpiration);

            await namespaceManager.DeleteSubsciptionAsync(topicPath, subscriptionName);
        }

        [Test]
        public async Task Should_properly_set_LockDuration_on_the_created_entity()
        {
            var namespaceManager = new NamespaceManagerAdapter(NamespaceManager.CreateFromConnectionString(AzureServiceBusConnectionString.Value));

            var settings = new DefaultConfigurationValues().Apply(new SettingsHolder());
            var extensions = new TransportExtensions<AzureServiceBusTransport>(settings);

            var lockDuration = TimeSpan.FromMinutes(2);
            extensions.Topology().Resources().Subscriptions().LockDuration(lockDuration);

            var creator = new AzureServiceBusSubsciptionCreator(settings);

            const string subscriptionName = "sub8";
            await creator.CreateAsync(topicPath, subscriptionName, namespaceManager);

            var foundDescription = await namespaceManager.GetSubscriptionAsync(topicPath, subscriptionName);

            Assert.AreEqual(lockDuration, foundDescription.LockDuration);

            await namespaceManager.DeleteSubsciptionAsync(topicPath, subscriptionName);
        }

        [Test]
        public async Task Should_properly_set_MaxDeliveryCount_on_the_created_entity()
        {
            var namespaceManager = new NamespaceManagerAdapter(NamespaceManager.CreateFromConnectionString(AzureServiceBusConnectionString.Value));

            var settings = new DefaultConfigurationValues().Apply(new SettingsHolder());
            var extensions = new TransportExtensions<AzureServiceBusTransport>(settings);

            const int deliveryCount = 10;
            extensions.Topology().Resources().Subscriptions().MaxDeliveryCount(deliveryCount);

            var creator = new AzureServiceBusSubsciptionCreator(settings);

            const string subscriptionName = "sub9";
            await creator.CreateAsync(topicPath, subscriptionName, namespaceManager);

            var foundDescription = await namespaceManager.GetSubscriptionAsync(topicPath, subscriptionName);

            Assert.AreEqual(deliveryCount, foundDescription.MaxDeliveryCount);

            await namespaceManager.DeleteSubsciptionAsync(topicPath, subscriptionName);
        }

        [Test]
        public async Task Should_properly_set_RequiresSession_on_the_created_entity()
        {
            var namespaceManager = new NamespaceManagerAdapter(NamespaceManager.CreateFromConnectionString(AzureServiceBusConnectionString.Value));

            var settings = new DefaultConfigurationValues().Apply(new SettingsHolder());
            var extensions = new TransportExtensions<AzureServiceBusTransport>(settings);

            extensions.Topology().Resources().Subscriptions().RequiresSession(true);

            var creator = new AzureServiceBusSubsciptionCreator(settings);

            const string subscriptionName = "sub10";
            await creator.CreateAsync(topicPath, subscriptionName, namespaceManager);

            var foundDescription = await namespaceManager.GetSubscriptionAsync(topicPath, subscriptionName);

            Assert.IsTrue(foundDescription.RequiresSession);

            await namespaceManager.DeleteSubsciptionAsync(topicPath, subscriptionName);
        }

        [Test]
        public async Task Should_properly_set_ForwardTo_on_the_created_entity()
        {
            var namespaceManager = new NamespaceManagerAdapter(NamespaceManager.CreateFromConnectionString(AzureServiceBusConnectionString.Value));

            var topicCreator = new AzureServiceBusTopicCreator(new DefaultConfigurationValues().Apply(new SettingsHolder()));
            var topicToForwardTo = await topicCreator.CreateAsync("topic2forward2", namespaceManager);


            var settings = new DefaultConfigurationValues().Apply(new SettingsHolder());
            var extensions = new TransportExtensions<AzureServiceBusTransport>(settings);

            extensions.Topology().Resources().Subscriptions().ForwardTo(topicToForwardTo.Path);

            var creator = new AzureServiceBusSubsciptionCreator(settings);

            const string subscriptionName = "sub11";
            await creator.CreateAsync(topicPath, subscriptionName, namespaceManager);

            var foundDescription = await namespaceManager.GetSubscriptionAsync(topicPath, subscriptionName);

            Assert.That(foundDescription.ForwardTo.EndsWith(topicToForwardTo.Path));

            await namespaceManager.DeleteSubsciptionAsync(topicPath, subscriptionName);
            await namespaceManager.DeleteTopicAsync(topicToForwardTo.Path);
        }

        [Test]
        public async Task Should_properly_set_ForwardTo_on_the_created_entity_that_qualifies_the_condition()
        {
            var namespaceManager = new NamespaceManagerAdapter(NamespaceManager.CreateFromConnectionString(AzureServiceBusConnectionString.Value));

            var topicCreator = new AzureServiceBusTopicCreator(new DefaultConfigurationValues().Apply(new SettingsHolder()));
            var topicToForwardTo = await topicCreator.CreateAsync("topic2forward2", namespaceManager);


            var settings = new DefaultConfigurationValues().Apply(new SettingsHolder());
            var extensions = new TransportExtensions<AzureServiceBusTransport>(settings);

            extensions.Topology().Resources().Subscriptions().ForwardTo(subName => subName == "sub12", topicToForwardTo.Path);

            var creator = new AzureServiceBusSubsciptionCreator(settings);

            const string subscriptionName = "sub12";
            await creator.CreateAsync(topicPath, subscriptionName, namespaceManager);

            var foundDescription = await namespaceManager.GetSubscriptionAsync(topicPath, subscriptionName);

            Assert.That(foundDescription.ForwardTo.EndsWith(topicToForwardTo.Path));

            await namespaceManager.DeleteSubsciptionAsync(topicPath, subscriptionName);
            await namespaceManager.DeleteTopicAsync(topicToForwardTo.Path);
        }
        
        [Test]
        public async Task Should__properly_set_ForwardDeadLetteredMessagesTo_on_the_created_entity()
        {
            var namespaceManager = new NamespaceManagerAdapter(NamespaceManager.CreateFromConnectionString(AzureServiceBusConnectionString.Value));

            var topicCreator = new AzureServiceBusTopicCreator(new DefaultConfigurationValues().Apply(new SettingsHolder()));
            var topicToForwardTo = await topicCreator.CreateAsync("topic2forward2", namespaceManager);


            var settings = new DefaultConfigurationValues().Apply(new SettingsHolder());
            var extensions = new TransportExtensions<AzureServiceBusTransport>(settings);

            extensions.Topology().Resources().Subscriptions().ForwardDeadLetteredMessagesTo(topicToForwardTo.Path);

            var creator = new AzureServiceBusSubsciptionCreator(settings);

            const string subscriptionName = "sub13";
            await creator.CreateAsync(topicPath, subscriptionName, namespaceManager);

            var foundDescription = await namespaceManager.GetSubscriptionAsync(topicPath, subscriptionName);

            Assert.That(foundDescription.ForwardDeadLetteredMessagesTo.EndsWith(topicToForwardTo.Path));

            await namespaceManager.DeleteSubsciptionAsync(topicPath, subscriptionName);
            await namespaceManager.DeleteTopicAsync(topicToForwardTo.Path);
        }

        [Test]
        public async Task Should__properly_set_ForwardDeadLetteredMessagesTo_on_the_created_entity_that_qualifies_the_condition()
        {
            var namespaceManager = new NamespaceManagerAdapter(NamespaceManager.CreateFromConnectionString(AzureServiceBusConnectionString.Value));

            var topicCreator = new AzureServiceBusTopicCreator(new DefaultConfigurationValues().Apply(new SettingsHolder()));
            var topicToForwardTo = await topicCreator.CreateAsync("topic2forward2", namespaceManager);


            var settings = new DefaultConfigurationValues().Apply(new SettingsHolder());
            var extensions = new TransportExtensions<AzureServiceBusTransport>(settings);

            extensions.Topology().Resources().Subscriptions().ForwardDeadLetteredMessagesTo(subName => subName == "sub14", topicToForwardTo.Path);

            var creator = new AzureServiceBusSubsciptionCreator(settings);

            const string subscriptionName = "sub14";
            await creator.CreateAsync(topicPath, subscriptionName, namespaceManager);

            var foundDescription = await namespaceManager.GetSubscriptionAsync(topicPath, subscriptionName);

            Assert.That(foundDescription.ForwardDeadLetteredMessagesTo.EndsWith(topicToForwardTo.Path));

            await namespaceManager.DeleteSubsciptionAsync(topicPath, subscriptionName);
            await namespaceManager.DeleteTopicAsync(topicToForwardTo.Path);
        }

        // todo: test exception handling and edge cases
    }
}