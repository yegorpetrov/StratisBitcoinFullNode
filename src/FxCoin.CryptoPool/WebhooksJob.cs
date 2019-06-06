namespace FxCoin.CryptoPool
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
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
                this.runner.Run<DbWalletContext, HttpClient, NodeSettings>((ctx, client, settings) =>
                {
                    var tip = chain.Tip.Height;

                    var hooks = ctx.Set<TxWebhook>()
                        .Include(wh => wh.TxRef)
                        .Where(h => h.SendOn.HasValue && h.SendOn <= DateTime.UtcNow && h.TxRef.ArrivalBlock <= tip - settings.ConfirmationsRecommended)
                        .OrderBy(e => e.Id)
                        .Take(1000);

                    foreach (var hook in hooks)
                    {
                        ProcessWebhook(client, hook, stoppingToken).Wait();
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

        private async Task ProcessWebhook(HttpClient client, TxWebhook hook, CancellationToken stoppingToken)
        {
            this.logger.LogTrace($"Processing webhook {hook.Id}");

            hook.SendOn = DateTime.UtcNow + (2 * (DateTime.UtcNow - hook.Created)); // defer

            var uri = new UriBuilder("http://example.com")
            {
                Query = $"txid={hook.TxRef.TxId}&block={hook.TxRef.ArrivalBlock}&amount={hook.TxRef.Amount}"
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
