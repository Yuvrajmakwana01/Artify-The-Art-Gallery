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

        // Task<IEnumerable<vm_Artwork>> GetAllArtworks(int artistId);

        Task<IEnumerable<vm_Artwork>> GetAllArtworks();



        Task<IEnumerable<dynamic>> GetCategories();

        // Task<IEnumerable<t_Artwork>> GetApprovedArtworks();

        // Task<IEnumerable<t_Artwork>> GetArtworksByArtist(int artistId);

        // Task<int> UpdateApprovalStatus(int artworkId, string status, string adminNote);

        // Task<int> DeleteArtwork(int artworkId);
    }
}