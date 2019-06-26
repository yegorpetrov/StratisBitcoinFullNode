using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using FxCoin.CryptoPool.DbWallet.Entities;

namespace FxCoin.CryptoPool.DbWallet.TransactionHandler
{
    public class UnspentOutputReference
    {
        /// <summary>
        /// The address associated with this UTXO
        /// </summary>
        public HdAddress Address { get; set; }

        /// <summary>
        /// The transaction representing the UTXO.
        /// </summary>
        public TxRef Transaction { get; set; }

        /// <summary>
        /// Number of confirmations for this UTXO.
        /// </summary>
        public int Confirmations { get; set; }
        public HdAccount Account { get; internal set; }

        /// <summary>
        /// Convert the <see cref="TransactionData"/> to an <see cref="OutPoint"/>
        /// </summary>
        /// <returns>The corresponding <see cref="OutPoint"/>.</returns>
        public OutPoint ToOutPoint()
        {
            return new OutPoint(uint256.Parse(this.Transaction.TxId), (uint)this.Transaction.Index);
        }
    }
}
