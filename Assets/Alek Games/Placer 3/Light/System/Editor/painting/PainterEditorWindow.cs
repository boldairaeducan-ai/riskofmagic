using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEditor;

using AlekGames.Placer3.Profiles;
using AlekGames.Placer3.Systems.Addons;
using AlekGames.Placer3.Shared;
using AlekGames.Placer3.Editor;
using UnityEditor.TestTools.TestRunner.Api;
using static Codice.CM.Common.Purge.PurgeReport;

namespace AlekGames.Placer3.Systems.Main
{
    public class PainterEditorWindow : EditorWindow, IPrefabPalleteUser
    {
        #region values

        private PlacerDeafultsData deafults;

        public Transform transform;

        public PainterValues pValues;

        public float YRotationOffset;
        public bool enabledPaint { get; private set; }
        public Vector3 paintPos { get; private set; }
        public Vector3 paintNormal { get; private set; }

        public bool toBigNormal { get; private set; }

        public bool paintReturned { get; private set; }

        private bool isPlacing;

        public prefabPalette.objectSettings previewObjectData { get; private set; }
        public GameObject preview { get; private set; }

        private Vector3? lastSpawn = null;


        private string valuesName = "values";
        private Vector2 deafultsScrollPos;
        private Vector2 windowScrollPos;

        private string brushNote;
        private const string brushNoteObstacle = "detected an obstacle on the avoided layer\n";
        private const string brushNoteNoGround = "not pointing at any object on the ground layer\n";

        private bool helpWindows;

        #endregion


        #region editor drawing



