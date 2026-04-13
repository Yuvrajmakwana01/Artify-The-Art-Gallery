using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repository.Models;

namespace Repository.Interfaces
{
    public interface IArtistInterface
    {
        /// <summary>
        /// Get artist profile by ID
        /// </summary>
        Task<t_ArtistProfile> GetArtistById(int artistId);

        /// <summary>
        /// Update artist profile (biography, name, URLs, cover image, active status)
        /// Returns:
        /// 1 = Success
        /// 0 = Not Found
        /// -1 = Error
        /// </summary>
        Task<int> EditArtistProfile(t_ArtistProfile model);
        Task<int> Register(t_Artist user);
        Task<t_Artist> Login(vm_Login user);
    }
}