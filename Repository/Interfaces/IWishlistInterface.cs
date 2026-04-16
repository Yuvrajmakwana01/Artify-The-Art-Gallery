using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Repository.Models;

namespace Repository.Interfaces
{
    public interface IWishlistInterface
    {
        Task<List<t_Wishlist>> GetWishlistAsync(int buyerId);
        Task<bool> AddToWishlistAsync(int buyerId, int artworkId);
        Task<bool> RemoveFromWishlistAsync(int buyerId, int artworkId);
        Task<bool> ClearWishlistAsync(int buyerId);
        Task<bool> IsInWishlistAsync(int buyerId, int artworkId);
    }
}