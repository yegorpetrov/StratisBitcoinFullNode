using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Policy;
using FxCoin.CryptoPool.DbWallet.Entities;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;
using TracerAttributes;

namespace FxCoin.CryptoPool.DbWallet.TransactionHandler
{
    /// <summary>
    /// A handler that has various functionalities related to transaction operations.
    /// </summary>
    /// <remarks>
    /// This will uses the <see cref="IWalletFeePolicy"/> and the <see cref="TransactionBuilder"/>.
    /// TODO: Move also the broadcast transaction to this class
    /// TODO: Implement lockUnspents
    /// TODO: Implement subtractFeeFromOutputs
    /// </remarks>
    public class WalletTransactionHandler
    {
        private readonly ILogger logger;

        private readonly Network network;

        private readonly DbWalletManager walletManager;

        private readonly WalletFeePolicy walletFeePolicy;
        
        public WalletTransactionHandler(
            ILoggerFactory loggerFactory,
            DbWalletManager walletManager,
            WalletFeePolicy walletFeePolicy,
            IBlockStore blockStore,
            Network network)
        {
            this.network = network;
            this.walletManager = walletManager;
            this.walletFeePolicy = walletFeePolicy;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <inheritdoc />
        public Transaction BuildTransaction(TransactionBuildContext context)
        {
            this.InitializeTransactionBuilder(context);

            if (context.Shuffle)
                context.TransactionBuilder.Shuffle();

            Transaction transaction = context.TransactionBuilder.BuildTransaction(context.Sign);

            if (context.TransactionBuilder.Verify(transaction, out TransactionPolicyError[] errors))
                return transaction;

            string errorsMessage = string.Join(" - ", errors.Select(s => s.ToString()));
            this.logger.LogError($"Build transaction failed: {errorsMessage}");
            throw new WalletException($"Could not build the transaction. Details: {errorsMessage}");
        }

        /// <summary>
        /// Initializes the context transaction builder from information in <see cref="TransactionBuildContext"/>.
        /// </summary>
        /// <param name="context">Transaction build context.</param>
        protected virtual void InitializeTransactionBuilder(TransactionBuildContext context)
        {
            Guard.NotNull(context, nameof(context));
            Guard.NotNull(context.Recipients, nameof(context.Recipients));
            Guard.NotNull(context.AccountReference, nameof(context.AccountReference));

            this.AddRecipients(context);
            this.AddOpReturnOutput(context);
            this.AddCoins(context);
            this.AddSecrets(context);
            this.FindChangeAddress(context);
            this.AddFee(context);
        }

        /// <summary>
        /// Loads all the private keys for each of the <see cref="HdAddress"/> in <see cref="TransactionBuildContext.UnspentOutputs"/>
        /// </summary>
        /// <param name="context">The context associated with the current transaction being built.</param>
        protected void AddSecrets(TransactionBuildContext context)
        {
            if (!context.Sign)
                return;

            ExtKey seedExtKey = this.walletManager.GetExtKey(context.AccountReference, context.WalletPassword, context.CacheSecret);
            
            var signingKeys = new HashSet<ISecret>();
            var added = new HashSet<HdAddress>();
            foreach (UnspentOutputReference unspentOutputsItem in context.UnspentOutputs)
            {
                if (added.Contains(unspentOutputsItem.Address))
                    continue;

                HdAddress address = unspentOutputsItem.Address;
                ExtKey addressExtKey = seedExtKey.Derive(new KeyPath($"m/44'/{this.network.Consensus.CoinType}'/{context.AccountReference}'/{(address.IsChange ? 1 : 0)}/{address.Index}"));
                BitcoinExtKey addressPrivateKey = addressExtKey.GetWif(this.network);
                signingKeys.Add(addressPrivateKey);
                added.Add(unspentOutputsItem.Address);
            }

            context.TransactionBuilder.AddKeys(signingKeys.ToArray());
        }

        /// <summary>
        /// Find the next available change address.
        /// </summary>
        /// <param name="context">The context associated with the current transaction being built.</param>
        protected void FindChangeAddress(TransactionBuildContext context)
        {
            // Get an address to send the change to.
            context.ChangeScriptPubKey = this.walletManager.GetAddress(context.AccountReference, true).scriptPubKey;
            context.TransactionBuilder.SetChange(new Script(context.ChangeScriptPubKey));
        }

        /// <summary>
        /// Find all available outputs (UTXO's) that belong to <see cref="WalletAccountReference.AccountName"/>.
        /// Then add them to the <see cref="TransactionBuildContext.UnspentOutputs"/>.
        /// </summary>
        /// <param name="context">The context associated with the current transaction being built.</param>
        protected void AddCoins(TransactionBuildContext context)
        {
            context.UnspentOutputs = this.walletManager.GetSpendableTransactionsInAccount(context.AccountReference, context.MinConfirmations).ToList();

            if (!context.UnspentOutputs.Any())
            {
                throw new InsufficientFundsException($"No spendable transactions found");
            }

            // Get total spendable balance in the account.
            long balance = context.UnspentOutputs.Sum(t => t.Transaction.Amount);
            long totalToSend = context.Recipients.Sum(s => s.Amount);
            if (balance < totalToSend)
            {
                throw new InsufficientFundsException($"Not enough funds: {balance} < {totalToSend}");
            }

            Money sum = 0;
            var coins = new List<Coin>();

            foreach (UnspentOutputReference item in context.UnspentOutputs
                .OrderByDescending(a => a.Confirmations > 0)
                .ThenByDescending(a => a.Transaction.Amount))
            {
                // If the total value is above the target
                // then it's safe to stop adding UTXOs to the coin list.
                // The primary goal is to reduce the time it takes to build a trx
                // when the wallet is bloated with UTXOs.
                if (sum > totalToSend)
                    break;

                var spkBytes = this.network.GenerateAddressAndSpk(item.Account.ExtPubKey, item.Address.Index, item.Address.IsChange).spk;
                coins.Add(new Coin(uint256.Parse(item.Transaction.TxId), (uint)item.Transaction.Index, item.Transaction.Amount, new Script(spkBytes)));
                sum += item.Transaction.Amount;
            }

            // All the UTXOs are added to the builder without filtering.
            // The builder then has its own coin selection mechanism
            // to select the best UTXO set for the corresponding amount.
            // To add a custom implementation of a coin selection override
            // the builder using builder.SetCoinSelection().

            context.TransactionBuilder.AddCoins(coins);
        }

        /// <summary>
        /// Add recipients to the <see cref="TransactionBuilder"/>.
        /// </summary>
        /// <param name="context">The context associated with the current transaction being built.</param>
        /// <remarks>
        /// Add outputs to the <see cref="TransactionBuilder"/> based on the <see cref="Recipient"/> list.
        /// </remarks>
        protected virtual void AddRecipients(TransactionBuildContext context)
        {
            if (context.Recipients.Any(a => a.Amount == Money.Zero))
                throw new WalletException("No amount specified.");

            if (context.Recipients.Any(a => a.SubtractFeeFromAmount))
                throw new NotImplementedException("Substracting the fee from the recipient is not supported yet.");

            foreach (Recipient recipient in context.Recipients)
                context.TransactionBuilder.Send(recipient.ScriptPubKey, recipient.Amount);
        }

        /// <summary>
        /// Use the <see cref="FeeRate"/> from the <see cref="walletFeePolicy"/>.
        /// </summary>
        /// <param name="context">The context associated with the current transaction being built.</param>
        protected void AddFee(TransactionBuildContext context)
        {
            Money fee;
            Money minTrxFee = new Money(this.network.MinTxFee, MoneyUnit.Satoshi);

            // If the fee hasn't been set manually, calculate it based on the fee type that was chosen.
            if (context.TransactionFee == null)
            {
                FeeRate feeRate = context.OverrideFeeRate ?? this.walletFeePolicy.GetFeeRate(context.FeeType.ToConfirmations());
                fee = context.TransactionBuilder.EstimateFees(feeRate);

                // Make sure that the fee is at least the minimum transaction fee.
                fee = Math.Max(fee, minTrxFee);
            }
            else
            {
                if (context.TransactionFee < minTrxFee)
                {
                    throw new WalletException($"Not enough fees. The minimun fee is {minTrxFee}.");
                }

                fee = context.TransactionFee;
            }

            context.TransactionBuilder.SendFees(fee);
            context.TransactionFee = fee;
        }

        /// <summary>
        /// Add extra unspendable output to the transaction if there is anything in OpReturnData.
        /// </summary>
        /// <param name="context">The context associated with the current transaction being built.</param>
        protected void AddOpReturnOutput(TransactionBuildContext context)
        {
            if (string.IsNullOrEmpty(context.OpReturnData)) return;

            byte[] bytes = Encoding.UTF8.GetBytes(context.OpReturnData);
            // TODO: Get the template from the network standard scripts instead
            Script opReturnScript = TxNullDataTemplate.Instance.GenerateScriptPubKey(bytes);
            context.TransactionBuilder.Send(opReturnScript, context.OpReturnAmount ?? Money.Zero);
        }
    }

