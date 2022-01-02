using System.Threading.Tasks;
using IdempotencyTools;
using Microsoft.ServiceFabric.Services.Remoting;

namespace Interactions
{
    public interface ILogStore : IService
    {
        Task<bool> LogPurchase(IdempotentMessage<PurchaseInfo> idempotentMessage);
    }
}