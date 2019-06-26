using System;
using System.Collections.Generic;
using System.Text;
using FxCoin.CryptoPool.DbWallet.Entities;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;

namespace FxCoin.CryptoPool.DbWallet
{
    public static class HdAddressExtensions
    {
        public static (string address, byte[] spk) GenerateAddressAndSpk(this Network network, string extPubKey, int addressIndex, bool isChange)
        {
            var address = HdOperations
                .GeneratePublicKey(extPubKey, addressIndex, isChange)
                .GetAddress(network);

            return (address.ToString(), address.ScriptPubKey.ToBytes());
        }
    }
}
