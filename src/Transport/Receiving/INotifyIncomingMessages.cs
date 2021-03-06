using System;

namespace NServiceBus.AzureServiceBus
{
    using System.Threading.Tasks;
    
    public interface INotifyIncomingMessages
    {
        bool IsRunning { get; }
        int RefCount { get; set; }

        void Initialize(EntityInfo entity, Func<IncomingMessageDetails, ReceiveContext, Task> callback, Func<Exception, Task> errorCallback, int maximumConcurrency);

        void Start();
        Task Stop();
    }
}