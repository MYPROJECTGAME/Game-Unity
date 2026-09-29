using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

// Attach to ONE empty GameObject and press Play. The whole game builds itself.
public class HorrorGame : MonoBehaviour
{
    class Door { public Transform piv; public BoxCollider col; public bool open, locked; public float ang, w; public Vector3 c; }
    readonly List<Door> doors = new List<Door>();
    readonly List<Transform> keys = new List<Transform>();
    readonly List<Light> lamps = new List<Light>();
    readonly List<float> lampK = new List<float>();
    readonly List<Vector2> furn = new List<Vector2>();
    Transform player, cam, granny, ghostBillboard, scareGhost; CharacterController cc; Light flash;
    Door front, near; Material wallMat, wallPanelMat, doorMat, woodMat, gold, floorMat, bottleMat;
    Texture2D keyTexture, ghostTexture, floorTexture;
    AudioClip ambienceClip, childCryClip, ghostLaughClip, ghostScreamClip, dogHowlClip, thunderClip, keyClip, doorClip;
    AudioSource ambienceSource, musicSource;
    float soundT = 8f, proximitySoundT = 4f;
    float yaw, pitch, stamina = 1, chaseT, blackout, nextBlackout = 12, msgT, bob;
    int keyCount, state; float playerBlood = 100f, hitCooldown, scareT; bool firstScareShown; string msg = "";
    Vector2Int pN; bool hasN, hasW; Vector3 pW;

