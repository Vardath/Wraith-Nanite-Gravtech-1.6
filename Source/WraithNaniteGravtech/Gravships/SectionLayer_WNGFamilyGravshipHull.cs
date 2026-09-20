using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Family-aware Odyssey gravship hull renderer.
    ///
    /// Straight hull cells remain owned by each family's linked hull graphic.  For angled/corner
    /// joins this layer now reuses the *same family hull material* instead of tinting Odyssey's
    /// vanilla GravshipHull corner artwork.  That keeps Wraith living chitin, Asuran nanite
    /// composite and Goa'uld Ha'tak armor visually continuous through 45-degree transitions.
    ///
    /// Substructure corner fill likewise uses the exact family substructure texture rather than
    /// Odyssey's generic corner texture.  This deliberately makes art identity follow the actual
    /// hull/substructure Defs, so future art upgrades do not require another rendering rewrite.
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

        private static readonly Dictionary<string, string> SubstructureTexturePaths =
            new Dictionary<string, string>
            {
                { "WNG_OrganicGravshipHull", "Terrain/Wraith/WNG_WraithGravshipSubstructure" },
                { "WNG_PrecursorGravshipHull", "Terrain/Precursor/WNG_AsuranGravshipSubstructure" },
                { "WNG_GoauldGravshipHull", "Terrain/Goauld/WNG_GoauldGravshipSubstructure" }
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
        private const float FullCornerBandWidth = 0.58f;
        private const float PartialCornerBandWidth = 0.44f;

        private static readonly float CornerAltitude =
            AltitudeLayer.BuildingOnTop.AltitudeFor();

        private static readonly float SubstructureAltitude =
            AltitudeLayer.TerrainEdges.AltitudeFor();

        private static List<ThingDef> familyHullDefs;
        private static readonly Dictionary<string, Material> FamilySubstructureMaterials =
            new Dictionary<string, Material>();

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

                    Material hullMaterial = FamilyHullMaterial(hullDef);
                    if (hullMaterial != null)
                    {
                        AddAngledBand(
                            hullMaterial,
                            drawCell,
                            corner,
                            IsPartial(corner) ? PartialCornerBandWidth : FullCornerBandWidth,
                            CornerAltitude,
                            colour,
                            gravshipMasked,
                            indoorMasked);
                    }

                    bool substructureSouth =
                        terrain.FoundationAt(cell + IntVec3.South)?.IsSubstructure ?? false;

                    AddSubstructureCorner(
                        hullDef,
                        corner,
                        cell,
                        substructureSouth,
                        gravshipMasked,
                        indoorMasked);

                    // One empty corner cell can belong to only one coherent same-family topology.
                    break;
                }
            }

            FinalizeMesh(MeshParts.All);
        }

        private static void EnsureResolved()
        {
            if (familyHullDefs != null)
                return;

            familyHullDefs = FamilyHullDefNames
                .Select(DefDatabase<ThingDef>.GetNamedSilentFail)
                .Where(def => def != null)
                .Distinct()
                .ToList();
        }

        private static bool IsPartial(CornerType corner)
        {
            return corner == CornerType.DiagonalNW ||
                   corner == CornerType.DiagonalNE ||
                   corner == CornerType.DiagonalSW ||
                   corner == CornerType.DiagonalSE;
        }

        private static Material FamilyHullMaterial(ThingDef hullDef)
        {
            Graphic graphic = hullDef?.graphicData?.Graphic;
            if (graphic is Graphic_Linked linked)
                graphic = linked.SubGraphic;
            return graphic?.MatSingle;
        }

        private static Material FamilySubstructureMaterial(ThingDef hullDef)
        {
            if (hullDef == null ||
                !SubstructureTexturePaths.TryGetValue(hullDef.defName, out string path))
                return null;

            if (!FamilySubstructureMaterials.TryGetValue(path, out Material material))
            {
                material = MaterialPool.MatFrom(path, ShaderDatabase.Transparent);
                FamilySubstructureMaterials[path] = material;
            }
            return material;
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

        private void AddAngledBand(
            Material material,
            IntVec3 drawCell,
            CornerType corner,
            float width,
            float altitude,
            Color colour,
            bool gravshipMasked,
            bool indoorMasked)
        {
            AddAngledBand(GetSubMesh(material), drawCell, corner, width, altitude, colour);

            Texture2D texture = material.mainTexture as Texture2D;
            if (texture == null)
                return;

            if (gravshipMasked)
            {
                Material mask = MaterialPool.MatFrom(
                    texture,
                    ShaderDatabase.GravshipMaskMasked,
                    material.color);
                AddAngledBand(GetSubMesh(mask), drawCell, corner, width, altitude, colour);
            }

            if (indoorMasked)
            {
                Material mask = MaterialPool.MatFrom(
                    texture,
                    ShaderDatabase.IndoorMaskMasked,
                    material.color);
                AddAngledBand(GetSubMesh(mask), drawCell, corner, width, altitude, colour);
            }
        }

        private static void AddAngledBand(
            LayerSubMesh mesh,
            IntVec3 drawCell,
            CornerType corner,
            float width,
            float altitude,
            Color colour)
        {
            Vector2 p1;
            Vector2 p2;

            switch (corner)
            {
                case CornerType.CornerNW:
                case CornerType.DiagonalNW:
                    p1 = new Vector2(0f, 1f);
                    p2 = new Vector2(1f, 2f);
                    break;
                case CornerType.CornerNE:
                case CornerType.DiagonalNE:
                    p1 = new Vector2(1f, 2f);
                    p2 = new Vector2(2f, 1f);
                    break;
                case CornerType.CornerSE:
                case CornerType.DiagonalSE:
                    p1 = new Vector2(2f, 1f);
                    p2 = new Vector2(1f, 0f);
                    break;
                case CornerType.CornerSW:
                case CornerType.DiagonalSW:
                    p1 = new Vector2(1f, 0f);
                    p2 = new Vector2(0f, 1f);
                    break;
                default:
                    return;
            }

            Vector2 direction = (p2 - p1).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x) * (width * 0.5f);

            Vector2 a = p1 + normal;
            Vector2 b = p2 + normal;
            Vector2 c = p2 - normal;
            Vector2 d = p1 - normal;

            int first = mesh.verts.Count;
            AddBandVertex(mesh, drawCell, a, altitude, new Vector2(0f, 0f), colour);
            AddBandVertex(mesh, drawCell, b, altitude, new Vector2(1f, 0f), colour);
            AddBandVertex(mesh, drawCell, c, altitude, new Vector2(1f, 1f), colour);
            AddBandVertex(mesh, drawCell, d, altitude, new Vector2(0f, 1f), colour);

            mesh.tris.Add(first);
            mesh.tris.Add(first + 1);
            mesh.tris.Add(first + 2);
            mesh.tris.Add(first);
            mesh.tris.Add(first + 2);
            mesh.tris.Add(first + 3);
        }

        private static void AddBandVertex(
            LayerSubMesh mesh,
            IntVec3 drawCell,
            Vector2 local,
            float altitude,
            Vector2 uv,
            Color colour)
        {
            mesh.verts.Add(
                new Vector3(
                    drawCell.x + local.x,
                    altitude,
                    drawCell.z + local.y));
            mesh.uvs.Add(uv);
            mesh.colors.Add(colour);
        }

        private void AddSubstructureCorner(
            ThingDef hullDef,
            CornerType corner,
            IntVec3 cell,
            bool substructureSouth,
            bool gravshipMasked,
            bool indoorMasked)
        {
            Material material = FamilySubstructureMaterial(hullDef);
            if (material == null)
                return;

            if (corner == CornerType.CornerNW ||
                corner == CornerType.DiagonalNW)
            {
                AddSubstructureQuad(
                    material,
                    cell,
                    gravshipMasked,
                    indoorMasked);

                if (!substructureSouth)
                {
                    AddSubstructureQuad(
                        material,
                        cell + IntVec3.South,
                        gravshipMasked,
                        indoorMasked);
                }
            }

            if (corner == CornerType.CornerNE ||
                corner == CornerType.DiagonalNE)
            {
                AddSubstructureQuad(
                    material,
                    cell,
                    gravshipMasked,
                    indoorMasked);

                if (!substructureSouth)
                {
                    AddSubstructureQuad(
                        material,
                        cell + IntVec3.South,
                        gravshipMasked,
                        indoorMasked);
                }
            }
        }

        private void AddSubstructureQuad(
            Material material,
            IntVec3 cell,
            bool gravshipMasked,
            bool indoorMasked)
        {
            AddQuad(GetSubMesh(material), cell.ToVector3(), 1f, SubstructureAltitude, Color.white);

            Texture2D texture = material.mainTexture as Texture2D;
            if (texture == null)
                return;

            if (gravshipMasked)
            {
                Material mask = MaterialPool.MatFrom(
                    texture,
                    ShaderDatabase.GravshipMaskMasked,
                    material.color);
                AddQuad(GetSubMesh(mask), cell.ToVector3(), 1f, SubstructureAltitude, Color.white);
            }

            if (indoorMasked)
            {
                Material mask = MaterialPool.MatFrom(
                    texture,
                    ShaderDatabase.IndoorMaskMasked,
                    material.color);
                AddQuad(GetSubMesh(mask), cell.ToVector3(), 1f, SubstructureAltitude, Color.white);
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

            mesh.verts.Add(new Vector3(origin.x, altitude, origin.z));
            mesh.verts.Add(new Vector3(origin.x, altitude, origin.z + scale));
            mesh.verts.Add(new Vector3(origin.x + scale, altitude, origin.z + scale));
            mesh.verts.Add(new Vector3(origin.x + scale, altitude, origin.z));

            mesh.uvs.Add(new Vector2(0f, 0f));
            mesh.uvs.Add(new Vector2(0f, 1f));
            mesh.uvs.Add(new Vector2(1f, 1f));
            mesh.uvs.Add(new Vector2(1f, 0f));

            mesh.colors.Add(colour);
            mesh.colors.Add(colour);
            mesh.colors.Add(colour);
            mesh.colors.Add(colour);

            mesh.tris.Add(first);
            mesh.tris.Add(first + 1);
            mesh.tris.Add(first + 2);
            mesh.tris.Add(first);
            mesh.tris.Add(first + 2);
            mesh.tris.Add(first + 3);
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
    }
}
