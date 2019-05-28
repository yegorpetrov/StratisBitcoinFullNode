using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using FxCoin.CryptoPool.Contracts;
using FxCoin.CryptoPool.DbWallet.Broadcasting;
using FxCoin.CryptoPool.DbWallet.TransactionHandler;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Controllers;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.RPC.Exceptions;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Interfaces;

namespace FxCoin.CryptoPool.DbWallet.Controllers
{
    public class DbWalletRpcController : FeatureController
    {
        private static Guid WalletPasswordKey = Guid.NewGuid();

        private readonly DbWalletManager manager;
        private readonly IBroadcasterManager broadcaster;
        private readonly IMemoryCache cache;
        private readonly WalletTransactionHandler handler;

        public DbWalletRpcController(
            ChainIndexer chainIndexer,
            IConsensusManager consensusManager,
            IFullNode fullNode,
            ILoggerFactory loggerFactory,
            Network network,
            DbWalletManager walletManager,
            IBroadcasterManager broadcaster,
            IMemoryCache cache,
            WalletTransactionHandler walletTransactionHandler) : base(fullNode: fullNode, consensusManager: consensusManager, chainIndexer: chainIndexer, network: network)
        {
            this.manager = walletManager;
            this.broadcaster = broadcaster;
            this.cache = cache;
            this.handler = walletTransactionHandler;
        }

        [ActionName("getnewaccount")]
        public object GetAccount(string password)
        {
            var mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
            ExtKey extendedKey = HdOperations.GetExtendedKey(mnemonic, string.Empty);
            string encryptedSeed = extendedKey.PrivateKey.GetEncryptedBitcoinSecret(password, this.Network).ToWif();
            int accountId = this.manager.CreateAccount(encryptedSeed, password, extendedKey.ChainCode);

            return new
            {
                mnemonic = mnemonic.ToString(),
                accountId
            };
        }

        [ActionName("getnewaddress")]
        public object GetNewAddress(string fromAccount)
        {
            int.TryParse(fromAccount, out int accountIndex);

            var address = this.manager.GetAddress(accountIndex, false);

            return new
            {
                Address = address.address,
                Index = address.index
            };
        }

        [ActionName("sendrawtransaction")]
        public async Task<uint256> SendTransactionAsync(string hex)
        {
            Transaction transaction = this.FullNode.Network.CreateTransaction(hex);
            await this.broadcaster.BroadcastTransactionAsync(transaction);
            this.manager.Process(transaction);
            uint256 hash = transaction.GetHash();

            return hash;
        }

        [ActionName("getbalance")]
        public decimal GetBalance(string fromAccount, int minConfirmations = 0)
        {
            int.TryParse(fromAccount, out int accountIndex);

            return this.manager.GetBalance(accountIndex, minConfirmations);
        }

        [ActionName("gettransaction")]
        public GetTransactionResponse GetTransaction(string txid)
        {
            return new GetTransactionResponse
            {
                Fee = 0.0m, // TODO
                TxId = txid
            };
        }

        [ActionName("walletpassphrase")]
        public bool UnlockWallet(string passphrase, int seconds)
        {
            try
            {
                this.manager.GetExtKey(default, passphrase, true);
                // TODO keep as secure string
                this.cache.Set(WalletPasswordKey, passphrase, DateTime.UtcNow.AddSeconds(seconds));
            }
            catch (SecurityException se)
            {
                throw new RPCServerException(RPCErrorCode.RPC_INVALID_REQUEST, se.Message);
            }
            return true;
        }

        [ActionName("walletlock")]
        public bool LockWallet()
        {
            this.cache.Remove(WalletPasswordKey);
            return true;
        }

        [ActionName("sendmany")]
        public async Task<object> SendManyAsync(string fromAccount, string addressesJson, int minConf = 1, string comment = null, string subtractFeeFromJson = null, bool isReplaceable = false, int? confTarget = null, string estimateMode = "UNSET", Guid? reservationId = default)
        {
            int.TryParse(fromAccount, out int accountIndex);

            var addresses = new Dictionary<string, decimal>();
            try
            {
                // Outputs addresses are keyvalue pairs of address, amount. Translate to Receipient list.
                addresses = JsonConvert.DeserializeObject<Dictionary<string, decimal>>(addressesJson);
            }
            catch (JsonSerializationException ex)
            {
                throw new RPCServerException(RPCErrorCode.RPC_PARSE_ERROR, ex.Message);
            }

            var ctx = new TransactionBuildContext(this.Network)
            {
                AccountReference = accountIndex,
                MinConfirmations = minConf,
                OverrideFeeRate = null, // TODO
                Shuffle = false,
                Recipients = addresses.Select(rcp => new Recipient
                {
                    Amount = new Money(rcp.Value, MoneyUnit.BTC),
                    ScriptPubKey = BitcoinAddress.Create(rcp.Key, this.Network).ScriptPubKey
                }),
                WalletPassword = this.cache.Get<string>(WalletPasswordKey)
            };

            var transaction = this.handler.BuildTransaction(ctx);

            if (reservationId.HasValue)
            {
                if (this.manager.IsReserveIdInUse(reservationId.Value))
                {
                    throw new InvalidOperationException($"Request id is in use: {reservationId}");
                }

                this.manager.ReserveTransactionFunds(transaction, reservationId.Value);
            }
            else // broadcast as is if no reservation id was set
            {
                await SendTransactionAsync(transaction.ToHex());
            }

            return reservationId.HasValue ? (object)new TxBuilderResponse
            {
                TxHash = transaction.GetHash().ToString(),
                TxHex = transaction.ToHex()
            } : transaction.GetHash().ToString();
        }

        [ActionName("walletpassphrasechange")]
        public void WalletPassphraseChange(string oldPassphrase, string newPassphrase)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(oldPassphrase)
                    || string.IsNullOrWhiteSpace(newPassphrase))
                {
                    throw new RPCServerException(RPCErrorCode.RPC_INVALID_PARAMETER, "passphrase can not be empty");
                }

                this.manager.WalletPassphraseChange(oldPassphrase, newPassphrase);

                this.cache.Remove(WalletPasswordKey);
            }
            catch (SecurityException se)
            {
                throw new RPCServerException(RPCErrorCode.RPC_INVALID_REQUEST, se.Message);
            }
        }       
    }
}
