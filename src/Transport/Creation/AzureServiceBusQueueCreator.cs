﻿namespace NServiceBus.AzureServiceBus
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using Microsoft.ServiceBus.Messaging;
    using NServiceBus.Logging;
    using NServiceBus.Settings;

    class AzureServiceBusQueueCreator : ICreateAzureServiceBusQueues
    {
        ConcurrentDictionary<string, Task<bool>> rememberExistence = new ConcurrentDictionary<string, Task<bool>>();
        ReadOnlySettings _settings;
        Func<string, ReadOnlySettings, QueueDescription> _descriptionFactory;

        ILog logger = LogManager.GetLogger(typeof(AzureServiceBusQueueCreator));

        public AzureServiceBusQueueCreator(ReadOnlySettings settings)
        {
            _settings = settings;

            if(!_settings.TryGet(WellKnownConfigurationKeys.Topology.Resources.Queues.DescriptionFactory, out _descriptionFactory))
            {
                _descriptionFactory = (queuePath, setting) => new QueueDescription(queuePath)
                {
                    LockDuration = setting.GetOrDefault<TimeSpan>(WellKnownConfigurationKeys.Topology.Resources.Queues.LockDuration),
                    MaxSizeInMegabytes = setting.GetOrDefault<long>(WellKnownConfigurationKeys.Topology.Resources.Queues.MaxSizeInMegabytes),
                    RequiresDuplicateDetection = setting.GetOrDefault<bool>(WellKnownConfigurationKeys.Topology.Resources.Queues.RequiresDuplicateDetection),
                    RequiresSession = setting.GetOrDefault<bool>(WellKnownConfigurationKeys.Topology.Resources.Queues.RequiresSession),
                    DefaultMessageTimeToLive = setting.GetOrDefault<TimeSpan>(WellKnownConfigurationKeys.Topology.Resources.Queues.DefaultMessageTimeToLive),
                    EnableDeadLetteringOnMessageExpiration = setting.GetOrDefault<bool>(WellKnownConfigurationKeys.Topology.Resources.Queues.EnableDeadLetteringOnMessageExpiration),
                    DuplicateDetectionHistoryTimeWindow = setting.GetOrDefault<TimeSpan>(WellKnownConfigurationKeys.Topology.Resources.Queues.DuplicateDetectionHistoryTimeWindow),
                    MaxDeliveryCount = setting.GetOrDefault<int>(WellKnownConfigurationKeys.Topology.Resources.Queues.MaxDeliveryCount),
                    EnableBatchedOperations = setting.GetOrDefault<bool>(WellKnownConfigurationKeys.Topology.Resources.Queues.EnableBatchedOperations),
                    EnablePartitioning = setting.GetOrDefault<bool>(WellKnownConfigurationKeys.Topology.Resources.Queues.EnablePartitioning),
                    SupportOrdering = setting.GetOrDefault<bool>(WellKnownConfigurationKeys.Topology.Resources.Queues.SupportOrdering),
                    AutoDeleteOnIdle = setting.GetOrDefault<TimeSpan>(WellKnownConfigurationKeys.Topology.Resources.Queues.AutoDeleteOnIdle),

                    EnableExpress = setting.GetConditional<bool>(queuePath, WellKnownConfigurationKeys.Topology.Resources.Queues.EnableExpress),
                    ForwardDeadLetteredMessagesTo = setting.GetConditional<string>(queuePath, WellKnownConfigurationKeys.Topology.Resources.Queues.ForwardDeadLetteredMessagesTo),
                    ForwardTo = setting.GetConditional<string>(queuePath, WellKnownConfigurationKeys.Topology.Resources.Queues.ForwardTo)
                };
            }
        }

        public async Task<QueueDescription> Create(string queuePath, INamespaceManager namespaceManager)
        {
            var description = _descriptionFactory(queuePath, _settings);

            try
            {
                if (_settings.GetOrDefault<bool>(WellKnownConfigurationKeys.Core.CreateTopology))
                {
                    if (!await ExistsAsync(namespaceManager, description.Path).ConfigureAwait(false))
                    {
                        await namespaceManager.CreateQueue(description).ConfigureAwait(false);
                        logger.InfoFormat("Queue '{0}' created", description.Path);

                        await rememberExistence.AddOrUpdate(description.Path, s => Task.FromResult(true), (s, b) => Task.FromResult(true)).ConfigureAwait(false);
                    }
                    else
                    {
                        logger.InfoFormat("Queue '{0}' already exists, skipping creation", description.Path);
                        logger.InfoFormat("Checking if queue '{0}' needs to be updated", description.Path);
                        var existingDescription = await namespaceManager.GetQueue(description.Path).ConfigureAwait(false);
                        if (MembersAreNotEqual(existingDescription, description))
                        {
                            logger.InfoFormat("Updating queue '{0}' with new description", description.Path);
                            await namespaceManager.UpdateQueue(description).ConfigureAwait(false);
                        }
                    }
                }
                else
                {
                    logger.InfoFormat("Transport.CreateQueues is set to false, skipping the creation of '{0}'", description.Path);
                }
            }
            catch (MessagingEntityAlreadyExistsException)
            {
                // the queue already exists or another node beat us to it, which is ok
                logger.InfoFormat("Queue '{0}' already exists, another node probably beat us to it", description.Path);
            }
            catch (TimeoutException)
            {
                logger.InfoFormat("Timeout occurred on queue creation for '{0}' going to validate if it doesn't exist", description.Path);

                // there is a chance that the timeout occurred, but the topic was still created, check again
                if (!await ExistsAsync(namespaceManager, description.Path, removeCacheEntry: true).ConfigureAwait(false))
                {
                    throw;
                }

                logger.InfoFormat("Looks like queue '{0}' exists anyway", description.Path);
            }
            catch (MessagingException ex)
            {
                if (!ex.IsTransient)
                {
                    logger.Fatal(string.Format("{1} {2} occurred on queue creation {0}", description.Path, (ex.IsTransient ? "Transient" : "Non transient"), ex.GetType().Name), ex);
                    throw;
                }

                logger.Info(string.Format("{1} {2} occurred on queue creation {0}", description.Path, (ex.IsTransient ? "Transient" : "Non transient"), ex.GetType().Name), ex);
            }

            return description;
        }


        async Task<bool> ExistsAsync(INamespaceManager namespaceClient, string queuePath, bool removeCacheEntry = false)
        {
            var key = queuePath;
            logger.InfoFormat("Checking existence cache for '{0}'", queuePath);

            var exists = await rememberExistence.GetOrAdd(key, async s =>
            {
                logger.InfoFormat("Checking namespace for existence of the queue '{0}'", queuePath);
                return await namespaceClient.QueueExists(key).ConfigureAwait(false);
            }).ConfigureAwait(false);

            logger.InfoFormat("Determined, from cache, that the queue '{0}' {1}", queuePath, exists ? "exists" : "does not exist");

            return exists;
        }

        private bool MembersAreNotEqual(QueueDescription existingDescription, QueueDescription newDescription)
        {
            if (existingDescription.RequiresDuplicateDetection != newDescription.RequiresDuplicateDetection)
            {
                logger.Warn("RequiresDuplicateDetection cannot be update on the existing queue!");
            }
            if (existingDescription.EnablePartitioning != newDescription.EnablePartitioning)
            {
                logger.Warn("EnablePartitioning cannot be update on the existing queue!");
            }
            if (existingDescription.RequiresSession != newDescription.RequiresSession)
            {
                logger.Warn("RequiresSession cannot be update on the existing queue!");
            }

            return existingDescription.AutoDeleteOnIdle != newDescription.AutoDeleteOnIdle
                   || existingDescription.LockDuration != newDescription.LockDuration
                   || existingDescription.MaxSizeInMegabytes != newDescription.MaxSizeInMegabytes
                   || existingDescription.DefaultMessageTimeToLive != newDescription.DefaultMessageTimeToLive
                   || existingDescription.EnableDeadLetteringOnMessageExpiration != newDescription.EnableDeadLetteringOnMessageExpiration
                   || existingDescription.DuplicateDetectionHistoryTimeWindow != newDescription.DuplicateDetectionHistoryTimeWindow
                   || existingDescription.MaxDeliveryCount != newDescription.MaxDeliveryCount
                   || existingDescription.EnableBatchedOperations != newDescription.EnableBatchedOperations
                   || existingDescription.SupportOrdering != newDescription.SupportOrdering
                   || existingDescription.EnableExpress != newDescription.EnableExpress
                   || existingDescription.ForwardDeadLetteredMessagesTo != newDescription.ForwardDeadLetteredMessagesTo
                   || existingDescription.ForwardTo != newDescription.ForwardTo;
        }
    }
}