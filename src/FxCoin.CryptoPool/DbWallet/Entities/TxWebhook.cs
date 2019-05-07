using System;
using System.Collections.Generic;
using System.Text;

namespace FxCoin.CryptoPool.DbWallet.Entities
{
    public class TxWebhook : Base
    {
        public int TxRefId { get; set; }
        public TxRef TxRef { get; set; }
        public DateTime Created { get; set; }
        public DateTime? SendOn { get; set; }
        public string Status { get; set; }
    }
}
