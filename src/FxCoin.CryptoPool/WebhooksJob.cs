namespace FxCoin.CryptoPool
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net.Http;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using FxCoin.CryptoPool.DbWallet;
    using FxCoin.CryptoPool.DbWallet.Entities;
    using FxCoin.CryptoPool.DbWallet.Entities.Context;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using NBitcoin;
    using Stratis.Bitcoin.Configuration;
    using Stratis.Bitcoin.Signals;

    public class WebhooksJob : BackgroundService
    {
        private readonly ScopeRunner runner;
        private readonly ChainIndexer chain;
        private readonly ISignals signals;
        private readonly ILogger logger;

        public WebhooksJob(ScopeRunner runner, ILoggerFactory loggerFactory, ChainIndexer chain, ISignals signals)
        {
            this.runner = runner;
            this.chain = chain;
            this.signals = signals;
            this.logger = loggerFactory.CreateLogger<WebhooksJob>();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var token = this.signals.Subscribe<TxRefEvent>(ev => RegisterTxRef(ev, stoppingToken));

            while (!stoppingToken.IsCancellationRequested)
            {
                this.runner.Run<DbWalletContext, NodeSettings>((ctx, settings) =>
                {
                    var tip = chain.Tip.Height;

                    var hooks = ctx.Set<TxWebhook>()
                        .Include(wh => wh.TxRef)
                        .Include(wh => wh.TxRef.Address)
                        .Where(h => h.SendOn.HasValue && h.SendOn <= DateTime.UtcNow && h.TxRef.ArrivalBlock < tip - 2)
                        .OrderBy(e => e.Id)
                        .Take(100);

                    using (var client = HttpClientFactory.Create())
                    {
                        foreach (var hook in hooks)
                        {
                            var webhookUri = settings.ConfigReader.GetOrDefault("webhookUri", string.Empty, this.logger);
                            var webhookSecret = settings.ConfigReader.GetOrDefault("webhookSecret", string.Empty, this.logger);
                            ProcessWebhook(client, hook, webhookUri, webhookSecret, stoppingToken).Wait();
                        }
                    }

                    try
                    {
                        this.logger.LogTrace($"Updating webhooks...");
                        ctx.SaveChangesAsync(stoppingToken).Wait();
                    }
                    catch (DbUpdateException e)
                    {
                        this.logger.LogError(e, $"Unable to update webhooks: {e.Message}");
                    }
                });

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }

            this.signals.Unsubscribe(token);
        }

        private void RegisterTxRef(TxRefEvent ev, CancellationToken token)
        {
            this.runner.Run<DbWalletContext>(ctx =>
            {
                ctx.Set<TxWebhook>().Add(new TxWebhook
                {
                    Created = DateTime.UtcNow,
                    SendOn = DateTime.UtcNow,
                    TxRefId = ev.TxRef.Id
                });

                ctx.SaveChangesAsync(token).Wait();
            });
        }

        private async Task ProcessWebhook(HttpClient client, TxWebhook hook, string baseUri, string secret, CancellationToken stoppingToken)
        {
            this.logger.LogTrace($"Processing webhook {hook.Id}");

            hook.SendOn = DateTime.UtcNow + (2 * (DateTime.UtcNow - hook.Created)); // defer

            if (string.IsNullOrEmpty(baseUri))
            {
                this.logger.LogWarning("Webhook uri isn't set (-webhookUri)");
                hook.Status = "Webhook uri isn't set";
                return;
            }

            var query = new
            {
                address = hook.TxRef.Address.Address,
                amount = new Money(hook.TxRef.Amount).ToDecimal(MoneyUnit.BTC).ToString(CultureInfo.InvariantCulture),
                txId = hook.TxRef.TxId
            };

            string control;

            using (var sha1 = SHA1.Create())
            {
                var bytes = Encoding.ASCII.GetBytes($"{query.address}{query.amount}{query.txId}{secret}");
                control = string.Join(string.Empty, sha1.ComputeHash(bytes).Select(b => b.ToString("x2")));
            }

            var uri = new UriBuilder(baseUri)
            {
                Query = $"txid={query.txId}&amount={query.amount}&address={query.address}&control={control}"
            };
            try
            {
                using (var rsp = await client.GetAsync(uri.Uri, stoppingToken))
                {
                    if (rsp.IsSuccessStatusCode)
                    {
                        this.logger.LogTrace($"Webhook {hook.Id} has been sent");

                        if (hook.TxRef.ArrivalBlock.HasValue)
                        {
                            this.logger.LogTrace($"Webhook {hook.Id} has been finalized");
                            hook.SendOn = default;
                        }
                    }
                    hook.Status = $"{(int)rsp.StatusCode} | {uri} | {await rsp.Content.ReadAsStringAsync()}";
                }
            }
            catch (Exception e)
            {
                this.logger.LogWarning($"Unable to send webhook {hook.Id}: {e.Message}");
                hook.Status = $"{default(int):000} | {uri} | {e.GetType().Name}: {e.Message}";
            }
        }
    }
}
