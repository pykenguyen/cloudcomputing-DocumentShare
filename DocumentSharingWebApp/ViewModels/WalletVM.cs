using System.Collections.Generic;
using DocumentSharingWebApp.Models;

namespace DocumentSharingWebApp.ViewModels
{
    public class WalletVM
    {
        public decimal Coins { get; set; }
        public List<DocumentPurchase> Purchases { get; set; } = new();
        public List<Transaction> Transactions { get; set; } = new();
    }
}
