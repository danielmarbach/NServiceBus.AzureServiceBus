namespace NServiceBus.AzureServiceBus
{
    using System.Threading.Tasks;
    using Microsoft.ServiceBus;
    using Microsoft.ServiceBus.Messaging;

    class MessagingFactoryAdapter : IMessagingFactory
    {
        readonly MessagingFactory _factory;

        public MessagingFactoryAdapter(MessagingFactory factory)
        {
            _factory = factory;
        }

        public bool IsClosed
        {
            get { return _factory.IsClosed; }
        }

        public RetryPolicy RetryPolicy
        {
            get { return _factory.RetryPolicy; }
            set { _factory.RetryPolicy = value; }
        }

        public async Task<IMessageReceiver> CreateMessageReceiver(string entitypath, ReceiveMode receiveMode)
        {
            return new MessageReceiverAdapter(await _factory.CreateMessageReceiverAsync(entitypath, receiveMode).ConfigureAwait(false));
        }

        public async Task<IMessageSender> CreateMessageSender(string entitypath)
        {
            return new MessageSenderAdapter(await _factory.CreateMessageSenderAsync(entitypath).ConfigureAwait(false));
        }

        public async Task<IMessageSender> CreateMessageSender(string entitypath, string viaEntityPath)
        {
            return new MessageSenderAdapter(await _factory.CreateMessageSenderAsync(entitypath, viaEntityPath).ConfigureAwait(false));
        }

        public async Task CloseAsync()
        {
            await _factory.CloseAsync();
        }
    }
}