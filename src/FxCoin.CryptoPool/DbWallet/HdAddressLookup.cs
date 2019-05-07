using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NBitcoin;
using NBitcoin.DataEncoders;
using FxCoin.CryptoPool.DbWallet.Entities;
using FxCoin.CryptoPool.DbWallet.Entities.Context;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Utilities;

namespace FxCoin.CryptoPool.DbWallet
{
    public class HdAddressLookup
    {
        private readonly ScopeRunner runner;
        private IDictionary<string, int> spkToAddrIdLookup;
        private IDictionary<OutPoint, int> utxoIdLookup;

        public HdAddressLookup(ScopeRunner runner)
        {
            this.runner = runner;
        }

        public void Init()
        {
            this.runner.Run<DbWalletContext>(dbContext =>
            {
                this.spkToAddrIdLookup = dbContext.Set<HdAddress>().Select(a => new
                {
                    a.Id,
                    a.ScriptPubKey,
                })
                .ToDictionary(e => ToHex(e.ScriptPubKey), e => e.Id);

                this.utxoIdLookup = dbContext.Set<TxRef>()
                    .Where(txref => !txref.SpendingBlock.HasValue)
                    .Select(txref => new
                    {
                        txref.Id,
                        txref.TxId,
                        txref.Index
                    })
                    .ToDictionary(e => new OutPoint(uint256.Parse(e.TxId), e.Index), e => e.Id);
            });
        }

        private static string ToHex(byte[] input) => Encoders.Hex.EncodeData(input);

        public int this[byte[] key]
        {
            get => this.spkToAddrIdLookup[ToHex(key)];
            set => this.spkToAddrIdLookup[ToHex(key)] = value;
        }

        public int this[OutPoint @out]
        {
            get => this.utxoIdLookup[@out];
            set => this.utxoIdLookup[@out] = value;
        }

        public bool TryGetAddressId(byte[] key, out int id)
        {
            return this.spkToAddrIdLookup.TryGetValue(ToHex(key), out id);
        }

        public bool TryGetUtxoId(OutPoint @out, out int id)
        {
            return this.utxoIdLookup.TryGetValue(@out, out id);
        }

        public bool IsRelevantBlock(Block block)
        {
            return block.Transactions.Any(transaction =>
                transaction.Outputs.Any(o => TryGetAddressId(o.ScriptPubKey.ToBytes(), out _)) ||
                transaction.Inputs.Any(i => TryGetUtxoId(i.PrevOut, out _)));
        }

        public void EvictUtxo(OutPoint @out)
        {
            this.utxoIdLookup.Remove(@out);
        }
    }
}
