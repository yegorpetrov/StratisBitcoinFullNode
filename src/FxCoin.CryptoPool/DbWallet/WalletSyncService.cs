namespace FxCoin.CryptoPool.DbWallet
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Extensions.Logging;
    using Stratis.Bitcoin.EventBus;
    using Stratis.Bitcoin.EventBus.CoreEvents;
    using Stratis.Bitcoin.Signals;

    public sealed class WalletSyncService : IDisposable
    {
        private readonly ISignals signals;
        private readonly ScopeRunner scopeRunner;
        private readonly HdAddressLookup addressLookup;
        private IEnumerable<SubscriptionToken> subscriptionTokens;

        public WalletSyncService(
            ILoggerFactory loggerFactory,
            ISignals signals,
            ScopeRunner scopeRunner,
            HdAddressLookup addressLookup)
        {
            this.signals = signals;
            this.scopeRunner = scopeRunner;
            this.addressLookup = addressLookup;
        }

        public void Dispose()
        {
            foreach (SubscriptionToken token in this.subscriptionTokens)
            {
                this.signals.Unsubscribe(token);
            }
        }

        public void Initialize()
        {
            this.scopeRunner.Run<DbWalletManager>(manager =>
            {
                manager.Initialize();
            });
            this.addressLookup.Init();
            this.subscriptionTokens = new[]
            {
                this.signals.Subscribe<BlockConnected>(this.OnBlockConnected),
                this.signals.Subscribe<TransactionReceived>(this.OnTransactionAvailable)
            };
        }

        private void OnTransactionAvailable(TransactionReceived signal)
        {
            this.scopeRunner.Run<DbWalletManager>(manager =>
            {
                manager.Process(signal.ReceivedTransaction);
            });
        }

        /// <summary>
        /// Processes incoming blocks
        /// </summary>
        /// <remarks>Warning: hot method during IBD</remarks>
        private void OnBlockConnected(BlockConnected signal)
        {
            if (this.addressLookup.IsRelevantBlock(signal.ConnectedBlock.Block))
            {
                this.scopeRunner.Run<DbWalletManager>(manager =>
                {
                    manager.Process(signal.ConnectedBlock);
                });
            }
        }
    }
}
