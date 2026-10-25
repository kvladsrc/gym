using System;
using System.Collections.Generic;
using UnityEngine;

namespace FinishTheGame
{
    /// <summary>The game's models (the asset studio's, art/convert_models.py):
    /// each a mesh standing on its origin and its painted texture, and how it
    /// is turned to face +z. The level's props name them (<see cref="Prop.model"/>);
    /// a prop with no model, or one not here, is a box.</summary>
    [Serializable]
    public class Model
    {
        public string name;
        public Mesh mesh;
        public Texture2D texture;
        /// <summary>Degrees about y that turn the model's front to +z.</summary>
        public float yaw;
        /// <summary>Its texture's average colour: the model seen with little detail (the ribbon).</summary>
        public Color colour = Color.grey;
        /// <summary>Its share of its box's width seen with little detail (a tree's crown is not its box).</summary>
        public float bulk = 1f;
        /// <summary>For the decor (the studio's older models): what it is (tree, dead,
        /// stump, bush, rock, prop, camp, ruin) and its height, m; none for the set's own.</summary>
        public string kind;
        public float height;
        /// <summary>How deep it is set into the ground, a share of its height:
        /// a tree down to where its roots draw in to the trunk (measured).</summary>
        public float sink;
    }

    public static class ModelSet
    {
        public static Dictionary<string, Model> Index(IEnumerable<Model> models)
        {
            var index = new Dictionary<string, Model>();
            if (models == null) return index;
            foreach (var model in models)
                if (model != null && model.mesh != null) index[model.name] = model;
            return index;
        }

        /// <summary>The placing of a model for a prop: turned to the prop's yaw,
        /// scaled evenly to the prop's height (or, <see cref="Prop.byLength"/>,
        /// to its length along x).</summary>
        public static Matrix4x4 Place(Prop prop, Model model)
        {
            var size = model.mesh.bounds.size;
            float scale = prop.byLength ? prop.size.x / Mathf.Max(0.01f, size.x) : prop.size.y / Mathf.Max(0.01f, size.y);
            var at = prop.at + Vector3.down * (model.sink * size.y * scale);
            return Matrix4x4.TRS(at, Quaternion.Euler(0, prop.yaw + model.yaw, 0), Vector3.one * scale);
        }
    }
}
