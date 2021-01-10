﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConcurrentCollections;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace FxCoin.CryptoPool.DbWallet.Broadcasting
{
    public abstract class BroadcasterManagerBase : IBroadcasterManager
    {
        public event EventHandler<TransactionBroadcastEntry> TransactionStateChanged;

        /// <summary> Connection manager for managing node connections.</summary>
        protected readonly IConnectionManager connectionManager;

        public BroadcasterManagerBase(IConnectionManager connectionManager)
        {
            Guard.NotNull(connectionManager, nameof(connectionManager));

            this.connectionManager = connectionManager;
            Broadcasts = new ConcurrentHashSet<TransactionBroadcastEntry>();
        }

        public void OnTransactionStateChanged(TransactionBroadcastEntry entry)
        {
            TransactionStateChanged?.Invoke(this, entry);
        }

        private ConcurrentHashSet<TransactionBroadcastEntry> Broadcasts { get; }

        public TransactionBroadcastEntry GetTransaction(uint256 transactionHash)
        {
            TransactionBroadcastEntry txEntry = Broadcasts.FirstOrDefault(x => x.Transaction.GetHash() == transactionHash);
            return txEntry ?? null;
        }

        public void AddOrUpdate(Transaction transaction, State state, string errorMessage = "")
        {
            TransactionBroadcastEntry broadcastEntry = Broadcasts.FirstOrDefault(x => x.Transaction.GetHash() == transaction.GetHash());

            if (broadcastEntry == null)
            {
                broadcastEntry = new TransactionBroadcastEntry(transaction, state, errorMessage);
                Broadcasts.Add(broadcastEntry);
                OnTransactionStateChanged(broadcastEntry);
            }
            else if (broadcastEntry.State != state)
            {
                broadcastEntry.State = state;
                OnTransactionStateChanged(broadcastEntry);
            }
        }

        public abstract Task BroadcastTransactionAsync(Transaction transaction);

        /// <summary>
        /// Sends transaction to peers.
        /// </summary>
        /// <param name="transaction">Transaction that will be propagated.</param>
        /// <param name="peers">Peers to whom we will propagate the transaction.</param>
        protected async Task PropagateTransactionToPeersAsync(Transaction transaction, List<INetworkPeer> peers)
        {
            AddOrUpdate(transaction, State.ToBroadcast);

            var invPayload = new InvPayload(transaction);

            foreach (INetworkPeer peer in peers)
            {
                try
                {
                    await peer.SendMessageAsync(invPayload).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }
        }

        protected bool IsPropagated(Transaction transaction)
        {
            TransactionBroadcastEntry broadcastEntry = GetTransaction(transaction.GetHash());
            return broadcastEntry != null && broadcastEntry.State == State.Propagated;
        }
    }
}