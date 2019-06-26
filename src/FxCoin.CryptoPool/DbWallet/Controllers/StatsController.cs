using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Stratis.Bitcoin.Utilities;

namespace FxCoin.CryptoPool.DbWallet.Controllers
{
    [Route("api/[controller]")]
    public class StatsController : Controller
    {
        [HttpGet("current")]
        public IActionResult GetCurrentStats([FromServices] INodeStats nodeStats)
        {
            return Ok(nodeStats.GetStats());
        }

        [HttpGet("benchmark")]
        public IActionResult GetIbdBenchmark([FromServices] INodeStats nodeStats)
        {
            return Ok(nodeStats.GetBenchmark());
        }
    }
}