        private void OnGUI()
        {
            Color old = GUI.color;
            if (enabledPaint) GUI.color = deafults.WorkColor;

            helpWindows = helpWindows ? !GUILayout.Button("Disable Help Windows") : GUILayout.Button("Enable Help Windows");

            GUILayout.Space(10);

            if (!PlacerEditorHelper.autoObjectSelectionField(ref transform)) return;

            windowScrollPos = EditorGUILayout.BeginScrollView(windowScrollPos);

            PainterValues lastV = pValues;


            lastV.paintMode = (PainterValues.paintM)EditorGUILayout.EnumPopup(new GUIContent("Paint Mode"), lastV.paintMode);
            if (helpWindows)
            {
                EditorGUILayout.HelpBox("you can switch between paint modes with 1,2,3,4 keys on your keybord when painting.\nyou can rotate the placed object with the R and T key\nwhen using these shourtcuts make sure you are focused on the scene window (holding left click will be enaugh)", MessageType.Info);
                if ((isPlacingMode() ? usingHoldActivation():
                    lastV.massReplaceDestroyMode == PainterValues.massReplaceDestroyM.massByDistance || lastV.massReplaceDestroyMode == PainterValues.massReplaceDestroyM.massByColliders)
                    ) EditorGUILayout.HelpBox("in this mode, make sure to not leave the scene window while holding mouse 0 button, and then realising. system doesnt register relises when you are not in scene window", MessageType.Warning);
            }

            GUILayout.Space(10);

            if (isPlacingMode())
            {
                PlacerEditorHelper.prefabPaletteField(ref lastV.palette, ref lastV.specificIndex, this, () => PlacerEditorHelper.doARepaint(this));

                GUILayout.Space(10);

                lastV.holdActivation = (PainterValues.holdActivationM)EditorGUILayout.EnumPopup(new GUIContent("Hold Activation"), lastV.holdActivation);

                if (usingHoldActivation())
                {
                    if (helpWindows)
                        EditorGUILayout.HelpBox("when using holdActivation make sure to not leave the scene window while holding mouse 0 button, and then realising. system doesnt register relises when you are not in scene window", MessageType.Warning);

                    lastV.ignoreFirstHoldPlace = EditorGUILayout.Toggle(new GUIContent("Ignore First Hold Place"), lastV.ignoreFirstHoldPlace);

                    lastV.holdActivationDistance = EditorGUILayout.FloatField(new GUIContent("Hold Activation Disatnce", ""), lastV.holdActivationDistance);
                    if (lastV.holdActivationDistance < 0.1f) lastV.holdActivationDistance = 0.1f;
                }

                GUILayout.Space(10);

                lastV.planeProjection = EditorGUILayout.Toggle("Plane Projection", lastV.planeProjection);
                if (lastV.planeProjection)
                {
                    Vector3 norm = EditorGUILayout.Vector3Field("Projected Plane Normal", lastV.projectedPlane.normal);
                    if (norm == Vector3.zero) norm = Vector3.up;
                    float d = EditorGUILayout.FloatField("Plane Distance", lastV.projectedPlane.distance);
                    lastV.projectedPlane = new Plane();
                    lastV.projectedPlane.normal = norm;
                    lastV.projectedPlane.distance = d;
                    if (helpWindows) EditorGUILayout.HelpBox("you can click the scroll wheel to automatically set up the plane. the painted normal will be the plane normal so if your palette doesnt support teh plane angle you will not be able to paint. ", MessageType.Info);
                }

                GUILayout.Space(10);

                drawGridSettings(ref lastV);

                GUILayout.Space(10);

                YRotationOffset = EditorGUILayout.FloatField(new GUIContent("Y Rotation Offset", ""), YRotationOffset);
                lastV.rotateByDegrees = EditorGUILayout.FloatField(new GUIContent("rotateByDegrees", ""), lastV.rotateByDegrees);
                if (helpWindows)
                    EditorGUILayout.HelpBox("click R or T when painting to change the rotation offset by a specified number of degrees. this value doesnt support undo", MessageType.Info);



                if (lastV.paintMode == PainterValues.paintM.scatter)
                {
                    GUILayout.Space(10);

                    lastV.brushSize = EditorGUILayout.FloatField("Brush Size", lastV.brushSize);
                    lastV.scatterCount = EditorGUILayout.IntField(new GUIContent("Scatter Count", ""), lastV.scatterCount);
                    lastV.scatterAvoidenceDistance = EditorGUILayout.FloatField(new GUIContent("Scatter Avoidiance Distance", ""), lastV.scatterAvoidenceDistance);
                }

                GUILayout.Space(20);

                if (!enabledPaint) lastV.spawnPreview = EditorGUILayout.Toggle(new GUIContent("Spawn Preview", ""), lastV.spawnPreview);
            }
            else
            {
                if (lastV.paintMode == PainterValues.paintM.replace)
                {
                    PlacerEditorHelper.prefabPaletteField(ref lastV.palette, ref lastV.specificIndex, this, () => PlacerEditorHelper.doARepaint(this));

                    GUILayout.Space(10);
                }

                lastV.massReplaceDestroyMode = (PainterValues.massReplaceDestroyM)EditorGUILayout.EnumPopup(new GUIContent("Mass " + lastV.paintMode + " Mode"), lastV.massReplaceDestroyMode);

                if (lastV.massReplaceDestroyMode == PainterValues.massReplaceDestroyM.massByDistance || lastV.massReplaceDestroyMode == PainterValues.massReplaceDestroyM.massByColliders)
                {
                    lastV.brushSize = EditorGUILayout.FloatField("Brush Size", lastV.brushSize);
                    lastV.findCount = EditorGUILayout.IntField("Find Count", lastV.findCount);
                }

            }

            EditorGUILayout.EndScrollView();

            Color oldBC = GUI.color;
            GUI.color = enabledPaint ? Color.yellow : deafults.WorkColor;
            if (lastV.palette != null)
            {
                GUILayout.Space(10);
                if (enabledPaint)
                {
                    if (GUILayout.Button("--- Disable ---")) Disable();
                }
                else if (GUILayout.Button("--- Enable ---")) Enable();
            }
            else EditorGUILayout.HelpBox("select a prefab palette to continue", MessageType.Error);
            GUI.color = oldBC;

            PlacerEditorHelper.childRemoveField(transform);

            PlacerDeafultsData.pValues v = new PlacerDeafultsData.pValues() { name = valuesName, values = lastV };
            PlacerEditorHelper.deafultPresetsField(ref deafults.painterVaues, ref v, ref valuesName, ref deafultsScrollPos);
            lastV = v.values;

            GUI.color = old;

            if (!pValues.Equals(lastV))
            {
                Undo.RegisterCompleteObjectUndo(this, "painter values change");
                pValues = lastV;
            }
        }

