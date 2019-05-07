namespace FxCoin.CryptoPool.DbWallet.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using FxCoin.CryptoPool.Contracts;
    using FxCoin.CryptoPool.DbWallet.Broadcasting;
    using FxCoin.CryptoPool.DbWallet.TransactionHandler;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Caching.Memory;
    using NBitcoin;
    using NBitcoin.DataEncoders;
    using Newtonsoft.Json.Linq;
    using Stratis.Bitcoin.Features.Wallet;
    using Stratis.Bitcoin.Interfaces;

    [Route("api/[controller]")]
    public class DbWalletController : Controller
    {
        private static Guid WalletPasswordKey = Guid.NewGuid();

        private readonly DbWalletManager manager;
        private readonly Network network;
        private readonly WalletTransactionHandler handler;
        private readonly IBroadcasterManager broadcaster;
        private readonly IMemoryCache cache;

        public DbWalletController(
            DbWalletManager manager,
            Network network,
            WalletTransactionHandler handler,
            IBroadcasterManager broadcaster,
            IMemoryCache cache)
        {
            this.manager = manager;
            this.network = network;
            this.handler = handler;
            this.broadcaster = broadcaster;
            this.cache = cache;
        }

        [HttpPost("newacc")]
        public async Task<IActionResult> GetNewAccount([FromBody] string password)
        {
            var mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
            ExtKey extendedKey = HdOperations.GetExtendedKey(mnemonic, string.Empty);
            string encryptedSeed = extendedKey.PrivateKey.GetEncryptedBitcoinSecret(password, this.network).ToWif();
            int accountId = this.manager.CreateAccount(encryptedSeed, password, extendedKey.ChainCode);

            return Ok(new
            {
                mnemonic = mnemonic.ToString(),
                accountId
            });
        }

        [HttpPost("{accountIndex}/freshaddress")]
        public (string address, int index) GetNewAddress([FromRoute] int accountIndex, [FromQuery] bool forChange = false)
        {
            var address = this.manager.GetAddress(accountIndex, forChange);

            return (
                address.address,
                address.index
            );
        }

        [HttpPost("{accountIndex}/txbuilder")]
        public async Task<TxBuilderResponse> BuildTx(
            [FromRoute] int accountIndex,
            [FromBody] TxBuilderRequest request)
        {
            var ctx = new TransactionBuildContext(this.network)
            {
                AccountReference = accountIndex,
                MinConfirmations = request.MinConfirmations,
                OverrideFeeRate = request.SatoshiPerKbFee.HasValue ? new FeeRate(request.SatoshiPerKbFee) : null,
                Shuffle = false,
                Recipients = request.Recipients.Select(rcp => new Recipient
                {
                    Amount = new Money(rcp.Value),
                    ScriptPubKey = BitcoinAddress.Create(rcp.Key, this.network).ScriptPubKey
                }),
                WalletPassword = request.WalletPassword
            };

            var transaction = handler.BuildTransaction(ctx);

            if (request.ReservationId.HasValue)
            {
                if (this.manager.IsReserveIdInUse(request.ReservationId.Value))
                {
                    throw new InvalidOperationException($"Request id is in use: {request.ReservationId}");
                }

                this.manager.ReserveTransactionFunds(transaction, request.ReservationId.Value);
            }
            else // broadcast as is if no reservation id was set
            {
                await Broadcast(transaction.ToHex());
            }

            return new TxBuilderResponse
            {
                TxHash = transaction.GetHash().ToString(),
                TxHex = transaction.ToHex()
            };
        }

        [HttpPost("broadcaster")]
        public async Task Broadcast([FromBody] string tx)
        {
            var transaction = new Transaction();
            transaction.FromBytes(Encoders.Hex.DecodeData(tx));
            await broadcaster.BroadcastTransactionAsync(transaction);
            this.manager.Process(transaction);
        }

        [HttpGet("{accountIndex}/balance")]
        public async Task<long> GetBalance([FromRoute] int accountIndex, [FromQuery] int confirmations = 2)
        {
            return await Task.FromResult(this.manager.GetBalance(accountIndex, confirmations));
        }

        [HttpPost("jsonrpc")]
        public async Task<object> LegacyJsonRpcApi([FromBody] JObject rpc, [FromServices] IBlockStore blockStore)
        {
            object result = null;
            string error = null;
            JToken args = rpc["params"];

            switch (rpc["method"].Value<string>())
            {
                case "getbalance":
                    result = new Money(await GetBalance(0, 0)).ToDecimal(MoneyUnit.BTC);
                    break;
                case "sendmany":
                    result = BuildTx(0, new TxBuilderRequest
                    {
                        MinConfirmations = 0,
                        Recipients = args[1]
                            .ToObject<IDictionary<string, decimal>>()
                            .ToDictionary(kv => kv.Key, kv => new Money(kv.Value, MoneyUnit.BTC).Satoshi),
                        ReservationId = null,
                        SatoshiPerKbFee = null,
                        WalletPassword = cache.Get<string>(WalletPasswordKey)
                    }).Result.TxHash;
                    break;
                case "walletpassphrase":
                    var password = (string)args[0];
                    var seconds = (int)args[1];
                    cache.Set(WalletPasswordKey, password, DateTime.UtcNow.AddSeconds(seconds));
                    result = true;
                    break;
                case "gettransaction":
                    var txid = (string)args[0];
                    result = new GetTransactionResponse
                    {
                        Fee = 0.0m, // TODO
                        TxId = txid
                    };
                    break;
                case "getnewaddress":
                    result = GetNewAddress(0).address;
                    break;
            }

            return new
            {
                id = rpc["id"]?.Value<object>(),
                error,
                result
            };
        }
    }
}
