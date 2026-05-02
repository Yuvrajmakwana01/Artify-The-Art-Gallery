namespace Repository.Models
{
    /// <summary>
    /// Represents a single approved payout record visible to the artist.
    /// </summary>
    public class t_ArtistPayoutHistory
    {
        public int    Id            { get; set; }
        public string RequestMonth  { get; set; } = string.Empty;
        public decimal GrossAmount  { get; set; }
        public decimal Commission   { get; set; }
        public decimal NetAmount    { get; set; }
        public string Status        { get; set; } = string.Empty;
        public DateTime? ProcessedDate { get; set; }
    }
}