        private void drawGridSettings(ref PainterValues use)
        {
            GUILayout.BeginHorizontal();
            use.placeGridSize = EditorGUILayout.FloatField(new GUIContent("Place Grid Size", ""), use.placeGridSize);
            if (use.placeGridSize == 0) GUILayout.Label("disabled");
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Auto Find Place Grid Size"))
            {
                float smallestOverallAxis = float.PositiveInfinity;
                foreach (GameObject g in use.palette.prefabs.Select(t => t.prefab))
                {
                    List<MeshFilter> mf = new List<MeshFilter>();
                    mf.AddRange(g.GetComponents<MeshFilter>());
                    mf.AddRange(g.GetComponentsInChildren<MeshFilter>());
                    Bounds combination = new Bounds();
                    for (int i = 0; i < mf.Count; i++)
                    {
                        Bounds b2 = mf[i].sharedMesh.bounds;
                        Vector3 mltp = mf[i].transform.lossyScale;

                        b2.extents = new Vector3(b2.extents.x * mltp.x, b2.extents.y * mltp.y, b2.extents.z * mltp.z);
                        Quaternion rot = Quaternion.Inverse(mf[i].transform.rotation);
                        b2.extents = rot * b2.extents;
                        b2.center = rot * b2.center;
                        combination.Encapsulate(b2);
                    }

                    float biggestAxes = Mathf.Max(combination.size.x, combination.size.z);
                    if (smallestOverallAxis > biggestAxes) smallestOverallAxis = biggestAxes;
                }

                if (smallestOverallAxis != float.PositiveInfinity)
                {
                    Undo.RegisterCompleteObjectUndo(this, "auto find grid size");
                    use.placeGridSize = smallestOverallAxis * (use.palette.minMaxScale.y <= 0 ? 1 : use.palette.minMaxScale.y);
                }
                else Debug.Log("couldnt auto find grid size");
            }


        }

        #endregion

        #region scene drawing

        public void init()
        {
            deafults = PlacerDeafultsData.getDataInProject();
            if (deafults.painterVaues.Count > 0) pValues = deafults.painterVaues[0].values;
            else pValues = new PainterValues(deafults.PrefabPalette);
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            init();
        }
        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            Disable();
        }

