namespace FxCoin.CryptoPool.DbWallet
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Extensions.DependencyInjection;

    public class ScopeRunner
    {
        private readonly IServiceProvider serviceProvider;

        public ScopeRunner(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        public void Run<S1>(Action<S1> action)
        {
            using (IServiceScope scope = this.serviceProvider.CreateScope())
            {
                action(scope.ServiceProvider.GetService<S1>());
            }
        }

        public void Run<S1, S2>(Action<S1, S2> action)
        {
            using (IServiceScope scope = this.serviceProvider.CreateScope())
            {
                action(
                    scope.ServiceProvider.GetService<S1>(),
                    scope.ServiceProvider.GetService<S2>());
            }
        }

        public void Run<S1, S2, S3>(Action<S1, S2, S3> action)
        {
            using (IServiceScope scope = this.serviceProvider.CreateScope())
            {
                action(
                    scope.ServiceProvider.GetService<S1>(),
                    scope.ServiceProvider.GetService<S2>(),
                    scope.ServiceProvider.GetService<S3>());
            }
        }

        public void Run<S1, S2, S3, S4>(Action<S1, S2, S3, S4> action)
        {
            using (IServiceScope scope = this.serviceProvider.CreateScope())
            {
                action(
                    scope.ServiceProvider.GetService<S1>(),
                    scope.ServiceProvider.GetService<S2>(),
                    scope.ServiceProvider.GetService<S3>(),
                    scope.ServiceProvider.GetService<S4>());
            }
        }
    }
}
