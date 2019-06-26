using System;
using System.Collections.Generic;
using System.Text;

namespace FxCoin.CryptoPool.DbWallet
{
    public class InsufficientFundsException : WalletException
    {
        public InsufficientFundsException(string message) : base(message)
        {
        }
    }
}
