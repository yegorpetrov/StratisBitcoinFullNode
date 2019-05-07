using FxCoin.CryptoPool.DbWallet.Broadcasting;
using System;

namespace FxCoin.CryptoPool.DbWallet.Broadcasting
{
    public class TransactionBroadcastEntry
    {
        public NBitcoin.Transaction Transaction { get; }

        public State State { get; set; }

        public string ErrorMessage { get; set; }

        public TransactionBroadcastEntry(NBitcoin.Transaction transaction, State state, string errorMessage)
        {
            Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
            State = state;
            ErrorMessage = errorMessage;
        }
    }
}
