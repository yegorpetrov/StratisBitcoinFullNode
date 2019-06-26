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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace FxCoin.CryptoPool.DbWallet
{
    public class HdAddressLookup
    {
        private readonly ScopeRunner runner;
        private readonly ILogger<HdAddressLookup> _logger;
        private readonly Network _network;
        private IDictionary<string, int> spkToAddrIdLookup;
        private IDictionary<OutPoint, int> utxoIdLookup;

        public HdAddressLookup(ScopeRunner runner, INodeStats nodeStats, ILogger<HdAddressLookup> logger, Network network)
        {
            this.runner = runner;
            this._logger = logger;
            this._network = network;
            nodeStats.RegisterStats(AddComponentStats, StatsType.Component);
        }

        private void AddComponentStats(StringBuilder statsBuilder)
        {
            statsBuilder.AppendLine();
            statsBuilder.AppendLine("======Wallet Lookup======");
            statsBuilder.AppendLine($"ScriptPubKey to address id lookup size: {this.spkToAddrIdLookup.Count}");
            statsBuilder.AppendLine($"OutPoint to TxRef id lookup size: {this.utxoIdLookup.Count}");
        }

        public void Init()
        {
            _logger.LogInformation($"Initializing address lookup...");
            var sw = new Stopwatch();
            sw.Start();

            this.runner.Run<DbWalletContext>(dbContext =>
            {
                this.spkToAddrIdLookup = new ConcurrentDictionary<string, int>(
                    dbContext.Set<HdAddress>()
                    .AsNoTracking()
                    .Include(a => a.Account)
                    .Select(e => new KeyValuePair<string, int>(ToHex(this._network.GenerateAddressAndSpk(e.Account.ExtPubKey, e.Index, e.IsChange).spk), e.Id))
                );

                this.utxoIdLookup = new ConcurrentDictionary<OutPoint, int>(
                    dbContext.Set<TxRef>()
                    .AsNoTracking()
                    .Where(txref => !txref.SpendingBlock.HasValue)
                    .Select(e => new { e.Id, e.TxId, e.Index })
                    .Select(e => new KeyValuePair<OutPoint, int>(
                        new OutPoint(uint256.Parse(e.TxId), e.Index), e.Id))
                );
            });

            sw.Stop();
            _logger.LogInformation($"Address lookup initialized in {sw.Elapsed} ({this.spkToAddrIdLookup.Count}, {this.utxoIdLookup.Count})");
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

        public object Dump()
        {
            return new
            {
                spkToAddrIdLookup,
                utxoIdLookup
            };
        }
    }
}
