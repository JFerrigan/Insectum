using System;
using UnityEngine;

namespace Insectum.Game
{
    public enum TerrainMaterial
    {
        Sand,
        SoftDirt,
        Mud,
        PackedDirt,
        Clay,
        AlienOrganic,
        SoftStone,
        HardStone,
        Metal,
        Reinforced,
        Sandstone,
        // The map's side walls: nothing can dig or blast it.
        Boundary
    }

    [Serializable]
    public struct TerrainMaterialTuning
    {
        [Min(0)] public int excavationEnergy;
        [Min(0)] public int blastResistance;
        public Color32 tint;
    }

    public sealed class TerrainMaterials : ScriptableObject
    {
        [SerializeField] private TerrainMaterialTuning sand;
        [SerializeField] private TerrainMaterialTuning softDirt;
        [SerializeField] private TerrainMaterialTuning mud;
        [SerializeField] private TerrainMaterialTuning packedDirt;
        [SerializeField] private TerrainMaterialTuning clay;
        [SerializeField] private TerrainMaterialTuning alienOrganic;
        [SerializeField] private TerrainMaterialTuning softStone;
        [SerializeField] private TerrainMaterialTuning hardStone;
        [SerializeField] private TerrainMaterialTuning metal;
        [SerializeField] private TerrainMaterialTuning reinforced;
        [SerializeField] private TerrainMaterialTuning sandstone;
        [SerializeField] private TerrainMaterialTuning boundary;

        public TerrainMaterialTuning Get(TerrainMaterial material) => material switch
        {
            TerrainMaterial.Sand => sand,
            TerrainMaterial.SoftDirt => softDirt,
            TerrainMaterial.Mud => mud,
            TerrainMaterial.PackedDirt => packedDirt,
            TerrainMaterial.Clay => clay,
            TerrainMaterial.AlienOrganic => alienOrganic,
            TerrainMaterial.SoftStone => softStone,
            TerrainMaterial.HardStone => hardStone,
            TerrainMaterial.Metal => metal,
            TerrainMaterial.Reinforced => reinforced,
            TerrainMaterial.Sandstone => sandstone,
            TerrainMaterial.Boundary => boundary,
            _ => throw new ArgumentOutOfRangeException(nameof(material))
        };

        public static TerrainMaterials Load() => Resources.Load<TerrainMaterials>("TerrainMaterials")
            ?? throw new InvalidOperationException("The TerrainMaterials asset is missing.");
    }
}
