using FxCoin.CryptoPool.DbWallet.Entities;
using Stratis.Bitcoin.EventBus;
using System;
using System.Collections.Generic;
using System.Text;

namespace FxCoin.CryptoPool.DbWallet
{
    public class TxRefEvent : EventBase
    {
        public TxRef TxRef { get; set; }
    }
}
