using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FxCoin.CryptoPool.DbWallet.Entities;
using FxCoin.CryptoPool.DbWallet.Entities.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FxCoin.CryptoPool.DbWallet.Controllers.Models
{
    [Route("api/[controller]")]
    public class WalletMetricsController : Controller
    {
        [HttpGet("current")]
        public async Task<IActionResult> GetWalletMetricsAsync([FromServices] DbWalletContext dbContext, CancellationToken token)
        {
            var txRefs = dbContext.Set<TxRef>().AsNoTracking();
            var now = DateTime.UtcNow;
            var sb = new StringBuilder();

            var span = TimeSpan.FromMinutes(1);
            for (; span < TimeSpan.FromDays(7); span *= 4)
            {
                var reservations = await txRefs
                    .Where(t =>
                        t.ReservedBy.HasValue &&
                        t.ReservedBy != Guid.Empty &&
                        now - t.ReservedOn < span)
                    .ToArrayAsync(token);

                sb.AppendLine($"reservations_number{{span=\"{span.TotalMinutes}\"}} {reservations.Length}");
            }
            {
                var reservations = await txRefs
                    .Where(t =>
                        t.ReservedBy.HasValue &&
                        t.ReservedBy != Guid.Empty &&
                        now - t.ReservedOn >= span)
                    .ToArrayAsync(token);

                sb.AppendLine($"reservations_number{{span=\"+Inf\"}} {reservations.Length}");
            }

            var unspent = txRefs.Where(t => !t.ReservedBy.HasValue);
            sb.AppendLine($"unspent_entries_count {await unspent.CountAsync(token)}");
            sb.AppendLine($"unspent_entries_sum {await unspent.SumAsync(e => e.Amount, token)}");

            var spent = txRefs.Where(t => t.ReservedBy.HasValue);
            sb.AppendLine($"spent_entries_count {await spent.CountAsync(token)}");
            sb.AppendLine($"spent_entries_sum {await spent.SumAsync(e => e.Amount, token)}");

            sb.AppendLine($"newest_arrival_block_height {await txRefs.MaxAsync(e => e.ArrivalBlock, token)}");
            sb.AppendLine($"newest_spending_block_height {await txRefs.MaxAsync(e => e.SpendingBlock, token)}");

            return Ok(sb.ToString());
        }
    }
}