    public class TransactionBuildContext
    {
        /// <summary>
        /// Initialize a new instance of a <see cref="TransactionBuildContext"/>
        /// </summary>
        /// <param name="network">The network for which this transaction will be built.</param>
        public TransactionBuildContext(Network network)
        {
            this.TransactionBuilder = new TransactionBuilder(network);
            this.Recipients = new List<Recipient>();
            this.WalletPassword = string.Empty;
            this.FeeType = FeeType.Medium;
            this.MinConfirmations = 1;
            this.AllowOtherInputs = false;
            this.Sign = true;
            this.CacheSecret = true;
        }

        /// <summary>
        /// The wallet account to use for building a transaction.
        /// </summary>
        public int AccountReference { get; set; }

        /// <summary>
        /// The recipients to send Bitcoin to.
        /// </summary>
        public IEnumerable<Recipient> Recipients { get; set; }

        /// <summary>
        /// An indicator to estimate how much fee to spend on a transaction.
        /// </summary>
        /// <remarks>
        /// The higher the fee the faster a transaction will get in to a block.
        /// </remarks>
        public FeeType FeeType { get; set; }

        /// <summary>
        /// The minimum number of confirmations an output must have to be included as an input.
        /// </summary>
        public int MinConfirmations { get; set; }

        /// <summary>
        /// Coins that are available to be spent.
        /// </summary>
        public List<UnspentOutputReference> UnspentOutputs { get; set; }

