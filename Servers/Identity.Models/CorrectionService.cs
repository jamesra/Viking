namespace Viking.Identity.Models
{
    /// <summary>
    /// A section-correction container (GrpcSectionCorrectionService). A context's corrections live in one place, so a
    /// context belongs to at most one correction service: the management site refuses an assignment that reaches a
    /// context another correction service already reaches, and where group or unit changes make two reach it later,
    /// the service with the lowest id keeps it. The container signs in as the shared "correction" OAuth client; Viking
    /// clients receive <see cref="ContextService.Endpoint"/> as the CorrectionServiceUrl of each of its contexts.
    /// </summary>
    public class CorrectionService : ContextService
    {
    }
}
