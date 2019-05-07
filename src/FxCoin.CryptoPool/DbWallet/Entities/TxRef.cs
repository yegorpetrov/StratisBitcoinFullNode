namespace FxCoin.CryptoPool.DbWallet.Entities
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Text;

    public class TxRef : Base
    {
        public HdAddress Address { get; set; }

        public int AddressId { get; set; }

        public string TxId { get; set; }

        public int Index { get; set; }

        public long Amount { get; set; }

        public int? ArrivalBlock { get; set; }

        public int? SpendingBlock { get; set; }

        public Guid? ReservedBy { get; set; }

        public DateTime? ReservedOn { get; set; }

        //public byte[] ScriptPubKey { get; set; }
    }
}
