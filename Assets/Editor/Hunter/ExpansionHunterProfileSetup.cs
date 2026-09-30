// ============================================================================
// ExpansionHunterProfileSetup.cs
// ============================================================================
// PURPOSE:
//   Produces the complete playable roster before scene serialization or player builds.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Hunter.
// KEY RESPONSIBILITIES:
//   - Compose existing idempotent generators at stable Resources paths.
//   - Resolve all ten persisted profiles without runtime asset creation or legacy selection.
// DEPENDENCIES:
//   - Hunter profile setup tools, UnityEditor and Domain Hunter data.
// USAGE NOTES:
//   Explicit idle-editor command, never a build/import callback. Existing legacy assets
//   are borrowed only as placeholders and remain available to compatibility scenes.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEditor;
using Worsen.Domain.Hunter;
namespace Worsen.Editor.Hunter
{
    public static class ExpansionHunterProfileSetup
    {
        public static IReadOnlyList<string> Names { get; } = Array.AsReadOnly(new[]
            { "Echo", "Weaver", "Ticking", "Ram", "Skip", "Mimic", "Blinder", "Herald", "Mannequin", "Stare" });
        [MenuItem("Worsen/Hunter/Build Complete Expansion Roster")]
        public static void Build() => BuildProfiles();
        public static HunterProfile[] BuildProfiles()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Expansion roster setup requires idle Edit Mode.");
            var placeholder = AssetDatabase.LoadAssetAtPath<HunterProfile>(EchoProfileSetup.PlaceholderProfilePath);
            if (placeholder == null || placeholder.Prefab == null) HorrorHunterSetup.BuildProfiles();
            EchoProfileSetup.Build(); WeaverProfileSetup.Build(); TickingProfileSetup.Build();
            RosterBProfileSetup.BuildRam(); RosterBProfileSetup.BuildSkip(); RosterBProfileSetup.BuildMimic();
            BlinderHeraldProfileSetup.BuildBlinder(); BlinderHeraldProfileSetup.BuildHerald();
            ObservedHunterProfileSetup.BuildMannequin(); ObservedHunterProfileSetup.BuildStare();
            return LoadProfiles();
        }
        public static HunterProfile[] LoadProfiles()
        {
            var result = new HunterProfile[Names.Count];
            for (int i = 0; i < Names.Count; i++)
            {
                string name = Names[i];
                string path = RosterBProfileSetup.Root + name + "/" + name + "Profile.asset";
                result[i] = AssetDatabase.LoadAssetAtPath<HunterProfile>(path);
                if (result[i] == null || result[i].ArchetypeKey != name.ToLowerInvariant() || result[i].Prefab == null || result[i].ArchetypeRules == null)
                    throw new InvalidOperationException("Missing or invalid persisted expansion profile: " + path + ". Run Build Complete Expansion Roster before building.");
            }
            return result;
        }
    }
}
