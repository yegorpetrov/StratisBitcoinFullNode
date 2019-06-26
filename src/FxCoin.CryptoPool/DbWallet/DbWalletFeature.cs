namespace FxCoin.CryptoPool.DbWallet
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using FxCoin.CryptoPool.DbWallet.Broadcasting;
    using FxCoin.CryptoPool.DbWallet.Controllers;
    using FxCoin.CryptoPool.DbWallet.Entities.Context;
    using FxCoin.CryptoPool.DbWallet.TransactionHandler;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using NBitcoin;
    using Stratis.Bitcoin.Builder;
    using Stratis.Bitcoin.Builder.Feature;
    using Stratis.Bitcoin.Configuration.Logging;
    using Stratis.Bitcoin.Features.BlockStore;
    using Stratis.Bitcoin.Features.MemoryPool;

    public sealed class DbWalletFeature : FullNodeFeature
    {
        private readonly WalletSyncService syncService;
        private readonly WebhooksJob webhooksJob;

        public DbWalletFeature(WalletSyncService syncService, WebhooksJob webhooksJob)
        {
            this.syncService = syncService;
            this.webhooksJob = webhooksJob;
        }

        public override Task InitializeAsync()
        {
            this.syncService.Initialize();
            this.webhooksJob.StartAsync(CancellationToken.None).Wait();
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            this.webhooksJob.StopAsync(CancellationToken.None).Wait();
            base.Dispose();
        }
    }

    public static class FullNodeBuilderDbWalletExtension
    {
        public static IFullNodeBuilder UseDbWallet(this IFullNodeBuilder fullNodeBuilder, string connectionString)
        {
            LoggingConfiguration.RegisterFeatureNamespace<DbWalletFeature>("dbwallet");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                .AddFeature<DbWalletFeature>()
                .DependOn<MempoolFeature>()
                .DependOn<BlockStoreFeature>()
                .FeatureServices(services =>
                {
                    services.AddDbContext<DbWalletContext>(options => options.UseSqlServer(connectionString));
                    services.AddSingleton<WalletSyncService>();
                    services.AddSingleton<ScopeRunner>();
                    services.AddSingleton<HdAddressLookup>();
                    services.AddScoped<DbWalletManager>();
                    services.AddScoped<StatsController>();
                    services.AddScoped<WalletTransactionHandler>();
                    services.AddScoped<WalletFeePolicy>();
                    services.AddSingleton<WebhooksJob>();
                    services.AddSingleton<IBroadcasterManager, FullNodeBroadcasterManager>();
                    services.AddSingleton<BroadcasterBehavior>();
                });
            });

            return fullNodeBuilder;
        }
    }
}
