namespace FxCoin.CryptoPool.DbWallet.Entities
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public class HdAddress : Base
    {
        public HdAccount Account { get; set; }

        public int AccountId { get; set; }

        public string Address { get; set; }

        public bool IsChange { get; set; }

        public int Index { get; set; }

        /// <summary>
        /// Whether this address had any txrefs assigned to it
        /// </summary>
        /// <remarks>Denormalized for performance</remarks>
        public bool IsInUse { get; set; }

        public override string ToString()
        {
            return $"Addr {this.Address} idx{this.Index} {(this.IsChange ? "change" : string.Empty)}";
        }
    }
}
