using System;

namespace FxCoin.CryptoPool.DbWallet
{
    public class WalletException : Exception
    {
        public WalletException(string message) : base(message)
        {
        }
    }
}
