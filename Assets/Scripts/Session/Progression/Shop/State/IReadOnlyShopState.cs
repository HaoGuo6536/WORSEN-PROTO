// ============================================================================
// IReadOnlyShopState.cs
// ============================================================================
// PURPOSE:
//   Exposes reservation identity without sharing inventory mutation methods.
//   The parent Progression owns the mutable subtree; other readers use frozen views.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Session · Progression.Shop read-only state view.
// KEY RESPONSIBILITIES:
//   - Describe whether a slot replacement has been reserved.
// DEPENDENCIES:
//   - None; primitive values only.
// USAGE NOTES:
//   Declared beside ShopBehaviorState as required by architecture §2c and §3.
// ============================================================================
namespace Worsen.Session.Progression.Shop
{
    public interface IReadOnlyShopState { string PendingOfferId { get; } }
}