        private void OnSceneGUI(SceneView view)
        {

            if (enabledPaint)
            {

                Color c;
                if (paintReturned) c = Color.red;
                else
                {
                    c = toBigNormal ?
                    Color.red
                    :
                    Color.green;
                }
                c.a = 0.3f;
                Handles.color = c;


                float range = 1;

                switch (pValues.paintMode)
                {
                    case PainterValues.paintM.scatter:
                        range = pValues.brushSize;
                        break;
                    case PainterValues.paintM.exact:
                        range = 1;
                        break;
                    case PainterValues.paintM.remove:
                        range = pValues.massReplaceDestroyMode == PainterValues.massReplaceDestroyM.exact ? 1 : pValues.brushSize;
                        break;
                    case PainterValues.paintM.replace:
                        range = pValues.massReplaceDestroyMode == PainterValues.massReplaceDestroyM.exact ? 1 : pValues.brushSize;
                        break;
                }

                Handles.DrawSolidDisc(paintPos, paintNormal, range);

                float randY = pValues.palette.randRotAdd.y;

                if (!paintReturned)
                {
                    if (pValues.planeProjection)
                    {
                        if (planeIsUp())
                            Handles.Label(paintPos + Vector3.down * 1.5f, "plane height: " + pValues.projectedPlane.distance.ToString("0.##"), GUI.skin.textArea);

                        Color old = Handles.color;
                        Color n = Color.cyan;
                        n.a = 0.05f;
                        Handles.color = n;
                        Handles.DrawSolidDisc(paintPos, pValues.projectedPlane.normal, 10);
                        Handles.color = old;
                    }

                }

                if (randY < 180)
                {
                    c = Color.blue;
                    c.a = 0.25f;
                    Handles.color = c;
                    Quaternion rotOf = Quaternion.AngleAxis(YRotationOffset, Vector3.up);
                    Handles.DrawSolidArc(paintPos, paintNormal, rotOf * Quaternion.AngleAxis(randY, Vector3.up) * Vector3.forward, Mathf.Clamp(randY * 2, 5, 360), 2);

                    if (randY <= 45)
                    {
                        c = Color.red;
                        c.a = 0.25f;
                        Handles.color = c;

                        Handles.DrawSolidArc(paintPos, paintNormal, rotOf * Quaternion.AngleAxis(randY + 90, Vector3.up) * Vector3.forward, Mathf.Clamp(randY * 2, 5, 360), 2);
                    }
                }

                if (brushNote != string.Empty) Handles.Label(paintPos + Vector3.down * 0.5f, brushNote, GUI.skin.textArea);
            }
        }

        #endregion


        #region updates

        [ContextMenu("enable")]
        public void Enable()
        {
            if (enabledPaint)
            {
                if (deafults.debugInfoMessages) Debug.Log("already enabled");
                return;
            }

            if (!Application.isEditor)
            {
                Destroy(this);
                return;
            }

            if (deafults.debugInfoMessages) Debug.Log("add paint feature to scene");

            isPlacing = false;
            enabledPaint = true;
            SceneView.beforeSceneGui += beforeScene;

            if (pValues.spawnPreview) previewAsyncUpdates();

        }

        [ContextMenu("disable")]
        public void Disable()
        {
            if (!enabledPaint)
            {
                if (deafults.debugInfoMessages) Debug.Log("already disabled");
                return;
            }

            if (!Application.isEditor)
            {
                Destroy(this);
                return;
            }

            if (deafults.debugInfoMessages) Debug.Log("remove paint feature from scene");

            enabledPaint = false;
            SceneView.beforeSceneGui -= beforeScene;

            removePreview();
        }

