using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FinishTheGame
{
    /// <summary>The demo's quest, what the ribbon is for: on the road, in
    /// the first wood after the start (<see cref="comesAt"/>),
    /// out of the farm's mark on the ribbon a spirit comes with a flash, a
    /// light hopping down the ribbon from turn to turn, down to the road and
    /// to the hero; there it takes a likeness: the farm woman Hilda, but the
    /// finest of her, a lady in jewels and brocade, glowing; and says what
    /// sounds like nonsense (the wheel that goes nowhere keeps them), asked
    /// anything answers nothing, and goes back up the ribbon into the mark.
    /// Then, a hard cut (the game between left out), at the farm: Hilda
    /// herself, searching the ground for her barn keys. Told what her spirit said, she understands it (the old
    /// cart that has not moved in ten years: she sat on it at noon), and the
    /// keys are found under its wheel.
    ///
    /// For takes: F8 starts it again from the road before the spirit, F9
    /// goes straight to the farm (the spirit met).</summary>
    public class SpiritQuest : MonoBehaviour
    {
        public CrystalWorld world;
        public RibbonBend ribbon;
        public Hero hero;
        public ForwardCamera view;
        public Dialogue dialogue;
        public RibbonSpirit spirit;
        public Villager hilda, ghost;
        /// <summary>The keys' model (none: their glint alone), its height, m; the glint's look.</summary>
        public GameObject keysModel;
        public Texture2D keysTexture;
        public Material keysLook, glintLook;
        public float keysSize = 0.3f;
        /// <summary>Every spoken line, named by its id (art/queue_quest.py).</summary>
        public AudioClip[] lines;
        public AudioClip keysSound, dreamSound;
        /// <summary>The spirit comes where the story is here, s (in the first wood after the start).</summary>
        public float comesAt = 240f;
        /// <summary>The hero talks to Hilda from this near, m.</summary>
        public float reach = 3.2f;

        enum Step { Road, Spirit, Farm, Done }
        Step step;
        bool metSpirit, greeted, toldOfKeys;
        /// <summary>Where the route comes nearest the farm, and Hilda, s.</summary>
        float farmS, hildaS;
        /// <summary>The cart; Hilda's place; the keys, and the way out from the cart's side they lie by.</summary>
        Vector3 cart, hildaHome, keysAt, keysOut;
        /// <summary>The way from the road to the cart: the hero comes to her along it, the farmyard behind her.</summary>
        Vector3 fromRoad;
        Quaternion cartTurn;
        Transform keys, glint;
        Speaker me;
        readonly Dictionary<string, AudioClip> voice = new Dictionary<string, AudioClip>();
        GUIStyle hintStyle;


        void Start()
        {
            foreach (var clip in lines)
                if (clip != null) voice[clip.name] = clip;
            var heroVoice = hero.gameObject.AddComponent<AudioSource>();
            heroVoice.playOnAwake = false;
            me = new Speaker { body = hero.transform, eyes = 1.62f, voice = heroVoice };
            ghost.Haunted();
            // The farm: where the route comes nearest it; the old cart there.
            var path = world.Path;
            float nearest = float.MaxValue;
            for (float s = 0; s <= path.Length; s += 2f)
            {
                var p = path.Position(s);
                float d = Vector2.Distance(new Vector2(p.x, p.z), PlaneLayout.Farm);
                if (d < nearest)
                {
                    nearest = d;
                    farmS = s;
                }
            }
            world.Nearest("old66", PlaneLayout.Farm, out var found);
            cart = world.FromLevel(new Vector3(found.at.x, Land.Height(found.at.x, found.at.z), found.at.z));
            cartTurn = Quaternion.Euler(0, found.yaw, 0);
            // Hilda between the cart and the road; her keys under the cart's wheel.
            var road = world.FromLevel(path.Position(farmS));
            var toRoad = road - cart;
            toRoad.y = 0;
            hildaHome = cart + toRoad.normalized * 3.6f;
            fromRoad = -toRoad.normalized;
            nearest = float.MaxValue;
            for (float s = farmS - 300f; s <= farmS + 300f; s += 1f)
            {
                float d = Vector3.Distance(world.FromLevel(path.Position(s)), hildaHome);
                if (d < nearest)
                {
                    nearest = d;
                    hildaS = s;
                }
            }
            // By the wheel on her side of the cart, a little out from under it.
            var right = cartTurn * Vector3.right;
            float side = Vector3.Dot(hildaHome - cart, right) >= 0 ? 1f : -1f;
            keysOut = right * side;
            keysAt = cart + cartTurn * new Vector3(0.95f * side, 0, 0.4f) + keysOut * 0.3f;
            var level = world.ToLevel(keysAt);
            keysAt = world.FromLevel(new Vector3(level.x, Land.Height(level.x, level.z) + 0.03f, level.z));
            MakeKeys();
            Restore();
        }

        void MakeKeys()
        {
            keys = new GameObject("Keys").transform;
            if (keysModel != null)
            {
                // The studio made them standing: laid down flat in the grass, as long as keysSize.
                var model = Instantiate(keysModel, keys);
                model.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                var renderers = model.GetComponentsInChildren<Renderer>();
                foreach (var r in renderers)
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    if (keysLook == null) continue;
                    var made = new Material(keysLook);
                    if (keysTexture != null) made.mainTexture = keysTexture;
                    r.sharedMaterial = made;
                }
                Bounds Box()
                {
                    var box = renderers[0].bounds;
                    foreach (var r in renderers) box.Encapsulate(r.bounds);
                    return box;
                }
                var size = Box().size;
                model.transform.localScale = Vector3.one * (keysSize / Mathf.Max(0.01f, Mathf.Max(size.x, size.z)));
                var placed = Box();
                model.transform.localPosition = new Vector3(keys.position.x - placed.center.x, keys.position.y - placed.min.y + 0.01f, keys.position.z - placed.center.z);
            }
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(quad.GetComponent<Collider>());
            quad.name = "Glint";
            quad.GetComponent<MeshRenderer>().sharedMaterial = glintLook;
            glint = quad.transform;
            glint.SetParent(keys, false);
        }

        /// <summary>All as before the spirit: Hilda searching at her place, her keys under the cart, no spirit.</summary>
        void Restore()
        {
            StopAllCoroutines();
            dialogue.Release();
            step = Step.Road;
            metSpirit = greeted = toldOfKeys = false;
            hilda.searching = false;
            hilda.Stand(hildaHome, cart);
            hilda.searching = true;
            ghost.SetFade(0f);
            spirit.Hide();
            keys.gameObject.SetActive(true);
            keys.SetPositionAndRotation(keysAt, cartTurn * Quaternion.Euler(0, 35f, 0));
        }

        /// <summary>The hero put on the road at s, facing along it, the ribbon and camera with him.</summary>
        void Put(float s, Vector3? looking = null)
        {
            hero.Place(s);
            world.Restart();
            var way = looking.HasValue ? looking.Value - hero.transform.position : hero.transform.forward;
            view.Restart(Mathf.Atan2(way.x, way.z) * Mathf.Rad2Deg);
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f8Key.wasPressedThisFrame)
            {
                Restore();
                Put(comesAt - 120f);
            }
            if (keyboard != null && keyboard.f9Key.wasPressedThisFrame)
            {
                Restore();
                metSpirit = true;
                ToFarm();
            }
            if (step == Step.Road && hero.S >= comesAt && hero.S < farmS - 50f)
                StartCoroutine(Spirit());
            if (step == Step.Farm && !Dialogue.Cinematic && Near() && keyboard != null && keyboard.eKey.wasPressedThisFrame)
                StartCoroutine(TalkToHilda());
            // The keys' glint: a slow twinkle, turned to the camera.
            var eye = Camera.main;
            if (eye != null && keys.gameObject.activeSelf)
            {
                glint.position = keys.position + Vector3.up * 0.12f;
                glint.rotation = Quaternion.LookRotation(glint.position - eye.transform.position);
                glint.localScale = Vector3.one * (0.35f + 0.25f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(Time.time * 2.3f), 6f));
            }
        }

        bool Near() => Vector3.Distance(hero.transform.position, hilda.transform.position) < reach;

        IEnumerator Say(Speaker who, Speaker to, string id, string text)
        {
            voice.TryGetValue(id, out var clip);
            yield return dialogue.Say(who, to, text, clip);
        }

        Vector3 BeforeHero()
        {
            var front = hero.transform.position + hero.transform.forward * 2.8f;
            front.y = hero.transform.position.y;
            return front;
        }

        /// <summary>The camera behind the hero's shoulder, on what comes (or goes).</summary>
        IEnumerator Watch(System.Func<Vector3> at, System.Func<bool> until, float fieldOfView = 50f)
        {
            while (!until())
            {
                var target = at();
                var eyes = hero.transform.position + Vector3.up * 1.7f;
                var way = target - eyes;
                way.y = 0;
                if (way.sqrMagnitude < 1e-4f) way = hero.transform.forward;
                way.Normalize();
                var from = eyes - way * 3.2f + Vector3.Cross(Vector3.up, way) * 0.9f + Vector3.up * 0.35f;
                dialogue.Shot(from, Vector3.Lerp(target, eyes + way * 10f, 0.15f), fieldOfView);
                yield return null;
            }
        }

        IEnumerator Spirit()
        {
            step = Step.Spirit;
            // Out of the farm's mark on the ribbon: the scene takes the
            // picture, the hero stops and turns to it, the camera behind him on it.
            float mark = ribbon.AlongOf(farmS);
            dialogue.Take();
            bool arrived = false;
            StartCoroutine(Watch(() => spirit.Shown ? spirit.Place : spirit.Edge(mark), () => arrived));
            yield return spirit.Appear(RibbonSpirit.On(mark));
            hero.facing = spirit.Edge(0);
            var way = spirit.Plan(mark);
            yield return spirit.Ride(way, BeforeHero);
            arrived = true;
            // There it takes her likeness, a spirit: she comes into being from
            // the ground up out of its light.
            var at = spirit.Place;
            ghost.Stand(at, hero.transform.position);
            ghost.Play("Idle", 0f);
            StartCoroutine(spirit.Vanish(RibbonSpirit.At(() => at), false));
            yield return ghost.Fade(1f, 1.8f);
            var other = ghost.Speaker;
            dialogue.Begin(other);
            yield return Say(other, me, "spirit_hello", "The wheel that goes nowhere... it keeps them. It keeps them all day long.");
            // Asked anything, it answers nothing.
            int chosen = 0;
            yield return dialogue.Choose(other, new[] { "What?", "Who are you?" }, c => chosen = c);
            if (chosen == 0)
            {
                yield return Say(me, other, "hero_huh", "What?");
                yield return Say(other, me, "spirit_what", "The one that goes nowhere. Under. Under it, where it is warm. It keeps them.");
            }
            else
            {
                yield return Say(me, other, "hero_who", "Who are you?");
                yield return Say(other, me, "spirit_who", "Nine cows... and one bell. Nine cows, one bell. And the wheel keeps them.");
            }
            metSpirit = true;
            // Back the way it came, into the mark.
            StartCoroutine(ghost.Fade(0f, 1.2f));
            yield return new WaitForSeconds(0.5f);
            yield return spirit.Appear(RibbonSpirit.At(() => at), false);
            bool gone = false;
            StartCoroutine(Watch(() => spirit.Place, () => gone, 58f));
            yield return spirit.Ride(way, () => at, true);
            yield return spirit.Vanish(RibbonSpirit.On(mark));
            yield return new WaitForSeconds(0.6f);
            gone = true;
            // Later, at the farm: a hard cut, as if the game between were cut out.
            ToFarm();
        }

        /// <summary>The hero on the road by the farm, Hilda before him, searching.</summary>
        void ToFarm()
        {
            dialogue.Release();
            ghost.SetFade(0f);
            Put(hildaS, hilda.transform.position);
            // A little way off her, on the line from the road to the cart: the farmyard behind her.
            var at = hildaHome - fromRoad * 11f;
            var level = world.ToLevel(at);
            at = world.FromLevel(new Vector3(level.x, Land.Height(level.x, level.z) + 0.05f, level.z));
            var body = hero.GetComponent<CharacterController>();
            body.enabled = false;
            hero.transform.SetPositionAndRotation(at, Quaternion.LookRotation(fromRoad));
            body.enabled = true;
            view.Restart(Mathf.Atan2(fromRoad.x, fromRoad.z) * Mathf.Rad2Deg);
            step = Step.Farm;
        }

        IEnumerator TalkToHilda()
        {
            var her = hilda.Speaker;
            hilda.searching = false;
            dialogue.Begin(her);
            yield return hilda.TurnTo(hero.transform.position);
            if (!greeted)
            {
                greeted = true;
                yield return Say(her, me, "hilda_busy", "Not now, stranger. I've lost my keys, and the cows won't wait.");
            }
            bool found = false;
            while (true)
            {
                var answers = new List<string> { "What keys?" };
                if (metSpirit) answers.Add("A wheel that goes nowhere keeps them.");
                answers.Add("I'll leave you to it.");
                int chosen = 0;
                yield return dialogue.Choose(her, answers.ToArray(), c => chosen = c);
                string answer = answers[chosen];
                if (answer == "What keys?")
                {
                    yield return Say(me, her, "hero_keys", answer);
                    yield return Say(her, me, "hilda_keys", "The barn keys. Two iron keys on a ring. I had them this morning, I know I did.");
                    yield return Say(her, me, "hilda_cows", "If I don't find them, the cows stay locked in. By sundown the whole valley will hear them.");
                    if (!toldOfKeys)
                    {
                        toldOfKeys = true;
                        dialogue.Note("New log entry", "The Barn Keys");
                    }
                }
                else if (answer == "I'll leave you to it.")
                {
                    yield return Say(me, her, "hero_leave", answer);
                    yield return Say(her, me, "hilda_leave", "Mm. Mind the cows.");
                    break;
                }
                else
                {
                    yield return Say(me, her, "hero_wheel", answer);
                    yield return Say(her, me, "hilda_wheel", "A wheel that... what kind of talk is that?");
                    yield return Say(her, me, "hilda_cart", "Wait. The old cart! That cart hasn't moved in ten years. I sat on it at noon to rest my legs.");
                    yield return Found();
                    found = true;
                    break;
                }
            }
            dialogue.Release();
            hilda.searching = !found;
            if (found) step = Step.Done;
        }

        /// <summary>She goes to the cart, kneels by its wheel, and has them; back to the hero.</summary>
        IEnumerator Found()
        {
            var her = hilda.Speaker;
            var standing = hilda.transform.position;
            bool walking = true;
            StartCoroutine(Watch(() => hilda.transform.position + Vector3.up * 1.2f, () => !walking, 44f));
            var by = keysAt + keysOut * 0.65f;
            by.y = keysAt.y;
            yield return hilda.WalkTo(by);
            walking = false;
            yield return null;
            // Close by the wheel, from out beside the cart: she kneels, and has them.
            var along = cartTurn * Vector3.forward;
            if (Vector3.Dot(standing - keysAt, along) < 0) along = -along;
            dialogue.Shot(keysAt + keysOut * 3.4f + along * 2.3f + Vector3.up * 1.45f, keysAt + keysOut * 0.45f + Vector3.up * 0.6f, 42f);
            hilda.Face(keysAt, true);
            hilda.Play("Kneel", 0.35f);
            yield return new WaitForSeconds(1.3f);
            keys.gameObject.SetActive(false);
            dialogue.Play(keysSound);
            yield return new WaitForSeconds(0.9f);
            hilda.Play("Rise", 0.2f);
            yield return new WaitForSeconds(1.1f);
            hilda.Play("Idle", 0.3f);
            yield return hilda.TurnTo(hero.transform.position);
            yield return Say(her, null, "hilda_found", "There they are! Right under the wheel.");
            walking = true;
            StartCoroutine(Watch(() => hilda.transform.position + Vector3.up * 1.2f, () => !walking, 44f));
            yield return hilda.WalkTo(standing);
            walking = false;
            yield return hilda.TurnTo(hero.transform.position);
            yield return Say(her, me, "hilda_how", "How did you know that?");
            yield return dialogue.Choose(her, new[] { "Lucky guess." }, c => { });
            yield return Say(me, her, "hero_guess", "Lucky guess.");
            yield return Say(her, me, "hilda_guess", "Some guess.");
            yield return Say(her, me, "hilda_thanks", "Thank you, all the same. Here, take this bread. I baked it this morning.");
            dialogue.Note("Quest completed: The Barn Keys", "Experience + 100");
        }

        void OnGUI()
        {
            if (step != Step.Farm || Dialogue.Cinematic || !Near() || Event.current.type != EventType.Repaint) return;
            hintStyle ??= new GUIStyle(GUI.skin.label) { font = dialogue.font, alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(Screen.height / 40f), normal = { textColor = new Color(0.93f, 0.78f, 0.47f) } };
            GUI.Label(new Rect(Screen.width / 2f - 150, Screen.height * 0.78f, 300, 40), "E  —  talk", hintStyle);
        }
    }
}
