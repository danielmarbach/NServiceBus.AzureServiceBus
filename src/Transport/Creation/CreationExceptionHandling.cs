namespace NServiceBus.AzureServiceBus
{
    using Microsoft.ServiceBus.Messaging;

    internal static class CreationExceptionHandling
    {
        public static bool IsInnerExceptionTransient(this MessagingException messagingException)
        {
            var inner = messagingException;

            while (inner != null)
            {
                if (inner.IsTransient || inner is MessagingEntityAlreadyExistsException)
                {
                    return true;
                }

                inner = inner.InnerException as MessagingException;
            }

            return false;
        }
    }
}