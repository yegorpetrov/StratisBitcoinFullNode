using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;

namespace FxCoin.CryptoPool.DbWallet.Broadcasting
{
    public class FullNodeBroadcasterManager : BroadcasterManagerBase
    {
        /// <summary>Memory pool validator for validating transactions.</summary>
        private readonly IMempoolValidator mempoolValidator;

        public FullNodeBroadcasterManager(IConnectionManager connectionManager, IMempoolValidator mempoolValidator) : base(connectionManager)
        {
            Guard.NotNull(mempoolValidator, nameof(mempoolValidator));

            this.mempoolValidator = mempoolValidator;
        }

        /// <inheritdoc />
        public override async Task BroadcastTransactionAsync(Transaction transaction)
        {
            Guard.NotNull(transaction, nameof(transaction));

            if (IsPropagated(transaction))
                return;

            var state = new MempoolValidationState(false);

            if (!await mempoolValidator.AcceptToMemoryPool(state, transaction).ConfigureAwait(false))
            {
                string errorMessage = "Failed";

                if (state.Error?.ConsensusError != null)
                {
                    errorMessage = state.Error.ConsensusError.Message;
                }
                else if (!string.IsNullOrEmpty(state.Error?.Code))
                {
                    errorMessage = state.Error.Code;
                }

                AddOrUpdate(transaction, State.CantBroadcast, errorMessage);
            }
            else
            {
                await PropagateTransactionToPeersAsync(transaction, connectionManager.ConnectedPeers.ToList()).ConfigureAwait(false);
            }
        }
    }
}
