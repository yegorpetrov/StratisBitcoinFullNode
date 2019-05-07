namespace FxCoin.CryptoPool.DbWallet.Entities.Context
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Design;

    public class DesignTimeFactory : IDesignTimeDbContextFactory<DbWalletContext>
    {
        public DbWalletContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<DbWalletContext>();
            options.UseSqlServer("dummy"); // it's gonna use the snapshot anyway
            return new DbWalletContext(options.Options);
        }
    }
}