        /// <summary>
        /// The builder used to build the current transaction.
        /// </summary>
        public readonly TransactionBuilder TransactionBuilder;

        /// <summary>
        /// The change address, where any remaining funds will be sent to.
        /// </summary>
        /// <remarks>
        /// A Bitcoin has to spend the entire UTXO, if total value is greater then the send target
        /// the rest of the coins go in to a change address that is under the senders control.
        /// </remarks>
        public byte[] ChangeScriptPubKey { get; set; }

        /// <summary>
        /// The total fee on the transaction.
        /// </summary>
        public Money TransactionFee { get; set; }

        /// <summary>
        /// The password that protects the wallet in <see cref="WalletAccountReference"/>.
        /// </summary>
        /// <remarks>
        /// TODO: replace this with System.Security.SecureString (https://github.com/dotnet/corefx/tree/master/src/System.Security.SecureString)
        /// More info (https://github.com/dotnet/corefx/issues/1387)
        /// </remarks>
        public string WalletPassword { get; set; }

        /// <summary>
        /// If false, allows unselected inputs, but requires all selected inputs be used.
        /// </summary>
        public bool AllowOtherInputs { get; set; }

        /// <summary>
        /// Allows the context to specify a <see cref="FeeRate"/> when building a transaction.
        /// </summary>
        public FeeRate OverrideFeeRate { get; set; }

        /// <summary>
        /// Shuffles transaction inputs and outputs for increased privacy.
        /// </summary>
        public bool Shuffle { get; set; }

        /// <summary>
        /// Optional data to be added as an extra OP_RETURN transaction output.
        /// </summary>
        public string OpReturnData { get; set; }

        /// <summary>
        /// Optional amount to add to the OP_RETURN transaction output.
        /// </summary>
        public Money OpReturnAmount { get; set; }

        /// <summary>
        /// Whether the transaction should be signed or not.
        /// </summary>
        public bool Sign { get; set; }

        /// <summary>
        /// Whether the secret should be cached for 5 mins after it is used or not.
        /// </summary>
        public bool CacheSecret { get; set; }
    }
}
