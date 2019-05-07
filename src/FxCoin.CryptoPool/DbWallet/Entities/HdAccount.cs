namespace FxCoin.CryptoPool.DbWallet.Entities
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public class HdAccount : Base
    {
        public string Seed { get; set; }

        public string ExtPubKey { get; set; }

        public byte[] ChainCode { get; set; }

        public int Index { get; set; }

        public override string ToString()
        {
            return $"Acc id{this.Id} idx{this.Index}";
        }
    }
}
