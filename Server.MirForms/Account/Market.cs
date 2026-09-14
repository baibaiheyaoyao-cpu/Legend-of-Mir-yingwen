using Server.MirDatabase;
using Server.MirEnvir;
using System.Data;

namespace Server.Database
{
    public partial class Market : Form
    {
        public Market()
        {
            InitializeComponent();
            LoadMarket();
        }

        #region Load Market
        private void LoadMarket()
        {
            // Clear existing items in the MarketListing
            MarketListing.Items.Clear();

            // Retrieve all auctions from the user database and filter out expired or sold items
            List<AuctionInfo> allAuctions = Envir.Main.Auctions.ToList();
            List<AuctionInfo> activeAuctions = allAuctions.Where(a => !a.Expired && !a.Sold).ToList();

            // Update the TotalItemsLabel with the count of active items
            TotalItemsLabel.Text = $"物品总数: {activeAuctions.Count}";

            // Retrieve search keyword from SearchBox and convert to lowercase for case-insensitive search
            string searchKeyword = SearchBox.Text.Trim().ToLower();
            bool filterByPlayer = FilterByPlayer.Checked;

            // Variables to track filtered player name and count
            string filteredPlayerName = string.Empty;
            int filteredPlayerItemCount = 0;

            // Apply filters based on checkboxes and search keyword
            var filteredAuctions = activeAuctions.Where(a =>
            {
                bool matchesSearch = string.IsNullOrEmpty(searchKeyword) ||
                                     (FilterByItem.Checked && a.Item?.Info.FriendlyName.ToLower().Contains(searchKeyword) == true) ||
                                     (filterByPlayer && a.SellerInfo?.Name.ToLower().Contains(searchKeyword) == true);

                // Count items by the filtered player if the player filter is applied
                if (filterByPlayer && a.SellerInfo?.Name.ToLower() == searchKeyword)
                {
                    filteredPlayerName = a.SellerInfo.Name; // Capture exact player name for label
                    filteredPlayerItemCount++;
                }

                return matchesSearch;
            }).ToList();

            // Update TotalItemsOwnedLabel based on the player filter results
            if (filterByPlayer && !string.IsNullOrEmpty(filteredPlayerName))
            {
                TotalItemsOwnedLabel.Text = $"该玩家物品总数: {filteredPlayerName} ({filteredPlayerItemCount})";
            }
            else
            {
                TotalItemsOwnedLabel.Text = "该玩家物品总数: ";
            }

            // Iterate over each filtered auction listing and add it to the MarketListing
            foreach (var listing in filteredAuctions)
            {
                // Create a new ListViewItem with the item's name or "Unknown Item" as a fallback
                ListViewItem item = new ListViewItem(listing.Item?.Info.FriendlyName ?? "未知物品");

                // Add sub-items for UID, Price, Seller, and Expiry
                item.SubItems.Add(listing.AuctionID.ToString()); // Auction ID
                item.SubItems.Add(listing.Price.ToString("N0")); // Gold Price
                item.SubItems.Add(listing.SellerInfo?.Name ?? "未知卖家"); // Seller Name
                item.SubItems.Add(listing.ConsignmentDate.AddDays(7).ToString("g")); // Expiry date assuming 7 days from consignment

                // Add the item to the MarketListing
                MarketListing.Items.Add(item);
            }
        }
        #endregion

        #region Refresh Listings Button
        private void RefreshListings_Click(object sender, EventArgs e)
        {
            LoadMarket();
        }
        #endregion

        #region Expire Listings Button
        private void ExpireListingButton_Click(object sender, EventArgs e)
        {
            // Ensure an item is selected in the MarketListing
            if (MarketListing.SelectedItems.Count == 0)
            {
                MessageBox.Show("请先选择一个要过期的寄售。");
                return;
            }

            // Get the selected auction ID from the ListView
            var selectedItem = MarketListing.SelectedItems[0];
            if (!ulong.TryParse(selectedItem.SubItems[1].Text, out ulong auctionId))
            {
                MessageBox.Show("所选拍卖ID无效。");
                return;
            }

            // Find the auction in Envir.Main.Auctions by AuctionID
            var auction = Envir.Main.Auctions.FirstOrDefault(a => a.AuctionID == auctionId);
            if (auction == null)
            {
                MessageBox.Show("未找到该寄售记录。");
                return;
            }

            // Mark the listing as expired
            auction.Expired = true;

            // Refresh the MarketListing to reflect the update
            LoadMarket();

            MessageBox.Show("寄售已成功标记为过期。");
        }
        #endregion

        #region Delete Listings Button
        private void DeleteListingButton_Click(object sender, EventArgs e)
        {
            if (MarketListing.SelectedItems.Count == 0)
            {
                MessageBox.Show("请先选择一个要删除的寄售。");
                return;
            }

            var selectedItem = MarketListing.SelectedItems[0];
            if (!ulong.TryParse(selectedItem.SubItems[1].Text, out ulong auctionId))
            {
                MessageBox.Show("所选拍卖ID无效。");
                return;
            }

            var auction = Envir.Main.Auctions.FirstOrDefault(a => a.AuctionID == auctionId);
            if (auction == null)
            {
                MessageBox.Show("未找到该寄售记录。");
                return;
            }

            var confirmResult = MessageBox.Show(
                "确定要删除该寄售吗?\n\n" +
                "警告: 此操作不可逆，物品和标价都不会返还给玩家。",
                "确认删除",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (confirmResult != DialogResult.Yes) return;

            string reason = ReasonTextBox.Text.Trim();
            if (auction.SellerInfo?.Player != null && !string.IsNullOrEmpty(reason))
            {
                auction.SellerInfo.Player.ReceiveChat(reason, ChatType.Announcement);
            }

            // Remove the auction from the main auction list and the seller's account
            Envir.Main.Auctions.Remove(auction);
            auction.SellerInfo.AccountInfo.Auctions.Remove(auction);

            LoadMarket();

            MessageBox.Show("寄售已删除，并已通知卖家。");
        }
        #endregion
    }
}
