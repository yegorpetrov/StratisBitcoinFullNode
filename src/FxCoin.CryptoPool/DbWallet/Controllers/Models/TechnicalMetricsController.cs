using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Utilities;

namespace FxCoin.CryptoPool.DbWallet.Controllers.Models
{
    [Route("api/[controller]")]
    public class TechnicalMetricsController : Controller
    {
        static readonly IDictionary<string, Regex> _parsers = new Dictionary<string, Regex>()
            {
                { "headers_height", new Regex(@"Headers.Height:\s+([0-9]+)") },
                { "consensus_height", new Regex(@"Consensus.Height:\s+([0-9]+)") },
                { "blockstore_height", new Regex(@"BlockStore.Height:\s+([0-9]+)") },
                { "consensus_chained_header_tree_size_megabytes", new Regex(@"Chained header tree size:\s+([\.0-9]+)") },
                { "consensus_unconsumed_blocks", new Regex(@"Unconsumed blocks:\s+([0-9]+)") },
                { "consensus_cache_filled_by_percentage", new Regex(@"Cache is filled by:\s+([\.0-9]+)\%") },
                { "consensus_downloading_blocks_queued", new Regex(@"Downloading blocks:\s+([0-9]+)\s+queued") },
                { "consensus_downloading_blocks_pending", new Regex(@"queued out of\s+([0-9]+)\s+pending") },
                { "puller_blocks_being_downloaded", new Regex(@"Blocks being downloaded:\s+([0-9]+)") },
                { "puller_queued_downloads", new Regex(@"Queued downloads:\s+([0-9]+)") },
                { "puller_avg_block_size_kilobytes", new Regex(@"Average block size:\s+([\.0-9]+)\s+KB") },
                { "puller_total_download_speed_kby_sec", new Regex(@"Total download speed:\s+([\.0-9]+)\s+KB/sec") },
                { "puller_avg_block_download_time_ms", new Regex(@"Average time to download a block:\s+([\.0-9]+)\s+ms") },
                { "puller_blocks_per_second_capability", new Regex(@"Amount of blocks node can download in 1 second:\s+([\.0-9]+)") },
                { "blockstore_batched", new Regex(@"([0-9]+)\s+batched blocks") },
                { "blockstore_queued", new Regex(@"([0-9]+)\s+queued blocks") },
                { "mempool_size", new Regex(@"MempoolSize:\s+([0-9]+)") },
                { "mempool_dynamicsize_kb", new Regex(@"DynamicSize:\s+([0-9]+)\s+kb") },
                { "mempool_orphan_size", new Regex(@"OrphanSize:\s+([0-9]+)") },
                { "lookup_spk", new Regex(@"ScriptPubKey to address id lookup size:\s+([0-9]+)") },
                { "lookup_utxo", new Regex(@"OutPoint to TxRef id lookup size:\s+([0-9]+)") }
            };

        /// <summary>
        /// Provides metrics for Prometheus by parsing the current node stats output
        /// </summary>
        /// <remarks>https://github.com/prometheus/docs/blob/master/content/docs/instrumenting/exposition_formats.md</remarks>
        [HttpGet]
        public IActionResult Get(
            [FromServices] INodeStats nodeStats,
            [FromServices] ILogger<TechnicalMetricsController> logger)
        {
            var stats = nodeStats.GetStats();

            var sb = new StringBuilder();

            foreach (var parser in _parsers.OrderBy(kv => kv.Key))
            {
                var result = parser.Value.Match(stats).Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(result))
                {
                    sb.AppendLine($"{parser.Key} {result}");
                }
                else
                {
                    logger.LogWarning($"No metrics data for {parser.Key}");
                }
            }

            return Ok(sb.ToString());
        }
    }
}
