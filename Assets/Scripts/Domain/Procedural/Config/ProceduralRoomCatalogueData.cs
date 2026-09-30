// ============================================================================
// ProceduralRoomCatalogueData.cs
// ============================================================================
// PURPOSE:
//   Holds each theme's imported room and kit definitions as durable designer content.
//   The deterministic editor setup replaces these snapshots from manifests; runtime
//   generation never reads JSON or searches the filesystem.
// ARCHITECTURAL ROLE:
//   Content SO (§4b) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Expose imported catalogues and optional kit prefabs without runtime setters.
// DEPENDENCIES:
//   - Own definitions and Unity serialization only.
// USAGE NOTES:
//   Created under Resources/ScriptableObjects/Domain/Procedural by the setup tool.
//   Missing or invalid theme content is an explicit organic fallback, not success.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Worsen.Domain.Procedural
{
    [CreateAssetMenu(menuName = "Worsen/Procedural/Room Catalogue")]
    public sealed class ProceduralRoomCatalogueData : ScriptableObject
    {
        [SerializeField] private ProceduralTemplateCatalogue[] _catalogues = Array.Empty<ProceduralTemplateCatalogue>();
        public IReadOnlyList<ProceduralTemplateCatalogue> Catalogues => Array.AsReadOnly(_catalogues);
        [Serializable] public sealed class KitAsset
        {
            public string Theme, Id;
            public GameObject Prefab;
        }
        [SerializeField] private KitAsset[] _pieces = Array.Empty<KitAsset>();
        public GameObject Piece(string theme, string id) => _pieces.FirstOrDefault(p => p.Theme == theme && p.Id == id)?.Prefab;
    }
}
