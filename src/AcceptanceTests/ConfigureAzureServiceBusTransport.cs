﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.AzureServiceBus;
using NServiceBus.AzureServiceBus.AcceptanceTests.Infrastructure;

public class ConfigureAzureServiceBusTransport : IConfigureTestExecution
{
    public Task Configure(BusConfiguration config, IDictionary<string, string> settings)
    {
        var connectionString = settings["Transport.ConnectionString"];
        var topology = Environment.GetEnvironmentVariable("AzureServiceBusTransport.Topology");
        
        var transportConfig = config.UseTransport<AzureServiceBusTransport>().ConnectionString(connectionString);

        if (topology == "ForwardingTopology")
        {
            transportConfig.UseTopology<ForwardingTopology>();
        }
        //else default

        config.RegisterComponents(c =>
        {
            c.ConfigureComponent<TestIndependenceMutator>(DependencyLifecycle.SingleInstance);
        });

        config.Pipeline.Register("TestIndependenceBehavior", typeof(TestIndependenceSkipBehavior),
            "Skips messages not created during the current test.");

        return Task.FromResult(0);
    }

    public Task Cleanup()
    {
        return Task.FromResult(0);
    }
}