    Material Mat(Color c, bool emit = false)
    {
        var m = new Material(Shader.Find("Standard")); m.color = c;
        if (emit) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * 2f); }
        return m;
    }
    Material ImageMat(Texture2D texture, bool transparent)
    {
        Shader shader = Shader.Find(transparent ? "Unlit/Transparent" : "Unlit/Texture");
        var m = new Material(shader); m.mainTexture = texture; m.color = Color.white;
        if (transparent && m.HasProperty("_Cull")) m.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        return m;
    }
    Transform Billboard(string name, Vector3 pos, Vector3 scale, Texture2D texture, Transform parent)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
        g.name = name; g.transform.SetParent(parent); g.transform.localPosition = pos; g.transform.localScale = scale;
        Destroy(g.GetComponent<Collider>()); g.GetComponent<Renderer>().material = ImageMat(texture, true);
        return g.transform;
    }
    GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material, Quaternion rotation)
    {
        var g = GameObject.CreatePrimitive(type); g.name = name; g.transform.SetParent(parent, false);
        g.transform.localPosition = localPosition; g.transform.localScale = scale; g.transform.localRotation = rotation;
        g.GetComponent<Renderer>().material = material; Destroy(g.GetComponent<Collider>()); return g;
    }
    Transform CreateKey(Vector3 position)
    {
        var root = new GameObject("Golden Key").transform; root.position = position;
        Material keyMat = Mat(new Color(1f, .48f, .015f), true); Material insetMat = Mat(new Color(.16f, .07f, .02f));
        Part(PrimitiveType.Cylinder, "Key Head", root, new Vector3(0, .45f, 0), new Vector3(.36f, .07f, .36f), keyMat, Quaternion.Euler(90, 0, 0));
        Part(PrimitiveType.Cylinder, "Key Head Inset", root, new Vector3(0, .45f, .075f), new Vector3(.19f, .08f, .19f), insetMat, Quaternion.Euler(90, 0, 0));
        Part(PrimitiveType.Cube, "Key Shaft", root, new Vector3(0, -.05f, 0), new Vector3(.12f, .75f, .12f), keyMat, Quaternion.identity);
        Part(PrimitiveType.Cube, "Key Tooth A", root, new Vector3(.16f, -.38f, 0), new Vector3(.24f, .14f, .12f), keyMat, Quaternion.identity);
        Part(PrimitiveType.Cube, "Key Tooth B", root, new Vector3(-.15f, -.52f, 0), new Vector3(.22f, .14f, .12f), keyMat, Quaternion.identity);
        var glow = new GameObject("Key Glow"); glow.transform.SetParent(root, false); glow.transform.localPosition = new Vector3(0, .35f, 0);
        var light = glow.AddComponent<Light>(); light.type = LightType.Point; light.color = new Color(1f, .46f, .05f); light.range = 2.5f; light.intensity = .55f;
        return root;
    }
    AudioClip MakeClip(string name, float seconds, System.Func<float, float> generator)
    {
        const int rate = 22050;
        int length = Mathf.CeilToInt(seconds * rate);
        var clip = AudioClip.Create(name, length, 1, rate, false);
        var samples = new float[length];
        for (int i = 0; i < length; i++)
        {
            float t = i / (float)rate;
            float edge = Mathf.Min(1f, t * 12f) * Mathf.Min(1f, (seconds - t) * 8f);
            samples[i] = Mathf.Clamp(generator(t) * edge, -1f, 1f);
        }
        clip.SetData(samples, 0);
        return clip;
    }
    void BuildScaryAudio()
    {
        ambienceClip = MakeClip("House Drone", 6f, t => Mathf.Sin(t * 2.1f) * .08f + Mathf.Sin(t * 7.7f) * .025f);
        childCryClip = MakeClip("Distant Child Cry", 4.4f, t =>
        {
            float sob = .35f + Mathf.Abs(Mathf.Sin(t * 2.2f)) * .65f;
            float pitch = 510f + Mathf.Sin(t * 3.7f) * 120f;
            return (Mathf.Sin(t * pitch) + Mathf.Sin(t * pitch * 2.01f) * .25f) * sob * .34f;
        });
        ghostLaughClip = MakeClip("Ghost Laugh", 2.8f, t =>
        {
            float pulse = Mathf.Abs(Mathf.Sin(t * 4.2f));
            float pitch = 180f + Mathf.Sin(t * 5f) * 55f;
            return (Mathf.Sin(t * pitch) + Mathf.Sin(t * pitch * 1.9f) * .35f) * pulse * .42f;
        });
        ghostScreamClip = MakeClip("Ghost Scream", 3.1f, t =>
        {
            float pitch = 230f + t * 310f + Mathf.Sin(t * 18f) * 50f;
            return (Mathf.Sin(t * pitch) + Mathf.Sin(t * pitch * 2.03f) * .2f) * .38f;
        });
        dogHowlClip = MakeClip("Distant Dog Howl", 3.8f, t =>
        {
            float pitch = 260f + Mathf.Sin(t * 1.6f) * 95f;
            return (Mathf.Sin(t * pitch) + Mathf.Sin(t * pitch * 2f) * .3f) * .3f;
        });
        thunderClip = MakeClip("House Thunder", 2.6f, t =>
        {
            float crack = Mathf.PerlinNoise(t * 90f, .4f) * 2f - 1f;
            return crack * Mathf.Exp(-t * 2.6f) * .55f + Mathf.Sin(t * 34f) * .12f;
        });
        keyClip = MakeClip("Key Chime", 1.2f, t => Mathf.Sin(t * 1100f) * .24f + Mathf.Sin(t * 1650f) * .12f);
        doorClip = MakeClip("Door Creak", 1.5f, t => Mathf.Sin(t * (130f + t * 280f)) * .18f + Mathf.Sin(t * 41f) * .12f);
        var go = new GameObject("House Ambience");
        ambienceSource = go.AddComponent<AudioSource>(); ambienceSource.clip = ambienceClip; ambienceSource.loop = true;
        ambienceSource.volume = .22f; ambienceSource.spatialBlend = 0f; ambienceSource.Play();
    }
    void PlayScare(AudioClip clip, Vector3 position, float volume)
    {
        if (clip != null) AudioSource.PlayClipAtPoint(clip, position, volume);
    }
    void ShowScare(float duration)
    {
        if (ghostTexture == null) return;
        if (scareGhost != null) Destroy(scareGhost.gameObject);
        scareGhost = Billboard("Granny Jumpscare", new Vector3(0, .15f, 1.25f), new Vector3(1.55f, 3.1f, 1), ghostTexture, cam);
        scareGhost.localRotation = Quaternion.Euler(0, 180, 0); scareT = duration;
    }
    GameObject P(PrimitiveType t, Vector3 pos, Vector3 sc, Material m, Transform par = null, bool col = true)
    {
        var g = GameObject.CreatePrimitive(t);
        if (!col) Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(par); g.transform.position = pos; g.transform.localScale = sc;
        g.GetComponent<Renderer>().material = m; return g;
    }
    void Wall(float x1, float z1, float x2, float z2)
    {
        float t = .4f, w = Mathf.Abs(x2 - x1) + (x1 == x2 ? t : 0), d = Mathf.Abs(z2 - z1) + (z1 == z2 ? t : 0);
        P(PrimitiveType.Cube, new Vector3((x1 + x2) / 2, 1.5f, (z1 + z2) / 2), new Vector3(w, 3, d), wallMat);
        if (z1 == z2)
        {
            P(PrimitiveType.Cube, new Vector3((x1 + x2) / 2, 1.5f, z1 + .215f), new Vector3(Mathf.Max(.5f, w - .22f), 2.58f, .035f), wallPanelMat, null, false);
            P(PrimitiveType.Cube, new Vector3((x1 + x2) / 2, 1.5f, z1 - .215f), new Vector3(Mathf.Max(.5f, w - .22f), 2.58f, .035f), wallPanelMat, null, false);
        }
        else
        {
            P(PrimitiveType.Cube, new Vector3(x1 + .215f, 1.5f, (z1 + z2) / 2), new Vector3(.035f, 2.58f, Mathf.Max(.5f, d - .22f)), wallPanelMat, null, false);
            P(PrimitiveType.Cube, new Vector3(x1 - .215f, 1.5f, (z1 + z2) / 2), new Vector3(.035f, 2.58f, Mathf.Max(.5f, d - .22f)), wallPanelMat, null, false);
        }
    }
    void AddMissingText(Vector3 position, Vector3 normal)
    {
        var g = new GameObject("MISSING warning"); g.transform.position = position; g.transform.rotation = Quaternion.LookRotation(normal);
        var text = g.AddComponent<TextMesh>(); text.text = "MISSING"; text.fontSize = 48; text.characterSize = .16f;
        text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center; text.color = new Color(.72f, .015f, .015f);
        var renderer = g.GetComponent<MeshRenderer>(); renderer.material = Mat(new Color(.72f, .015f, .015f), true);
    }
    void AddRoomFurniture(Vector3 center)
    {
        Material furnitureMat = Mat(new Color(.25f, .12f, .055f)); Material chairMat = Mat(new Color(.16f, .075f, .035f));
        P(PrimitiveType.Cube, center + new Vector3(0, 1.05f, 0), new Vector3(2.7f, .16f, 1.25f), furnitureMat);
        for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
            P(PrimitiveType.Cube, center + new Vector3(x * 1.05f, .52f, z * .42f), new Vector3(.12f, 1f, .12f), furnitureMat);
        P(PrimitiveType.Cube, center + new Vector3(2.1f, 1.1f, .5f), new Vector3(.8f, 2.2f, .7f), furnitureMat);
        P(PrimitiveType.Cube, center + new Vector3(-2.2f, .7f, .3f), new Vector3(1.05f, .12f, 1.05f), chairMat);
        P(PrimitiveType.Cube, center + new Vector3(-2.65f, 1.35f, .3f), new Vector3(.12f, 1.3f, 1.05f), chairMat);
        P(PrimitiveType.Cube, center + new Vector3(-2.65f, .7f, -.2f), new Vector3(.12f, 1.2f, .12f), chairMat);
        P(PrimitiveType.Cube, center + new Vector3(-1.75f, .7f, -.2f), new Vector3(.12f, 1.2f, .12f), chairMat);
        var bottle = P(PrimitiveType.Cylinder, center + new Vector3(.55f, 1.38f, 0), new Vector3(.16f, .28f, .16f), bottleMat, null, false);
        P(PrimitiveType.Cylinder, center + new Vector3(.55f, 1.7f, 0), new Vector3(.19f, .05f, .19f), bottleMat, null, false);
        furn.Add(new Vector2(center.x, center.z));
    }
    Door AddDoor(float hx, float hz, bool ax, bool locked = false)
    {
        var piv = new GameObject("Door").transform; piv.position = new Vector3(hx, 0, hz);
        var m = P(PrimitiveType.Cube, Vector3.zero, ax ? new Vector3(2.5f, 2.6f, .12f) : new Vector3(.12f, 2.6f, 2.5f), doorMat, piv);
        m.transform.localPosition = ax ? new Vector3(1.25f, 1.3f, 0) : new Vector3(0, 1.3f, 1.25f);
        Material trim = Mat(new Color(.13f, .065f, .028f)); Material panel = Mat(new Color(.38f, .20f, .09f));
        if (ax)
        {
            Part(PrimitiveType.Cube, "Door Panel Top", piv, new Vector3(1.25f, 2.0f, -.09f), new Vector3(1.5f, .72f, .08f), panel, Quaternion.identity);
            Part(PrimitiveType.Cube, "Door Panel Middle", piv, new Vector3(1.25f, 1.27f, -.09f), new Vector3(1.5f, .25f, .08f), panel, Quaternion.identity);
            Part(PrimitiveType.Cube, "Door Panel Bottom", piv, new Vector3(1.25f, .52f, -.09f), new Vector3(1.5f, .72f, .08f), panel, Quaternion.identity);
            Part(PrimitiveType.Cube, "Door Frame Left", piv, new Vector3(.06f, 1.35f, -.1f), new Vector3(.12f, 2.85f, .16f), trim, Quaternion.identity);
            Part(PrimitiveType.Cube, "Door Frame Right", piv, new Vector3(2.44f, 1.35f, -.1f), new Vector3(.12f, 2.85f, .16f), trim, Quaternion.identity);
            Part(PrimitiveType.Cube, "Door Frame Top", piv, new Vector3(1.25f, 2.74f, -.1f), new Vector3(2.5f, .14f, .16f), trim, Quaternion.identity);
            Part(PrimitiveType.Cylinder, "Door Handle", piv, new Vector3(2.12f, 1.35f, -.18f), new Vector3(.08f, .16f, .08f), gold, Quaternion.Euler(90, 0, 0));
        }
        else
        {
            Part(PrimitiveType.Cube, "Door Panel Top", piv, new Vector3(-.09f, 2.0f, 1.25f), new Vector3(.08f, .72f, 1.5f), panel, Quaternion.identity);
            Part(PrimitiveType.Cube, "Door Panel Middle", piv, new Vector3(-.09f, 1.27f, 1.25f), new Vector3(.08f, .25f, 1.5f), panel, Quaternion.identity);
            Part(PrimitiveType.Cube, "Door Panel Bottom", piv, new Vector3(-.09f, .52f, 1.25f), new Vector3(.08f, .72f, 1.5f), panel, Quaternion.identity);
            Part(PrimitiveType.Cube, "Door Frame Left", piv, new Vector3(-.1f, 1.35f, .06f), new Vector3(.16f, 2.85f, .12f), trim, Quaternion.identity);
            Part(PrimitiveType.Cube, "Door Frame Right", piv, new Vector3(-.1f, 1.35f, 2.44f), new Vector3(.16f, 2.85f, .12f), trim, Quaternion.identity);
            Part(PrimitiveType.Cube, "Door Frame Top", piv, new Vector3(-.1f, 2.74f, 1.25f), new Vector3(.16f, .14f, 2.5f), trim, Quaternion.identity);
            Part(PrimitiveType.Cylinder, "Door Handle", piv, new Vector3(-.18f, 1.35f, 2.12f), new Vector3(.08f, .16f, .08f), gold, Quaternion.Euler(0, 0, 90));
        }
        var d = new Door { piv = piv, col = m.GetComponent<BoxCollider>(), locked = locked, c = piv.position + (ax ? new Vector3(1.25f, 0, 0) : new Vector3(0, 0, 1.25f)) };
        doors.Add(d); return d;
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        foreach (var l in FindObjectsOfType<Light>()) if (l.type == LightType.Directional) l.enabled = false;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.07f, .07f, .09f);
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = .08f; RenderSettings.fogColor = Color.black;
        keyTexture = Resources.Load<Texture2D>("GoldenKey");
        ghostTexture = Resources.Load<Texture2D>("Granny");
        if (ghostTexture == null) ghostTexture = Resources.Load<Texture2D>("GhostWoman");
        floorTexture = Resources.Load<Texture2D>("WoodFloor");
        AudioClip theme = Resources.Load<AudioClip>("MainMenuTheme");
        BuildScaryAudio();
        if (theme != null)
        {
            var musicObject = new GameObject("Main Menu Theme"); musicSource = musicObject.AddComponent<AudioSource>();
            musicSource.clip = theme; musicSource.loop = true; musicSource.volume = .34f; musicSource.spatialBlend = 0f; musicSource.Play();
        }
        wallMat = Mat(new Color(.42f, .40f, .36f)); wallPanelMat = Mat(new Color(.72f, .70f, .64f));
        doorMat = Mat(new Color(.29f, .17f, .09f)); woodMat = Mat(new Color(.17f, .1f, .06f)); gold = Mat(new Color(1f, .8f, .2f), true);
        bottleMat = Mat(new Color(.32f, .72f, .82f));
        floorMat = ImageMat(floorTexture, false); floorMat.mainTextureScale = new Vector2(8, 8);

        // floor, ceiling, ground, gable roof
        P(PrimitiveType.Cube, new Vector3(15, -.1f, 15), new Vector3(30, .2f, 30), floorMat);
        P(PrimitiveType.Cube, new Vector3(15, 3.1f, 15), new Vector3(30, .2f, 30), Mat(new Color(.1f, .08f, .07f)));
        P(PrimitiveType.Cube, new Vector3(15, -.2f, 15), new Vector3(300, .2f, 300), Mat(new Color(.04f, .07f, .04f)));
        var rm = Mat(new Color(.16f, .08f, .06f));
        var r1 = P(PrimitiveType.Cube, new Vector3(15, 6.7f, 7), new Vector3(32, .4f, 17.6f), rm, null, false); r1.transform.rotation = Quaternion.Euler(-25, 0, 0);
        var r2 = P(PrimitiveType.Cube, new Vector3(15, 6.7f, 23), new Vector3(32, .4f, 17.6f), rm, null, false); r2.transform.rotation = Quaternion.Euler(25, 0, 0);

        // walls + doors
        Wall(0, 0, 30, 0); Wall(0, 0, 0, 30); Wall(30, 0, 30, 30); Wall(0, 30, 13.75f, 30); Wall(16.25f, 30, 30, 30);
        front = AddDoor(13.75f, 30, true, true);
        foreach (float c in new[] { 10f, 20f })
            for (int i = 0; i < 3; i++)
            {
                float a = i * 10;
                Wall(a, c, a + 3.75f, c); Wall(a + 6.25f, c, a + 10, c); Wall(c, a, c, a + 3.75f); Wall(c, a + 6.25f, c, a + 10);
                AddDoor(a + 3.75f, c, true); AddDoor(c, a + 3.75f, false);
            }
        AddMissingText(new Vector3(5, 1.65f, 9.77f), Vector3.forward);
        AddMissingText(new Vector3(24.8f, 1.65f, 15f), Vector3.left);
        AddMissingText(new Vector3(15f, 1.65f, 20.23f), Vector3.back);

        // furniture + lamps (one per room)
        Color[] lc = { new Color(1, .33f, .13f), new Color(1, .67f, .33f), new Color(.4f, 1, .53f), new Color(1, .13f, .2f) };
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
            {
                float fx = i * 10 + 1.8f + ((i + j) % 2) * 5, fz = j * 10 + 1.8f;
                P(PrimitiveType.Cube, new Vector3(fx + .9f, .5f, fz + .9f), new Vector3(1.8f, 1, 1.8f), woodMat); furn.Add(new Vector2(fx + .9f, fz + .9f));
                AddRoomFurniture(new Vector3(i * 10 + 5, 0, j * 10 + 5));
                var lg = new GameObject("Lamp"); lg.transform.position = new Vector3(i * 10 + 5, 2.6f, j * 10 + 5);
                var L = lg.AddComponent<Light>(); L.type = LightType.Point; L.range = 13; L.intensity = 1.2f; L.color = lc[(i * 3 + j) % 4];
                lamps.Add(L); lampK.Add(0);
            }

        // 10 keys (never in the start room)
        var rooms = new List<Vector2Int>();
        for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) if (!(i == 1 && j == 0)) rooms.Add(new Vector2Int(i, j));
        for (int n = 0; n < 10; n++)
        {
            var r = rooms[n % 8]; Vector2 p;
            do { p = new Vector2(r.x * 10 + 5 + Random.Range(-3.5f, 3.5f), r.y * 10 + 5 + Random.Range(-3.5f, 3.5f)); }
            while (furn.Exists(f => Vector2.Distance(f, p) < 2f));
            keys.Add(CreateKey(new Vector3(p.x, 1.05f, p.y)));
        }

        // player
        var pg = new GameObject("Player"); pg.transform.position = new Vector3(15, .1f, 4);
        cc = pg.AddComponent<CharacterController>(); cc.height = 1.8f; cc.radius = .4f; cc.center = new Vector3(0, .9f, 0);
        player = pg.transform;
        var cm = Camera.main; if (cm == null) { cm = new GameObject("Cam").AddComponent<Camera>(); cm.tag = "MainCamera"; cm.gameObject.AddComponent<AudioListener>(); }
        cam = cm.transform; cam.SetParent(player); cam.localPosition = new Vector3(0, 1.6f, 0); cam.localRotation = Quaternion.identity;
        cm.clearFlags = CameraClearFlags.SolidColor; cm.backgroundColor = Color.black;
        var fg = new GameObject("Flashlight"); fg.transform.SetParent(cam, false);
        flash = fg.AddComponent<Light>(); flash.type = LightType.Spot; flash.range = 24; flash.spotAngle = 60; flash.intensity = 2f; flash.color = new Color(1, .95f, .8f);

        // granny
        granny = new GameObject("Granny").transform;
        if (ghostTexture == null)
        {
            P(PrimitiveType.Capsule, new Vector3(0, .65f, 0), new Vector3(.6f, .65f, .6f), Mat(new Color(.16f, .09f, .19f)), granny, false);
            P(PrimitiveType.Sphere, new Vector3(0, 1.5f, 0), Vector3.one * .45f, Mat(new Color(.79f, .76f, .7f)), granny, false);
            P(PrimitiveType.Sphere, new Vector3(0, 1.58f, -.07f), new Vector3(.5f, .45f, .45f), Mat(new Color(.6f, .6f, .6f)), granny, false);
            var em = Mat(Color.red, true);
            P(PrimitiveType.Sphere, new Vector3(-.09f, 1.53f, .2f), Vector3.one * .07f, em, granny, false);
            P(PrimitiveType.Sphere, new Vector3(.09f, 1.53f, .2f), Vector3.one * .07f, em, granny, false);
            P(PrimitiveType.Cube, new Vector3(-.3f, 1.15f, .4f), new Vector3(.09f, .09f, .8f), Mat(new Color(.79f, .76f, .7f)), granny, false);
            P(PrimitiveType.Cube, new Vector3(.3f, 1.15f, .4f), new Vector3(.09f, .09f, .8f), Mat(new Color(.79f, .76f, .7f)), granny, false);
        }
        var gl = new GameObject("GrannyGlow"); gl.transform.SetParent(granny, false); gl.transform.localPosition = new Vector3(0, 1.3f, 0);
        var gL = gl.AddComponent<Light>(); gL.color = Color.red; gL.range = 7; gL.intensity = 1f;
        if (ghostTexture != null)
        {
            ghostBillboard = Billboard("Granny Image", new Vector3(0, 1.5f, .05f), new Vector3(1.5f, 3f, 1), ghostTexture, granny);
        }
        else
        {
            ghostBillboard = null;
            Say("Ghost image missing: using 3D fallback.", 4);
        }
        granny.position = new Vector3(5, 0, 25);
        Say("Find 10 keys. Stay quiet. She is listening...", 4);
    }

    Vector2Int Cell(Vector3 p) { return new Vector2Int(Mathf.Clamp((int)(p.x / 10), 0, 2), Mathf.Clamp((int)(p.z / 10), 0, 2)); }
    Vector2Int Step(Vector2Int g, Vector2Int p)
    {
        return g.x != p.x ? new Vector2Int(g.x + (int)Mathf.Sign(p.x - g.x), g.y) : new Vector2Int(g.x, g.y + (int)Mathf.Sign(p.y - g.y));
    }
    Vector2Int Nb(Vector2Int g)
    {
        var o = new List<Vector2Int>();
        foreach (var d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down }) { var c = g + d; if (c.x >= 0 && c.x < 3 && c.y >= 0 && c.y < 3) o.Add(c); }
        return o[Random.Range(0, o.Count)];
    }
    float Dist(Vector3 a, Vector3 b) { return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z)); }
    Vector3 DoorPt(Vector2Int g, Vector2Int n)
    {
        bool ax = n.x != g.x;
        float b = 10 * Mathf.Max(ax ? g.x : g.y, ax ? n.x : n.y), s = Mathf.Sign(ax ? n.x - g.x : n.y - g.y), l = (ax ? g.y : g.x) * 10 + 5;
        Vector3 gp = granny.position; float nd = ax ? gp.x : gp.z, la = ax ? gp.z : gp.x;
        float o = (Mathf.Abs(nd - b) > 1.5f || Mathf.Abs(la - l) > 1f) ? b - s * 1.2f : b + s * .8f;
        return ax ? new Vector3(o, 0, l) : new Vector3(l, 0, o);
    }
    Vector3 Target(bool chase)
    {
        var g = Cell(granny.position); var p = Cell(player.position);
        if (chase) { if (g == p) return player.position; return DoorPt(g, Step(g, p)); }
        if (hasN && g != pN) return DoorPt(g, Step(g, pN));
        hasN = false;
        if (!hasW) { pW = new Vector3(g.x * 10 + 5 + Random.Range(-2.5f, 2.5f), 0, g.y * 10 + 5 + Random.Range(-2.5f, 2.5f)); hasW = true; }
        if (Dist(granny.position, pW) < .7f) { hasW = false; pN = Nb(g); hasN = true; return DoorPt(g, Step(g, pN)); }
        return pW;
    }
    bool GrannyCanDetect(Vector3 grannyPosition, Vector3 playerPosition, bool sprinting, Vector2Int grannyCell, Vector2Int playerCell)
    {
        Vector3 toPlayer = playerPosition - grannyPosition; toPlayer.y = 0;
        float distance = toPlayer.magnitude;
        if (distance < .01f) return true;
        float facing = Vector3.Dot(granny.forward, toPlayer / distance);
        bool insideVisionCone = facing > -.15f && (grannyCell == playerCell || distance < 5f);
        bool makingNoise = sprinting && distance < 12f;
        return insideVisionCone || makingNoise;
    }
    bool Open(Door D, bool o)
    {
        if (!o && (Dist(D.c, player.position) < 1.3f || Dist(D.c, granny.position) < 1.6f)) return false;
        D.open = o; D.col.enabled = !o; D.w = 0; return true;
    }
    void Say(string s, float t) { msg = s; msgT = t; }

    void Update()
    {
        if (state != 0) { if (Input.GetKeyDown(KeyCode.R)) { Time.timeScale = 1; SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); } return; }
        float dt = Time.deltaTime; msgT -= dt; hitCooldown -= dt;
        if (scareGhost != null && scareT > 0f)
        {
            scareT -= dt;
            if (scareT <= 0f) { Destroy(scareGhost.gameObject); scareGhost = null; }
        }

        // player
        yaw += Input.GetAxis("Mouse X") * 2f; pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * 2f, -80, 80);
        player.rotation = Quaternion.Euler(0, yaw, 0); cam.localRotation = Quaternion.Euler(pitch, 0, 0);
        Vector3 dir = player.forward * Input.GetAxisRaw("Vertical") + player.right * Input.GetAxisRaw("Horizontal");
        if (dir.magnitude > 1) dir.Normalize();
        bool sp = Input.GetKey(KeyCode.LeftShift) && stamina > .02f && dir.sqrMagnitude > 0;
        stamina = sp ? Mathf.Max(0, stamina - dt * .35f) : Mathf.Min(1, stamina + dt * .12f);
        cc.Move((dir * (sp ? 5.6f : 3.2f) + Vector3.down * 2f) * dt);
        if (dir.sqrMagnitude > 0) bob += dt * (sp ? 12 : 8);
        cam.localPosition = new Vector3(0, 1.6f + Mathf.Sin(bob) * .045f, 0);
        Vector3 pp = player.position, gp = granny.position;

        // keys
        for (int i = keys.Count - 1; i >= 0; i--)
        {
            var k = keys[i]; k.LookAt(cam); k.Rotate(0, 0, 45 * dt);
            if (Vector3.Distance(k.position, pp + Vector3.up) < 1.3f)
            {
                Destroy(k.gameObject); keys.RemoveAt(i); keyCount++;
                PlayScare(keyClip, pp, .55f);
                if (keyCount == 1 && !firstScareShown) { firstScareShown = true; ShowScare(2.2f); Say("Something is watching you...", 3f); }
                if (keyCount >= 10) { front.locked = false; Open(front, true); Say("The front door is unlocked! RUN!", 5); }
                else Say("Key found: " + keyCount + "/10", 2.5f);
            }
        }

        // doors
        near = null; float nq = 2.3f;
        foreach (var D in doors)
        {
            D.ang = Mathf.Lerp(D.ang, D.open ? 1 : 0, dt * 5); D.piv.rotation = Quaternion.Euler(0, D.ang * 88f, 0);
            float q = Dist(D.c, pp); if (q < nq) { nq = q; near = D; }
        }
        if (Input.GetKeyDown(KeyCode.E) && near != null)
        {
            if (near.locked) Say("Locked. You need all 10 keys (" + keyCount + "/10)", 2);
            else if (!Open(near, !near.open)) Say("Something is in the way!", 1);
            else PlayScare(doorClip, near.c, .35f);
        }

        // granny
        float d = Dist(gp, pp); var g = Cell(gp); var p = Cell(pp);
        if (GrannyCanDetect(gp, pp, sp, g, p)) chaseT = 4; chaseT -= dt; bool chase = chaseT > 0;
        soundT -= dt; proximitySoundT -= dt;
        if (d < 9f && proximitySoundT <= 0f)
        {
            proximitySoundT = Random.Range(7f, 13f); PlayScare(d < 4f ? ghostLaughClip : childCryClip, gp, d < 4f ? .75f : .38f);
        }
        if (soundT <= 0f)
        {
            soundT = Random.Range(12f, 24f);
            int choice = Random.Range(0, 3);
            PlayScare(choice == 0 ? dogHowlClip : choice == 1 ? childCryClip : thunderClip,
                new Vector3(Random.Range(2f, 28f), 1f, Random.Range(2f, 28f)), .3f);
        }
        Vector3 v = Target(chase) - gp; v.y = 0; float dd = v.magnitude; bool blocked = false;
        if (ghostBillboard != null) ghostBillboard.LookAt(cam);
        foreach (var D in doors)
            if (!D.open && !D.locked && Dist(D.c, gp) < 1.5f) { blocked = true; D.w += dt; if (D.w > (chase ? .6f : 1.2f)) Open(D, true); }
        if (!blocked && dd > .01f)
        {
            float spd = chase ? 2.6f + keyCount * .19f : 1.5f;
            granny.position += v / dd * Mathf.Min(dd, spd * dt); granny.rotation = Quaternion.LookRotation(v / dd);
        }
        if (d < 1.1f && chase && hitCooldown <= 0f)
        {
            playerBlood = Mathf.Max(0f, playerBlood - 10f); hitCooldown = 1.5f; ShowScare(1.3f); PlayScare(ghostScreamClip, pp, .9f);
            if (playerBlood <= 0f) { state = 1; Time.timeScale = 0; Cursor.lockState = CursorLockMode.None; return; }
            Say("Granny hit you! Blood: " + Mathf.CeilToInt(playerBlood) + "/100", 2f); chaseT = 0f;
            Vector3 retreat = gp - pp; retreat.y = 0; if (retreat.sqrMagnitude > .01f) granny.position += retreat.normalized * 2.5f;
        }
        if (pp.z > 31 && keyCount >= 10) { state = 2; Cursor.lockState = CursorLockMode.None; return; }

        // scary lights
        blackout -= dt; nextBlackout -= dt;
        if (nextBlackout <= 0) { nextBlackout = 22 + Random.Range(0, 20f); blackout = 1.8f; PlayScare(thunderClip, pp, .65f); Say("The lights died...", 1.6f); }
        for (int i = 0; i < lamps.Count; i++)
        {
            if (Random.value < .008f + (d < 10 ? .05f : 0f)) lampK[i] = .15f + Random.value * .3f;
            lampK[i] = Mathf.Max(0, lampK[i] - dt);
            lamps[i].intensity = 1.2f * (blackout > 0 ? 0 : (lampK[i] > 0 ? (Random.value < .5f ? .1f : .7f) : 1f));
        }
        flash.intensity = blackout > 0 ? .4f : (Random.value < .01f ? .3f : 2f);
    }

    void OnGUI()
    {
        var l = new GUIStyle(GUI.skin.label) { fontSize = 22 }; l.normal.textColor = Color.white;
        var c = new GUIStyle(l) { alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(15, 10, 500, 30), "Keys: " + keyCount + "/10", l);
        GUI.Label(new Rect(15, 40, 500, 30), "Stamina: " + new string('|', Mathf.RoundToInt(stamina * 20)), l);
        GUI.Label(new Rect(15, 70, 500, 30), "Blood: " + Mathf.CeilToInt(playerBlood) + "/100", l);
        if (near != null && state == 0) GUI.Label(new Rect(0, Screen.height * .65f, Screen.width, 40), "[E] " + (near.open ? "Close" : "Open") + " door", c);
        if (msgT > 0) GUI.Label(new Rect(0, Screen.height * .8f, Screen.width, 40), msg, c);
        if (state != 0)
        {
            c.fontSize = 48; c.normal.textColor = state == 1 ? Color.red : Color.green;
            GUI.Label(new Rect(0, Screen.height * .35f, Screen.width, 80), state == 1 ? "GRANNY CAUGHT YOU" : "YOU ESCAPED", c);
            c.fontSize = 22; c.normal.textColor = Color.white;
            GUI.Label(new Rect(0, Screen.height * .5f, Screen.width, 40), "Press R to restart", c);
        }
    }
}