        Ray ray = new Ray();
        Vector3 lastpn;
        void beforeScene(SceneView scene)
        {
            if (transform == null)
            {
                Disable();
                return;
            }



            Event e = Event.current;

            if (e.isMouse)
            {
                brushNote = string.Empty;
                LayerMask groundLayer = getGroundLayer();
                LayerMask avoidedLayer = getAvoidedLayer();


                Camera cam = scene.camera;

                Vector3 mousePos = e.mousePosition;
                float ppp = EditorGUIUtility.pixelsPerPoint;
                mousePos.y = cam.pixelHeight - mousePos.y * ppp;
                mousePos.x *= ppp;
                ray = cam.ScreenPointToRay(mousePos);

                bool rayHit;
                RaycastHit info;
                if (usingPlaneProjection())
                {
                    rayHit = planeProjectionRayCast(ray, out info);
                }
                else rayHit = Physics.Raycast(ray, out info, float.PositiveInfinity, groundLayer);

                if (!rayHit)
                {
                    paintReturned = true;
                    paintPos = ray.origin + ray.direction * 16;
                    paintNormal = Vector3.up;
                }
                else
                {
                    paintReturned = !setPlacePos(info, groundLayer, avoidedLayer); // main paint pos set is here
                }

                bool obstacleDetected = false;

                if (!paintReturned && isPlacingMode())
                {
                    Vector3 castPoint = paintPos + paintNormal * 5;
                    paintReturned = obstacleDetected = Physics.Raycast(castPoint, -paintNormal, Vector3.Distance(castPoint, paintPos) - 0.001f, avoidedLayer);
                }


                updatePreview(false);


                if (isPlacingMode())
                {
                    toBigNormal = Vector3.Angle(paintNormal, Vector3.up) > pValues.palette.maxNormal;
                    GUI.changed = true;
                }
                else toBigNormal = false;

                //brushNote = string.Empty; done at the beggining
                if (!rayHit)
                {
                    if (usingPlaneProjection()) brushNote += "not pointing onto the specified plane (" + (planeIsUp() ? "y = " + pValues.projectedPlane.distance : "normal = " + pValues.projectedPlane.normal) + ")\n";
                    else brushNote += brushNoteNoGround;
                }
                else if (obstacleDetected) brushNote += brushNoteObstacle;
                if (toBigNormal) brushNote += " too big surface normal angle\n";

                if (e.button == 0)
                {

                    if (e.type == EventType.MouseUp)
                    {
                        lastSpawn = null;
                        isPlacing = false;
                        e.Use();
                    }
                    else if (((e.type == EventType.MouseDown) || continousPlacing()))
                    {
                        isPlacing = true;
                        e.Use();

                        if (paintReturned || toBigNormal) return;

                        if (usingHoldActivation())
                        {
                            bool hasLastSpawn = lastSpawn != null;

                            if (!hasLastSpawn)
                                lastSpawn = paintPos;

                            if (hasLastSpawn)
                            {
                                if (Vector3.Distance(lastSpawn.Value, paintPos) < pValues.holdActivationDistance) return;
                            }
                            else if (pValues.ignoreFirstHoldPlace) return;
                        }


                        switch (pValues.paintMode)
                        {
                            case PainterValues.paintM.scatter:
                                scatter(groundLayer, avoidedLayer);
                                break;
                            case PainterValues.paintM.exact:
                                spawnObj(paintPos, paintNormal);

                                break;
                            case PainterValues.paintM.remove:
                                editObj(false, info.transform);
                                break;
                            case PainterValues.paintM.replace:
                                editObj(true, info.transform);
                                break;
                        }

                        if (isPlacingMode())
                            lastSpawn = paintPos;
                    }
                }
                else if(e.button == 2)
                {
                    if (Physics.Raycast(ray, out RaycastHit clearInfo, float.PositiveInfinity))
                    {
                        pValues.projectedPlane = new Plane(clearInfo.normal, clearInfo.point);
                        pValues.projectedPlane.distance *= -1;
                        PlacerEditorHelper.doARepaint(this);
                    }
                }

                GUI.changed = true;
            }
            else if (usingPlaneProjection() && isPlacingMode() && e.type == EventType.ScrollWheel)
            {
                
                float ammount = -e.delta.y;
                if (ammount != 0)
                {
                    e.Use();
                    if (ammount > 0) ammount = usingGrid() ? pValues.placeGridSize / 2 : 0.5f;
                    else ammount = usingGrid() ? -pValues.placeGridSize / 2 : -0.5f;

                    pValues.projectedPlane.distance += ammount;
                    if (planeProjectionRayCast(ray, out RaycastHit info))
                    {
                        paintPos = info.point;
                    }
                    else paintPos += pValues.projectedPlane.normal.normalized * ammount;
                    updatePreview(false);
                    PlacerEditorHelper.doARepaint(this);
                }
            }
            else if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Alpha1)
                {
                    pValues.paintMode = PainterValues.paintM.scatter;
                    e.Use();
                    PlacerEditorHelper.doARepaint(this);
                }
                else if (e.keyCode == KeyCode.Alpha2)
                {
                    pValues.paintMode = PainterValues.paintM.exact;
                    e.Use();
                    PlacerEditorHelper.doARepaint(this);
                }
                else if (e.keyCode == KeyCode.Alpha3)
                {
                    pValues.paintMode = PainterValues.paintM.remove;
                    e.Use();
                    PlacerEditorHelper.doARepaint(this);
                }
                else if (e.keyCode == KeyCode.Alpha4)
                {
                    pValues.paintMode = PainterValues.paintM.replace;
                    e.Use();
                    PlacerEditorHelper.doARepaint(this);
                }
            }
            else if (e.keyCode == KeyCode.R || e.keyCode == KeyCode.T)
            {
                YRotationOffset += pValues.rotateByDegrees * (e.keyCode == KeyCode.T ? 1 : -1);
                if (YRotationOffset >= 360) YRotationOffset -= 360; //to keep it small value
                if (YRotationOffset <= -360) YRotationOffset += 360; //to keep it small value

                updatePreview(false);
                e.Use();
                PlacerEditorHelper.doARepaint(this);
            }
        }


        private bool planeProjectionRayCast(Ray ray, out RaycastHit info)
        {
            Plane projection = pValues.projectedPlane;
            projection.distance *= -1;
            bool planeRayHit = projection.Raycast(ray, out float d);

            Vector3 rayPoint = ray.GetPoint(d);

            if (Physics.Raycast(ray, out RaycastHit info2, d)) //collect transform info if possible
            {
                //Debug.Log(info2.transform.name);
                //if (!Physics.Raycast(ray, Vector3.Distance(info2.point, ray.origin) - 0.001f, pValues.palette.avoidedLayer)) info = info2;                
                //else info = new RaycastHit();
                info = info2;
            }
            else info = new RaycastHit();

            info.point = rayPoint;
            info.normal = Vector3.up;

            return planeRayHit;
        }

        #endregion

        #region info

        public bool usingGrid()
        {
            return pValues.placeGridSize != 0;
        }

        public bool usingPlaneProjection()
        {
            return pValues.planeProjection;
        }

        public bool usingHoldActivation()
        {
            return pValues.holdActivation == PainterValues.holdActivationM.enabledSimple || pValues.holdActivation == PainterValues.holdActivationM.enabledRotateWithDragDir;
        }

        public bool planeIsUp()
        {
            return pValues.projectedPlane.normal == Vector3.up;
        }

        public bool isPlacingMode()
        {
            return (pValues.paintMode == PainterValues.paintM.scatter || pValues.paintMode == PainterValues.paintM.exact);
        }

        private bool continousPlacing()
        {
            return isPlacing && (usingHoldActivation() || !isPlacingMode());
        }

        private bool continousPlacing(out bool hold)
        {
            hold = usingHoldActivation();
            return isPlacing && (hold || !isPlacingMode());
        }

        #endregion


        #region preview

        private void updatePreview(bool remake)
        {
            if (!pValues.spawnPreview || !enabledPaint) return;

            if (paintReturned)
            {
                removePreview();
                return;
            }

            if (isPlacingMode())
            {
                preview = pValues.palette.updatePreviewWithSettings(preview, paintPos, paintNormal, YRotationOffset, pValues.specificIndex, remake, previewObjectData, out prefabPalette.objectSettings s);
                previewObjectData = s;
            }
            else removePreview();
        }


        private async void previewAsyncUpdates()
        {
            while (true)
            {
                if (!enabledPaint)
                {
                    if (deafults.debugInfoMessages) Debug.Log("remove preview reMake sequence");
                    return;
                }

                updatePreview(true);

                await System.Threading.Tasks.Task.Delay(750); // remake once per 0.75 seconds
            }
        }

        private void removePreview()
        {
            if (preview != null)
                DestroyImmediate(preview);
        }

        #endregion


        #region spawns

        private void scatter(LayerMask ground, LayerMask avoided)
        {
            Vector3 rayCenter = paintPos + paintNormal * 3;

            Quaternion toRot = Quaternion.FromToRotation(Vector3.up, paintNormal);

            List<Vector3> spawnedPos = new List<Vector3>();

            float sqrAvoidiance = pValues.scatterAvoidenceDistance * pValues.scatterAvoidenceDistance;

            for (int i = 0; i < pValues.scatterCount; i++)
            {
                Vector2 move = Random.insideUnitCircle * pValues.brushSize;

                Vector3 offset = toRot * new Vector3(move.x, 0, move.y);

                Vector3 nCenter = rayCenter + offset;

                Ray ray = new Ray(nCenter, -paintNormal);

                if (usingPlaneProjection())
                {
                    if (planeProjectionRayCast(ray, out RaycastHit info))
                    {
                        bool tooCloase = false;
                        foreach (Vector3 p in spawnedPos)
                            if ((p - nCenter).sqrMagnitude < sqrAvoidiance)
                            {
                                tooCloase = true;
                                break;
                            }

                        if (tooCloase) continue;

                        spawnObj(info.point, info.normal);
                        spawnedPos.Add(nCenter);
                    }
                }
                else if (Physics.Raycast(ray, out RaycastHit info, 9, ground))
                {
                    if (!Physics.Raycast(ray, Vector3.Distance(nCenter, info.point), avoided, QueryTriggerInteraction.Ignore))
                    {
                        if (Vector3.Angle(Vector3.up, info.normal) <= pValues.palette.maxNormal)
                        {
                            bool tooCloase = false;
                            foreach (Vector3 p in spawnedPos)
                                if ((p - nCenter).sqrMagnitude < sqrAvoidiance)
                                {
                                    tooCloase = true;
                                    break;
                                }
                            if (tooCloase) continue;

                            spawnObj(info.point, info.normal);
                            spawnedPos.Add(nCenter);
                        }
                    }
                }
            }
        }
        
        private void spawnObj(Vector3 pos, Vector3 surfaceNormal) 
        {
            float rotMatch = 0;
            if(pValues.holdActivation == PainterValues.holdActivationM.enabledRotateWithDragDir && lastSpawn != null)
            {
                Vector3 dir = pos - lastSpawn.Value;
                Vector2 v1 = new Vector2(dir.x, dir.z);
                Vector2 v2 = new Vector2(0, 1);
                rotMatch = Vector2.Angle(v1, v2) * Mathf.Sign(v1.x * v2.y - v1.y * v2.x);
                //Debug.Log(rotMatch + " " + v1 + " to " + v2 + "   " + lastSpawn.Value);
            }
            //GameObject spawned = 
            pValues.palette.spawn(pos, surfaceNormal, transform, YRotationOffset + rotMatch, pValues.specificIndex);

            //Undo.RegisterCreatedObjectUndo(spawned, "painted object"); 
            //EditorUtility.SetDirty(spawned);
        }

        private void editObj(bool replace, Transform pointedAtTransform)
        {

            if (pValues.massReplaceDestroyMode == PainterValues.massReplaceDestroyM.exact)
            {
                if (pointedAtTransform != null)
                {
                    Transform checkedT = pointedAtTransform;
                    while (checkedT.parent != transform && checkedT.parent != null) checkedT = checkedT.parent;


                    if (checkedT.parent == null) return;
                    else
                    {
                        if (replace) spawnObj(checkedT.position, checkedT.up);
                        DestroyImmediate(checkedT.gameObject);
                    }
                }
            }
            else if (pValues.massReplaceDestroyMode == PainterValues.massReplaceDestroyM.massByDistance)
            {
                float sqrSize = pValues.brushSize * pValues.brushSize;

                int edited = 0;
                int curCheck = 0;

                while (transform.childCount > curCheck && edited <= pValues.findCount)
                {
                    if ((transform.GetChild(curCheck).position - paintPos).sqrMagnitude < sqrSize)
                    {
                        Transform obj = transform.GetChild(curCheck);

                        if (replace) spawnObj(obj.position, obj.up);
                        DestroyImmediate(obj.gameObject);

                        edited++;
                    }

                    curCheck++;
                }
            }
            else //if(pValues.massReplaceDestroyMode == PainterValues.massReplaceDestroyM.massByColliders)
            {
                Collider[] col = new Collider[pValues.findCount];
                int count = Physics.OverlapSphereNonAlloc(paintPos, pValues.brushSize, col);
                //Debug.Log(count);
                for (int i = 0; i < count; i++)
                {
                    if (col[i] == null) continue;

                    Transform checkedT = col[i].transform;
                    while (checkedT.parent != transform && checkedT.parent != null) checkedT = checkedT.parent;
                    //Debug.Log(checkedT + " " + i);

                    if (checkedT.parent == null) continue;
                    else
                    {
                        //Debug.Log("found parent");
                        if (replace) spawnObj(checkedT.position, checkedT.up);
                        DestroyImmediate(checkedT.gameObject);
                    }

                }
            }

        }

        #endregion


        #region get set

        private bool setPlacePos(RaycastHit info, LayerMask ground, LayerMask avoided)
        {
            if (usingGrid() && isPlacingMode())
            {
                Vector3 gridedPos = info.point;
                float halfGrid = pValues.placeGridSize / 2;
                if (usingPlaneProjection())
                {
                    if (planeIsUp())
                    {
                        doStufToPos(ref gridedPos);
                        paintNormal = Vector3.up;
                    }
                    else
                    {
                        Quaternion r = Quaternion.FromToRotation(pValues.projectedPlane.normal, Vector3.up);
                        gridedPos = r * gridedPos;
                        doStufToPos(ref gridedPos);
                        gridedPos = Quaternion.Inverse(r) * gridedPos;
                        paintNormal = pValues.projectedPlane.normal;
                    }
                }
                else
                {
                    doStufToPos(ref gridedPos);
                    if (Physics.Raycast(gridedPos + Vector3.up * 2, Vector3.down, out RaycastHit hit, float.PositiveInfinity, ground))
                    {
                        gridedPos = hit.point;
                        paintNormal = hit.normal;
                    }
                    else
                    {
                        brushNote += brushNoteNoGround;
                        return false;
                    }
                }

                paintPos = gridedPos;
                
                if(Physics.OverlapSphereNonAlloc(paintPos, 0.1f, new Collider[1], avoided) > 0)
                {
                    brushNote += brushNoteObstacle;
                    return false;
                }

                void doStufToPos(ref Vector3 pos)
                {
                    float XRest = (pos.x % pValues.placeGridSize);
                    if (XRest > halfGrid) pos.x += pValues.placeGridSize - XRest;
                    else pos.x -= XRest;

                    float ZRest = (pos.z % pValues.placeGridSize);
                    if (ZRest > halfGrid) pos.z += pValues.placeGridSize - ZRest;
                    else pos.z -= ZRest;
                }
            }
            else
            {
                paintPos = info.point;
                paintNormal = info.normal;
            }

            return true;
        }

        private LayerMask getAvoidedLayer()
        {
            if (isPlacingMode()) return pValues.palette.avoidedLayer;

            return 0;
        }

        private LayerMask getGroundLayer()
        {
            if (isPlacingMode()) return pValues.palette.groundLayer;

            return ~0;
        }

        #endregion

        #region palette user

        public void setPalette(prefabPalette palette)
        {
            pValues.palette = palette;
            removePreview();
            pValues.specificIndex = Mathf.Clamp(pValues.specificIndex, -1, palette.prefabs.Length - 1);
        }

        public prefabPalette getPalette()
        {
            return pValues.palette;
        }

        public void setSpecificIndex(int index)
        {
            pValues.specificIndex = index;
            if (pValues.spawnPreview) updatePreview(true);
        }
        public int getSpecificIndex()
        {
            return pValues.specificIndex;
        }

        #endregion
    }
}
