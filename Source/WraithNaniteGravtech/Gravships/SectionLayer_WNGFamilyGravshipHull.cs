using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Odyssey's native SectionLayer_GravshipHull only recognises ThingDefOf.GravshipHull.
    /// WNG family hulls are real airtight gravship hull walls with separate Defs, so this layer
    /// reproduces the current 1.6 angled-corner topology for those exact family Defs without
    /// patching or replacing the native layer.
    ///
    /// Dedicated WNG angled-corner art is intentionally not fabricated here. Until the later
    /// presentation pass supplies family corner masks, the current Odyssey angled hull masks are
    /// tinted by the exact neighbouring WNG hull DrawColor. Straight WNG hull surfaces remain owned
    /// by each family's existing Graphic_Linked atlas.
    /// </summary>
    public sealed class SectionLayer_WNGFamilyGravshipHull : SectionLayer
    {
        private enum CornerType
        {
            None,
            CornerNW,
            CornerNE,
            CornerSW,
            CornerSE,
            DiagonalNW,
            DiagonalNE,
            DiagonalSW,
            DiagonalSE
        }

        private static readonly string[] FamilyHullDefNames =
        {
            "WNG_OrganicGravshipHull",
            "WNG_PrecursorGravshipHull",
            "WNG_GoauldGravshipHull"
        };

        private static readonly Vector2[] QuadUvs =
        {
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 0f)
        };

        private static readonly IntVec3[] NeighbourOffsets =
        {
            IntVec3.North,
            IntVec3.East,
            IntVec3.South,
            IntVec3.West,
            IntVec3.North + IntVec3.West,
            IntVec3.North + IntVec3.East,
            IntVec3.South + IntVec3.East,
            IntVec3.South + IntVec3.West
        };

        private static readonly int[][] ColourProbePairs =
        {
            new[] { 0, 2 },
            new[] { 1, 3 },
            new[] { 4, 6 },
            new[] { 5, 7 }
        };

        private const float CornerScale = 2f;

        private static readonly float CornerAltitude =
            AltitudeLayer.BuildingOnTop.AltitudeFor();

        private static readonly float SubstructureAltitude =
            AltitudeLayer.TerrainEdges.AltitudeFor();

        private static List<ThingDef> familyHullDefs;

        private static CachedMaterial cornerNW;
        private static CachedMaterial cornerNE;
        private static CachedMaterial cornerSW;
        private static CachedMaterial cornerSE;
        private static CachedMaterial diagonalNW;
        private static CachedMaterial diagonalNE;
        private static CachedMaterial diagonalSW;
        private static CachedMaterial diagonalSE;
        private static CachedMaterial substructureWest;
        private static CachedMaterial substructureEast;
        private static CachedMaterial substructureTipWest;
        private static CachedMaterial substructureTipEast;

        public override bool Visible => ModsConfig.OdysseyActive;

        public SectionLayer_WNGFamilyGravshipHull(Section section)
            : base(section)
        {
            relevantChangeTypes =
                (ulong)MapMeshFlagDefOf.Buildings |
                (ulong)MapMeshFlagDefOf.Terrain |
                (ulong)MapMeshFlagDefOf.Things |
                (ulong)MapMeshFlagDefOf.Roofs;
        }

        public override void Regenerate()
        {
            if (!ModsConfig.OdysseyActive)
                return;

            EnsureResolved();
            ClearSubMeshes(MeshParts.All);

            if (familyHullDefs.Count == 0)
            {
                FinalizeMesh(MeshParts.All);
                return;
            }

            Map map = Map;
            TerrainGrid terrain = map.terrainGrid;

            foreach (IntVec3 cell in section.CellRect)
            {
                for (int i = 0; i < familyHullDefs.Count; i++)
                {
                    ThingDef hullDef = familyHullDefs[i];
                    if (!TryResolveCorner(
                            cell,
                            map,
                            terrain,
                            hullDef,
                            out CornerType corner,
                            out Color colour))
                    {
                        continue;
                    }

                    IntVec3 drawCell = cell + CornerOffset(corner);
                    bool gravshipMasked = CornerTouchesSubstructure(cell, corner);
                    bool indoorMasked = CornerTouchesRoof(cell, corner, map);

                    AddQuad(
                        CornerMaterial(corner).Material,
                        drawCell,
                        CornerScale,
                        CornerAltitude,
                        colour,
                        gravshipMasked,
                        indoorMasked);

                    bool substructureSouth =
                        terrain.FoundationAt(cell + IntVec3.South)?.IsSubstructure ?? false;

                    AddSubstructureCorner(
                        corner,
                        cell,
                        substructureSouth,
                        gravshipMasked,
                        indoorMasked);

                    // One empty corner cell can belong to only one coherent same-family topology.
                    // Stop after the first exact family match so different hull families never blend.
                    break;
                }
            }

            FinalizeMesh(MeshParts.All);
        }

        private static void EnsureResolved()
        {
            if (familyHullDefs == null)
            {
                familyHullDefs = FamilyHullDefNames
                    .Select(DefDatabase<ThingDef>.GetNamedSilentFail)
                    .Where(def => def != null)
                    .Distinct()
                    .ToList();
            }

            if (cornerNW != null)
                return;

            Shader wallShader = ShaderDatabase.CutoutOverlay;
            Shader substructureShader = ShaderDatabase.Transparent;

            cornerNW = new CachedMaterial(
                "Things/Building/Linked/GravshipHull/AngledGravshipHull_northwest",
                wallShader);
            cornerNE = new CachedMaterial(
                "Things/Building/Linked/GravshipHull/AngledGravshipHull_northeast",
                wallShader);
            cornerSW = new CachedMaterial(
                "Things/Building/Linked/GravshipHull/AngledGravshipHull_southwest",
                wallShader);
            cornerSE = new CachedMaterial(
                "Things/Building/Linked/GravshipHull/AngledGravshipHull_southeast",
                wallShader);
            diagonalNW = new CachedMaterial(
                "Things/Building/Linked/GravshipHull/AngledGravshipHull_Partial_northwest",
                wallShader);
            diagonalNE = new CachedMaterial(
                "Things/Building/Linked/GravshipHull/AngledGravshipHull_Partial_northeast",
                wallShader);
            diagonalSW = new CachedMaterial(
                "Things/Building/Linked/GravshipHull/AngledGravshipHull_Partial_southwest",
                wallShader);
            diagonalSE = new CachedMaterial(
                "Things/Building/Linked/GravshipHull/AngledGravshipHull_Partial_southeast",
                wallShader);

            substructureWest = new CachedMaterial(
                "Things/Building/Linked/GravshipHull/SubstructureCorner_Full_west",
                substructureShader);
            substructureEast = new CachedMaterial(
                "Things/Building/Linked/GravshipHull/SubstructureCorner_Full_east",
                substructureShader);
            substructureTipWest = new CachedMaterial(
                "Things/Building/Linked/GravshipHull/SubstructureCorner_Tip_west",
                substructureShader);
            substructureTipEast = new CachedMaterial(
                "Things/Building/Linked/GravshipHull/SubstructureCorner_Tip_east",
                substructureShader);
        }

        private static bool TryResolveCorner(
            IntVec3 cell,
            Map map,
            TerrainGrid terrain,
            ThingDef hullDef,
            out CornerType corner,
            out Color colour)
        {
            corner = CornerType.None;
            colour = Color.white;

            if (cell.GetEdifice(map) != null)
                return false;

            TerrainDef foundation = terrain.FoundationAt(cell);
            if (foundation != null && foundation.IsSubstructure)
                return false;

            bool[] linked = new bool[NeighbourOffsets.Length];
            for (int i = 0; i < NeighbourOffsets.Length; i++)
            {
                IntVec3 neighbour = cell + NeighbourOffsets[i];
                linked[i] =
                    neighbour.InBounds(map) &&
                    neighbour.GetEdificeSafe(map)?.def == hullDef;
            }

            if (linked[0] && linked[3] && !linked[2] && !linked[1])
                corner = linked[4] ? CornerType.CornerNW : CornerType.DiagonalNW;
            else if (linked[0] && linked[1] && !linked[2] && !linked[3])
                corner = linked[5] ? CornerType.CornerNE : CornerType.DiagonalNE;
            else if (linked[2] && linked[1] && !linked[0] && !linked[3])
                corner = linked[6] ? CornerType.CornerSE : CornerType.DiagonalSE;
            else if (linked[2] && linked[3] && !linked[0] && !linked[1])
                corner = linked[7] ? CornerType.CornerSW : CornerType.DiagonalSW;

            if (corner == CornerType.None)
                return false;

            for (int i = 0; i < ColourProbePairs.Length; i++)
            {
                int[] pair = ColourProbePairs[i];
                for (int j = 0; j < pair.Length; j++)
                {
                    int neighbourIndex = pair[j];
                    if (!linked[neighbourIndex])
                        continue;

                    Thing neighbour =
                        (cell + NeighbourOffsets[neighbourIndex]).GetEdificeSafe(map);
                    if (neighbour?.def == hullDef)
                    {
                        colour = neighbour.DrawColor;
                        return true;
                    }
                }
            }

            return true;
        }

        private static CachedMaterial CornerMaterial(CornerType corner)
        {
            EnsureResolved();
            switch (corner)
            {
                case CornerType.CornerNW: return cornerNW;
                case CornerType.CornerNE: return cornerNE;
                case CornerType.CornerSW: return cornerSW;
                case CornerType.CornerSE: return cornerSE;
                case CornerType.DiagonalNW: return diagonalNW;
                case CornerType.DiagonalNE: return diagonalNE;
                case CornerType.DiagonalSW: return diagonalSW;
                case CornerType.DiagonalSE: return diagonalSE;
                default:
                    throw new ArgumentOutOfRangeException(nameof(corner), corner, null);
            }
        }

        private static IntVec3 CornerOffset(CornerType corner)
        {
            switch (corner)
            {
                case CornerType.CornerNW:
                case CornerType.DiagonalNW:
                    return new IntVec3(-1, 0, 0);
                case CornerType.CornerSE:
                case CornerType.DiagonalSE:
                    return new IntVec3(0, 0, -1);
                case CornerType.CornerSW:
                case CornerType.DiagonalSW:
                    return new IntVec3(-1, 0, -1);
                default:
                    return IntVec3.Zero;
            }
        }

        private static bool CornerTouchesSubstructure(
            IntVec3 cell,
            CornerType corner)
        {
            switch (corner)
            {
                case CornerType.CornerNE:
                case CornerType.DiagonalNE:
                    return SectionLayer_GravshipMask.IsValidSubstructure(cell + IntVec3.North) ||
                           SectionLayer_GravshipMask.IsValidSubstructure(cell + IntVec3.East);
                case CornerType.CornerNW:
                case CornerType.DiagonalNW:
                    return SectionLayer_GravshipMask.IsValidSubstructure(cell + IntVec3.North) ||
                           SectionLayer_GravshipMask.IsValidSubstructure(cell + IntVec3.West);
                case CornerType.CornerSE:
                case CornerType.DiagonalSE:
                    return SectionLayer_GravshipMask.IsValidSubstructure(cell + IntVec3.South) ||
                           SectionLayer_GravshipMask.IsValidSubstructure(cell + IntVec3.East);
                case CornerType.CornerSW:
                case CornerType.DiagonalSW:
                    return SectionLayer_GravshipMask.IsValidSubstructure(cell + IntVec3.South) ||
                           SectionLayer_GravshipMask.IsValidSubstructure(cell + IntVec3.West);
                default:
                    return false;
            }
        }

        private static bool CornerTouchesRoof(
            IntVec3 cell,
            CornerType corner,
            Map map)
        {
            switch (corner)
            {
                case CornerType.CornerNE:
                case CornerType.DiagonalNE:
                    return (cell + IntVec3.North).Roofed(map) ||
                           (cell + IntVec3.East).Roofed(map);
                case CornerType.CornerNW:
                case CornerType.DiagonalNW:
                    return (cell + IntVec3.North).Roofed(map) ||
                           (cell + IntVec3.West).Roofed(map);
                case CornerType.CornerSE:
                case CornerType.DiagonalSE:
                    return (cell + IntVec3.South).Roofed(map) ||
                           (cell + IntVec3.East).Roofed(map);
                case CornerType.CornerSW:
                case CornerType.DiagonalSW:
                    return (cell + IntVec3.South).Roofed(map) ||
                           (cell + IntVec3.West).Roofed(map);
                default:
                    return false;
            }
        }

        private static void AddQuad(
            LayerSubMesh mesh,
            Vector3 origin,
            float scale,
            float altitude,
            Color colour)
        {
            int first = mesh.verts.Count;
            for (int i = 0; i < QuadUvs.Length; i++)
            {
                Vector2 uv = QuadUvs[i];
                mesh.verts.Add(
                    new Vector3(
                        origin.x + uv.x * scale,
                        altitude,
                        origin.z + uv.y * scale));
                mesh.uvs.Add(uv);
                mesh.colors.Add(colour);
            }

            mesh.tris.Add(first);
            mesh.tris.Add(first + 1);
            mesh.tris.Add(first + 2);
            mesh.tris.Add(first);
            mesh.tris.Add(first + 2);
            mesh.tris.Add(first + 3);
        }

        private void AddQuad(
            Material material,
            IntVec3 cell,
            float scale,
            float altitude,
            Color colour,
            bool gravshipMasked,
            bool indoorMasked)
        {
            LayerSubMesh baseMesh = GetSubMesh(material);
            AddQuad(baseMesh, cell.ToVector3(), scale, altitude, colour);

            Texture2D texture = baseMesh.material.mainTexture as Texture2D;
            if (texture == null)
                return;

            if (gravshipMasked)
            {
                Material mask = MaterialPool.MatFrom(
                    texture,
                    ShaderDatabase.GravshipMaskMasked,
                    baseMesh.material.color);
                AddQuad(GetSubMesh(mask), cell.ToVector3(), scale, altitude, colour);
            }

            if (indoorMasked)
            {
                Material mask = MaterialPool.MatFrom(
                    texture,
                    ShaderDatabase.IndoorMaskMasked,
                    baseMesh.material.color);
                AddQuad(GetSubMesh(mask), cell.ToVector3(), scale, altitude, colour);
            }
        }

        private void AddSubstructureCorner(
            CornerType corner,
            IntVec3 cell,
            bool substructureSouth,
            bool gravshipMasked,
            bool indoorMasked)
        {
            if (corner == CornerType.CornerNW ||
                corner == CornerType.DiagonalNW)
            {
                AddQuad(
                    substructureWest.Material,
                    cell,
                    1f,
                    SubstructureAltitude,
                    Color.white,
                    gravshipMasked,
                    indoorMasked);

                if (!substructureSouth)
                {
                    AddQuad(
                        substructureTipWest.Material,
                        cell + IntVec3.South,
                        1f,
                        SubstructureAltitude,
                        Color.white,
                        gravshipMasked,
                        indoorMasked);
                }
            }

            if (corner == CornerType.CornerNE ||
                corner == CornerType.DiagonalNE)
            {
                AddQuad(
                    substructureEast.Material,
                    cell,
                    1f,
                    SubstructureAltitude,
                    Color.white,
                    gravshipMasked,
                    indoorMasked);

                if (!substructureSouth)
                {
                    AddQuad(
                        substructureTipEast.Material,
                        cell + IntVec3.South,
                        1f,
                        SubstructureAltitude,
                        Color.white,
                        gravshipMasked,
                        indoorMasked);
                }
            }
        }
    }
}
