namespace FxCoin.CryptoPool
{
    using System;
    using System.Threading.Tasks;
    using FxCoin.CryptoPool.DbWallet;
    using NBitcoin;
    using Stratis.Bitcoin;
    using Stratis.Bitcoin.Builder;
    using Stratis.Bitcoin.Configuration;
    using Stratis.Bitcoin.Features.Api;
    //using Stratis.Bitcoin.Features.BlockStore;
    using Stratis.Bitcoin.Features.Consensus;
    using Stratis.Bitcoin.Features.MemoryPool;
    using Stratis.Bitcoin.Features.RPC;
    using Stratis.Bitcoin.Networks;
    using Stratis.Bitcoin.Utilities;

    internal class Program
    {
        private static async Task Main(string[] args)
        {
            var cfg = new TextFileConfiguration(args);

            NetworksSelector selector;

            switch (cfg.GetOrDefault("currency", "bitcoin"))
            {
                case "bitcoin":
                    selector = Networks.Bitcoin;
                    break;
                case "litecoin":
                    selector = new NetworksSelector(() => new LitecoinMain(), () => new LitecoinTest(), null);
                    break;
                default:
                    throw new NotImplementedException();
            }

            var nodeSettings = new NodeSettings(networksSelector: selector, args: args);

            IFullNode node = new FullNodeBuilder()
                .UseNodeSettings(nodeSettings)
                .UsePowConsensus()
                .UseMempool()
                .UseDbWallet(nodeSettings.ConfigReader.GetOrDefault("connectionstring", string.Empty))
                .UseApi()
                .AddRPC()
                .Build();

            if (node != null)
            {
                await node.RunAsync();
            }
        }
    }
}
