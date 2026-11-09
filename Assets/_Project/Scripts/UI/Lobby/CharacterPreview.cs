using RiskOfMagic.Characters;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace RiskOfMagic.UI
{
    /// <summary>
    /// Turntable preview of a character rendered into a RawImage: the CharacterDefinition.modelPrefab, or a capsule
    /// placeholder in the character's color. The stage (camera, light, pedestal) is created far below the menu scene.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class CharacterPreview : MonoBehaviour
    {
        [SerializeField] private Vector3 stagePosition = new(0f, -500f, 0f);
        [SerializeField] private Vector2Int resolution = new(768, 960);
        [SerializeField] private float rotationSpeed = 25f;
        [SerializeField] private Color background = new(0.08f, 0.07f, 0.1f, 1f);
        [SerializeField] private Vector3 cameraOffset = new(0f, 1.15f, 3.6f);
        [SerializeField] private float lookHeight = 0.95f;
        [SerializeField, Tooltip("Clear to transparent (the model floats over the menu).")] private bool transparentBackground;
        [SerializeField] private bool showPedestal = true;

        private RawImage image;
        private RenderTexture texture;
        private GameObject stage;
        private Transform pivot;
        private GameObject current;
        private CharacterDefinition shown;
        private Material placeholderMaterial;

        private void Awake()
        {
            image = GetComponent<RawImage>();
            texture = new RenderTexture(resolution.x, resolution.y, 24) { name = "CharacterPreview", antiAliasing = 4 };
            image.texture = texture;
            image.color = Color.white;
            BuildStage();
        }

        private void BuildStage()
        {
            stage = new GameObject("CharacterPreviewStage");
            stage.transform.position = stagePosition;
            pivot = new GameObject("Pivot").transform;
            pivot.SetParent(stage.transform, false);

            var camGo = new GameObject("PreviewCamera");
            camGo.transform.SetParent(stage.transform, false);
            camGo.transform.localPosition = cameraOffset;
            camGo.transform.LookAt(stage.transform.position + Vector3.up * lookHeight);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = transparentBackground ? Color.clear : background;
            cam.fieldOfView = 32f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 25f;
            cam.targetTexture = texture;

            var key = new GameObject("KeyLight").AddComponent<Light>();
            key.transform.SetParent(stage.transform, false);
            key.transform.localPosition = new Vector3(1.6f, 2.6f, 2.4f);
            key.type = LightType.Point;
            key.range = 10f;
            key.intensity = 4f;
            var rim = new GameObject("RimLight").AddComponent<Light>();
            rim.transform.SetParent(stage.transform, false);
            rim.transform.localPosition = new Vector3(-1.8f, 2.2f, -2f);
            rim.type = LightType.Point;
            rim.range = 10f;
            rim.intensity = 2.5f;
            rim.color = new Color(0.6f, 0.7f, 1f);

            if (!showPedestal)
                return;
            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "Pedestal";
            Destroy(pedestal.GetComponent<Collider>());
            pedestal.transform.SetParent(stage.transform, false);
            pedestal.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            pedestal.transform.localScale = new Vector3(1.6f, 0.05f, 1.6f);
            pedestal.GetComponent<Renderer>().sharedMaterial = NewMaterial(new Color(0.18f, 0.16f, 0.2f));
        }

        public void Show(CharacterDefinition character)
        {
            if (character == shown)
                return;
            shown = character;
            if (current != null)
                Destroy(current);
            current = null;
            if (character == null)
                return;

            if (character.modelPrefab != null)
            {
                current = Instantiate(character.modelPrefab, pivot);
                current.transform.localPosition = Vector3.zero;
                current.transform.localRotation = Quaternion.identity;
                foreach (var behaviour in current.GetComponentsInChildren<MonoBehaviour>(true))
                    behaviour.enabled = false; // pure display: no scripts (the Animator still plays its default state)
                return;
            }

            // Placeholder: body capsule + head, in the character's color.
            current = new GameObject("Placeholder");
            current.transform.SetParent(pivot, false);
            placeholderMaterial ??= NewMaterial(Color.white);
            var material = new Material(placeholderMaterial) { color = character.bodyColor };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", character.bodyColor);
            Part(PrimitiveType.Capsule, new Vector3(0f, 0.9f, 0f), new Vector3(0.8f, 0.9f, 0.8f), material);
            Part(PrimitiveType.Sphere, new Vector3(0f, 1.95f, 0f), Vector3.one * 0.5f, material);
        }

        private void Part(PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(current.transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static Material NewMaterial(Color color)
        {
            bool urp = GraphicsSettings.currentRenderPipeline != null;
            var shader = Shader.Find(urp ? "Universal Render Pipeline/Lit" : "Standard");
            var material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            return material;
        }

        // Only render while visible (several previews may exist in the menu).
        private void OnEnable()
        {
            if (stage != null)
                stage.SetActive(true);
        }

        private void OnDisable()
        {
            if (stage != null)
                stage.SetActive(false);
        }

        private void Update()
        {
            if (pivot != null)
                pivot.Rotate(0f, rotationSpeed * Time.unscaledDeltaTime, 0f, Space.Self);
        }

        private void OnDestroy()
        {
            if (stage != null)
                Destroy(stage);
            if (texture != null)
                texture.Release();
        }
    }
}
