﻿namespace FxCoin.CryptoPool.DbWallet
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Text;
    using FxCoin.CryptoPool.DbWallet.Entities;
    using FxCoin.CryptoPool.DbWallet.Entities.Context;
    using FxCoin.CryptoPool.DbWallet.TransactionHandler;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using NBitcoin;
    using NBitcoin.DataEncoders;
    using Stratis.Bitcoin.Features.Wallet;
    using Stratis.Bitcoin.Primitives;
    using Stratis.Bitcoin.Signals;
    using Stratis.Bitcoin.Utilities;

    public class DbWalletManager
    {
        private readonly DbWalletContext dbContext;
        private readonly Network network;
        private readonly HdAddressLookup addressLookup;
        private readonly ChainIndexer chain;
        private readonly ILogger logger;
        private readonly ISignals signals;

        public DbWalletManager(
            DbWalletContext dbContext,
            Network network,
            HdAddressLookup addressLookup,
            ChainIndexer chain,
            ILogger<DbWalletManager> logger,
            ISignals signals)
        {
            this.dbContext = dbContext;
            this.network = network;
            this.addressLookup = addressLookup;
            this.chain = chain;
            this.logger = logger;
            this.signals = signals;
        }

        public void Initialize()
        {
            this.dbContext.Database.Migrate();
        }

        public int CreateAccount(string encryptedSeed, string password, byte[] chainCode)
        {
            this.logger.LogTrace($"New account requested");

            DbSet<HdAccount> accounts = this.dbContext.Set<HdAccount>();
            HdAccount account;

            if ((account = accounts.FirstOrDefault(a => a.Seed == encryptedSeed)) == null)
            {
                account = accounts.Add(new HdAccount
                {
                    Seed = encryptedSeed,
                    Index = accounts.Count()
                })
                .Entity;

                this.dbContext.SaveChanges();

                Key privateKey = HdOperations.DecryptSeed(encryptedSeed, password, this.network);
                string accountHdPath = HdOperations.GetAccountHdPath(this.network.Consensus.CoinType, account.Index);
                ExtPubKey accountExtPubKey = HdOperations.GetExtendedPublicKey(privateKey, chainCode, accountHdPath);

                account.ExtPubKey = accountExtPubKey.ToString(this.network);
                account.ChainCode = chainCode;

                this.dbContext.SaveChanges();
            }
            else
            {
                this.logger.LogWarning($"An account with the same seed already exists: {account}");
            }

            return account.Index;
        }

        public (string address, int index, byte[] scriptPubKey) GetAddress(int accountId, bool forChange = false)
        {
            HdAccount account = this.dbContext.Set<HdAccount>()
                .FirstOrDefault(a => a.Index == accountId);

            if (account == default)
            {
                this.logger.LogWarning($"No account found for {accountId}");
                throw new KeyNotFoundException(
                    $"No account found for {accountId}");
            }
            else
            {
                DbSet<HdAddress> addresses = this.dbContext.Set<HdAddress>();
                int idx = addresses.Count(a => a.Account.Index == accountId && a.IsChange == forChange);

                HdAddress newAddressEntry = addresses.Add(new HdAddress
                {
                    Account = account,
                    IsChange = forChange,
                    Index = idx
                })
                .Entity;

                this.dbContext.SaveChanges();

                PubKey key = HdOperations.GeneratePublicKey(
                    account.ExtPubKey, newAddressEntry.Index, forChange);
                BitcoinPubKeyAddress address = key.GetAddress(this.network);
                newAddressEntry.ScriptPubKey = address.ScriptPubKey.ToBytes();
                newAddressEntry.PubKey = key.ToBytes();
                newAddressEntry.Address = address.ToString();
                this.dbContext.SaveChanges();
                this.addressLookup[newAddressEntry.ScriptPubKey] = newAddressEntry.Id;

                return (address.ToString(), newAddressEntry.Index, newAddressEntry.ScriptPubKey);
            }
        }

        public void WalletPassphraseChange(string oldPassphrase, string newPassphrase)
        {
            this.logger.LogTrace($"Account passphrase change requested");

            HdAccount hdAccount = this.dbContext.Set<HdAccount>()
               .FirstOrDefault(acc => acc.Index == default);

            var privateKey = Key.Parse(hdAccount.Seed, oldPassphrase, this.network);
            string encryptedSeed = privateKey.GetEncryptedBitcoinSecret(newPassphrase, this.network).ToWif();
        
            hdAccount.Seed = encryptedSeed;

            this.dbContext.Set<HdAccount>().Update(hdAccount);
            this.dbContext.SaveChanges();
        }

        internal void Process(ChainedHeaderBlock connectedBlock)
        {
            if (connectedBlock.Block == default)
            {
                return;
            }

            foreach (Transaction transaction in connectedBlock.Block.Transactions)
            {
                this.Process(transaction, connectedBlock.ChainedHeader.Height);
            }
        }

        private DbSet<TxRef> TxSet { get => this.dbContext.Set<TxRef>(); }

        internal void Process(Transaction transaction, int? block = null)
        {
            int outputIdx = 0;
            foreach (var output in transaction.Outputs)
            {
                if (this.addressLookup.TryGetAddressId(output.ScriptPubKey.ToBytes(), out int addressId))
                {
                    string txId = transaction.ToString();

                    TxRef txref =
                        TxSet.FirstOrDefault(t =>
                            t.TxId == txId &&
                            t.Index == outputIdx)
                        ??
                        TxSet.Attach(new TxRef
                        {
                            AddressId = addressId,
                            Amount = output.Value,
                            Index = outputIdx,
                            TxId = txId
                        }).Entity;

                    bool isNew = txref.Id <= default(int);

                    txref.ArrivalBlock = txref.ArrivalBlock ?? block;

                    this.dbContext.Find<HdAddress>(addressId).IsInUse = true;

                    this.dbContext.SaveChanges();

                    this.addressLookup[new OutPoint(transaction, outputIdx)] = txref.Id;

                    if (isNew)
                    {
                        this.signals.Publish(new TxRefEvent
                        {
                            TxRef = txref
                        });
                    }
                }
                outputIdx++;
            }

            foreach (TxIn input in transaction.Inputs)
            {
                if (this.addressLookup.TryGetUtxoId(input.PrevOut, out int txRefId))
                {
                    TxRef txRef = TxSet.Find(txRefId);

                    txRef.SpendingBlock = txRef.SpendingBlock ?? block;
                    txRef.ReservedBy = txRef.ReservedBy ?? Guid.Empty;
                    txRef.ReservedOn = txRef.ReservedOn ?? DateTime.UtcNow;

                    this.dbContext.SaveChanges();

                    if (block.HasValue)
                    {
                        this.addressLookup.EvictUtxo(input.PrevOut);
                    }
                }
            }
        }

        private static Expression<Func<TxRef, bool>> GetUtxoPredicate(int accountReference, int tip, int confirmations)
        {
            return txref =>
                    !txref.SpendingBlock.HasValue &&
                    !txref.ReservedBy.HasValue &&
                    !txref.ReservedOn.HasValue &&
                    (confirmations == 0 || txref.ArrivalBlock <= tip - confirmations) &&
                    txref.Address.Account.Index == accountReference;
        }

        internal long GetBalance(int accountReference, int confirmations = default)
        {
            var tip = this.chain.Tip.Height;

            return this.dbContext.Set<TxRef>()
                .Where(GetUtxoPredicate(accountReference, tip, confirmations))
                .Sum(e => e.Amount);
        }

        internal IEnumerable<UnspentOutputReference> GetSpendableTransactionsInAccount(int accountReference, int confirmations = default)
        {
            var tip = this.chain.Tip.Height;

            return this.dbContext.Set<TxRef>()
                .Include(t => t.Address)
                .Where(GetUtxoPredicate(accountReference, tip, confirmations))
                .Select(txref => new UnspentOutputReference
                {
                    Address = txref.Address,
                    Transaction = txref,
                    Confirmations = tip - txref.ArrivalBlock.Value
                });
        }
        internal void ReserveTransactionFunds(Transaction transaction, Guid reservationSourceId)
        {
            DbSet<TxRef> refs = this.dbContext.Set<TxRef>();
            DateTime now = DateTime.UtcNow;

            foreach (TxIn input in transaction.Inputs)
            {
                if (this.addressLookup.TryGetUtxoId(input.PrevOut, out int txRefId))
                {
                    TxRef txref = refs.Find(txRefId);

                    if (txref != null)
                    {
                        txref.ReservedBy = reservationSourceId;
                        txref.ReservedOn = now;
                    }
                }
            }

            this.dbContext.SaveChanges();
        }

        internal bool IsReserveIdInUse(Guid reserveId)
        {
            return this.dbContext.Set<TxRef>().Any(t => t.ReservedBy == reserveId);
        }

        internal ExtKey GetExtKey(int accountReference, string walletPassword, bool cacheSecret)
        {
            HdAccount hdAccount = this.dbContext.Set<HdAccount>()
                .FirstOrDefault(acc => acc.Index == accountReference);
            var privateKey = Key.Parse(hdAccount.Seed, walletPassword, this.network);
            return new ExtKey(privateKey, hdAccount.ChainCode);
        }
    }
}
