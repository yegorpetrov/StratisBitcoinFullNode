using System;
using System.Collections.Generic;

namespace FxCoin.CryptoPool.Contracts
{
    public class TxBuilderRequest
    {
        /// <summary>
        /// Collection of recipient addresses and satoshi amounts to be received by them
        /// </summary>
        public IDictionary<string, long> Recipients { get; set; }

        /// <summary>
        /// Miner's fee in satoshis per kB
        /// </summary>
        public long? SatoshiPerKbFee { get; set; }

        public Guid? ReservationId { get; set; }

        public int MinConfirmations { get; set; }

        public string WalletPassword { get; set; }
    }
}
