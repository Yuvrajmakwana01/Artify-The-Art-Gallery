using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repository.Models;

namespace Repository.Interfaces
{
    public interface IArtworkInterface
    {
      
        Task<int> UploadArtwork(t_Artwork art);
        Task<IEnumerable<vm_Artwork>> GetAllArtworks();

        Task<IEnumerable<dynamic>> GetCategories();

        Task<t_Artwork> GetById(int id);

        Task<IEnumerable<t_Artwork>> GetApprovedArtworks();
        Task<IEnumerable<t_Artwork>> GetArtworksByArtist(int artistId);
        Task<int> DeleteArtwork(int artworkId);
        Task<int> UpdateArtwork(t_Artwork art);
    }
}