using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace FxCoin.CryptoPool.DbWallet.Entities.Context
{
    public class DbWalletContext : DbContext
    {
        public DbWalletContext(DbContextOptions<DbWalletContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<HdAccount>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Seed);
                entity.Property(e => e.ExtPubKey);
                entity.HasAlternateKey(e => e.Index);
            });

            builder.Entity<HdAddress>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Account);
                entity.Property(e => e.Address).HasMaxLength(64);
                entity.Property(e => e.IsChange);
                entity.Property(e => e.Index);
            });

            builder.Entity<TxRef>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Address);
                entity.Property(e => e.TxId).HasMaxLength(64);
                //entity.Property(e => e.ScriptPubKey).HasMaxLength(10000);
                entity.Property(e => e.Index);
                entity.Property(e => e.Amount);
                entity.Property(e => e.SpendingBlock);
                entity.Property(e => e.ArrivalBlock);
            });

            builder.Entity<TxWebhook>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.TxRef);
                entity.Property(e => e.Created);
                entity.Property(e => e.SendOn);
                entity.Property(e => e.Status);
            });
        }
    }
}
