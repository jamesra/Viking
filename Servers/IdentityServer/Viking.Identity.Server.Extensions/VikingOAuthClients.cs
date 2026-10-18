namespace Viking.Identity.Server
{
    /// <summary>OAuth client ids issued by Identity Server.</summary>
    public static class VikingOAuthClients
    {
        /// <summary>Confidential backend for sbfsem-tools.com. With the SAM2 gallery clients, one of the only clients allowed to mint desktop launch codes from a bearer token.</summary>
        public const string SbfsemTools = "sbfsem-tools";

        /// <summary>SAM2 annotation gallery for TEM (tem.gallery.codepharm.net). May mint desktop launch codes.</summary>
        public const string GalleryTem = "sam2-gallery-tem";

        /// <summary>SAM2 annotation gallery for SEM (sem.gallery.codepharm.net). May mint desktop launch codes.</summary>
        public const string GallerySem = "sam2-gallery-sem";
    }
}
