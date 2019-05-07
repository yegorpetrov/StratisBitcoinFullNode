using System;
using System.Collections.Generic;
using System.Text;

namespace FxCoin.CryptoPool.Contracts
{
    public class GetTransactionResponse
    {
        public decimal Fee { get; set; }
        public string TxId { get; set; }
    }
}
