using System;
using System.Threading.Tasks;

namespace reactive.pipes
{
    /// <summary>
    /// An message publisher allows eventing to some consumers.
    /// </summary>
    public interface IMessagePublisher : IDisposable
    {
        Task<bool> PublishAsync(object @event);
        bool Publish(object @event);
    }
}