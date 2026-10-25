using System.Linq;
using UnityEngine;

namespace FinishTheGame
{
    /// <summary>The studio's rigged characters (Mixamo's skeleton) put in the
    /// world: the model under a parent, as tall as asked, its Animator on the
    /// node above the hips (where the clips' paths begin), its look a copy of
    /// a material with the model's own painted texture.</summary>
    public static class Rigs
    {
        public static Animator Dress(GameObject rig, Transform parent, float height, RuntimeAnimatorController controller, Material look, float sink = 0f)
        {
            var model = Object.Instantiate(rig, parent);
            model.name = "Look";
            var bounds = new Bounds(model.transform.position, Vector3.zero);
            foreach (var r in model.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            model.transform.localScale = Vector3.one * (height / Mathf.Max(0.1f, bounds.size.y));
            model.transform.localPosition = Vector3.down * sink;
            var hips = model.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.EndsWith("Hips"));
            var root = hips != null && hips.parent != null ? hips.parent.gameObject : model;
            var animator = root.GetComponent<Animator>();
            if (animator == null) animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            if (look != null)
                foreach (var r in model.GetComponentsInChildren<Renderer>())
                {
                    var made = new Material(look);
                    var painted = r.sharedMaterial != null ? r.sharedMaterial.mainTexture : null;
                    if (painted != null) made.mainTexture = painted;
                    r.sharedMaterial = made;
                }
            return animator;
        }
    }
}
