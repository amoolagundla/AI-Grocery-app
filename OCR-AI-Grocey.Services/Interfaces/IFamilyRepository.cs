using OCR_AI_Grocery.Family.models; 
using OCR_AI_Grocery.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OCR_AI_Grocery.Services.Repositories
{
    public interface IFamilyRepository
    {
        /// <summary>
        /// Gets all families associated with an email address
        /// </summary>
        /// <param name="email">The email address to look up</param>
        /// <returns>List of families associated with the email</returns>
        Task<List<FamilyEntity>> GetFamiliesByEmail(string email);

        /// <summary>
        /// Gets a family by its ID
        /// </summary>
        /// <param name="id">The family ID</param>
        /// <returns>The family entity, or null if not found</returns>
        Task<FamilyEntity> GetFamilyById(string id);

        /// <summary>
        /// Checks if a pending invite exists for the specified email and family
        /// </summary>
        /// <param name="email">The email address</param>
        /// <param name="familyId">The family ID</param>
        /// <returns>True if a pending invite exists, otherwise false</returns>
        Task<bool> CheckForPendingInvite(string email, string familyId);

        /// <summary>
        /// Creates a new family invite
        /// </summary>
        /// <param name="invite">The invite to create</param>
        Task CreateFamilyInvite(FamilyInvite invite);

        /// <summary>
        /// Gets pending invites for a specific invite ID and email
        /// </summary>
        /// <param name="inviteId">The invite ID</param>
        /// <param name="invitedUserEmail">The invited user's email</param>
        /// <returns>List of pending invites</returns>
        Task<List<FamilyInvite>> GetPendingInvites(string inviteId, string invitedUserEmail);

        /// <summary>
        /// Gets all pending invites for an email address
        /// </summary>
        /// <param name="email">The email address to get invites for</param>
        /// <returns>List of pending invites for the email address</returns>
        Task<List<FamilyInvite>> GetPendingInvitesByEmail(string email);

        /// <summary>
        /// Checks if a family junction exists for the specified email and family
        /// </summary>
        /// <param name="email">The email address</param>
        /// <param name="familyId">The family ID</param>
        /// <returns>True if a junction exists, otherwise false</returns>
        Task<bool> CheckFamilyJunctionExists(string email, string familyId);

        /// <summary>
        /// Creates a new family junction
        /// </summary>
        /// <param name="junction">The junction to create</param>
        Task CreateFamilyJunction(FamilyJunction junction);

        /// <summary>
        /// Updates a family invite
        /// </summary>
        /// <param name="invite">The invite to update</param>
        Task UpdateFamilyInvite(FamilyInvite invite);

        /// <summary>
        /// Initializes a family for a user, either by finding an existing one or creating a new one
        /// </summary>
        /// <param name="email">The user's email</param>
        /// <returns>The family ID and whether it's a new family</returns>
        Task<InitializeFamilyResponse> InitializeFamily(string email);

        /// <summary>
        /// Checks if the user has an existing family membership
        /// </summary>
        /// <param name="email">The user's email</param>
        /// <returns>The family ID if found, otherwise null</returns>
        Task<string> CheckExistingFamilyMembership(string email);

        /// <summary>
        /// Checks if the user is the primary contact for any family
        /// </summary>
        /// <param name="email">The user's email</param>
        /// <returns>The family entity if found, otherwise null</returns>
        Task<FamilyEntity> CheckExistingPrimaryFamily(string email);

        /// <summary>
        /// Creates a new family with the user as the primary contact
        /// </summary>
        /// <param name="email">The user's email</param>
        /// <returns>The new family ID</returns>
        Task<string> CreateNewFamily(string email);
    }
}