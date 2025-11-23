///===================================================================
/// 预制体/网格摆放工具
/// 作者:HCFlower
/// 发布日期:2025.11.23 15:00
///===================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// 预制体摆放工具
/// </summary>
public class PrefabPlacerTool : EditorWindow
{
    [MenuItem("Tools/预制体放置工具")]
    public static void ShowWindow()
    {
        PrefabPlacerTool window = GetWindow<PrefabPlacerTool>("预制体放置工具");
        window.minSize = new Vector2(400, 610);
    }

    private enum PlacementMode
    {
        SinglePlacement = 0,            // 单个放置
        StraightLineArrangement = 1,    // 直线排列
        RangePlacement = 2              // 圆形范围放置
    }

    private enum DistributionType
    {
        UniformDistribution = 0,        // 均匀分布
        RandomDistribution = 1          // 随机分布
    }

    // 对象项数据结构
    [System.Serializable]
    public class PlaceableObject
    {
        public GameObject prefab;
        public Mesh mesh;
        public Material material;
        public float weight = 1f; // 权重，用于随机选择
        public bool isEnabled = false; // 是否启用

        public bool IsValid()
        {
            return prefab != null || mesh != null;
        }

        public string GetName()
        {
            if (prefab != null) return prefab.name;
            if (mesh != null) return mesh.name + (material != null ? $" ({material.name})" : " (无材质)");
            return "空对象";
        }

        public Texture2D GetPreviewTexture()
        {
            if (prefab != null)
            {
                return AssetPreview.GetAssetPreview(prefab);
            }
            else if (mesh != null)
            {
                return AssetPreview.GetAssetPreview(mesh);
            }
            return null;
        }
    }

    private PlacementMode placementMode = PlacementMode.SinglePlacement;
    private DistributionType distributionType = DistributionType.UniformDistribution;

    private List<PlaceableObject> placeableObjects = new List<PlaceableObject>();
    private int selectedObjectIndex = -1;

    private int placementCount = 5;
    private float placementRadius = 5f;
    private float lineSpacing = 2f;
    private float lineRandomOffset = 0.5f;

    private bool randomRotation = false;
    private bool randomRotationX = false;
    private bool randomRotationY = true;
    private bool randomRotationZ = false;
    private bool randomScale = false;
    private float minScale = 1.0f;
    private float maxScale = 1.25f;
    private bool alignToSurface = true;
    private bool alignToSurfaceNormal = true;
    private LayerMask surfaceLayer = 1;
    private bool showPreview = true;

    // 放置状态
    private bool isInPlacementMode = false;
    private bool isInCleanupMode = false;
    private float placementRotation = 0f;
    private Vector3 lastMousePosition;
    private List<GameObject> placedObjects = new List<GameObject>();
    private Vector2 scrollPosition;
    private Vector2 objectListScrollPosition;

    // 随机种子
    private int randomSeed = 0;
    private System.Random random;
    private System.Random rotationRandom;
    private System.Random scaleRandom;

    // GUI折叠状态
    private bool showAdvancedOptions = true;
    private bool showRandomSettings = false;
    private bool showDirectionSettings = true;
    private bool showObjectList = true;
    private bool showObjectSettings = false;

    // 网格布局相关
    private const float GRID_ITEM_SIZE = 80f;
    private const float GRID_SPACING = 5f;
    private const float GRID_LIST_HEIGHT = 200f;

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        DrawHeader();
        DrawObjectList();
        CheckAutoCollapse();
        DrawObjectSettings();
        DrawSettings();
        DrawPlacementControls();
        EditorGUILayout.EndScrollView();
    }

    // 检查窗口高度并自动折叠部分区域
    private void CheckAutoCollapse()
    {
        float minHeightForExpand = 600f;

        if (position.height < minHeightForExpand)
        {
            if (showAdvancedOptions || showRandomSettings || showDirectionSettings)
            {
                showAdvancedOptions = false;
                showRandomSettings = false;
                showDirectionSettings = false;
            }
        }
    }

    private void DrawHeader()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        {
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
            titleStyle.fontSize = 16;
            titleStyle.alignment = TextAnchor.MiddleCenter;
            EditorGUILayout.LabelField("预制体摆放工具", titleStyle, GUILayout.Height(32));
            EditorGUILayout.HelpBox("在场景视图中点击放置\n" +
                                  "Ctrl+滚轮:旋转角度 | Shift+滚轮:随机位置变化\n" +
                                  "Alt+滚轮:调整数量 | Ctrl+Alt+滚轮:范围/间隔缩放\n" +
                                  "ESC:退出放置模式", MessageType.Info);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(3);
    }

    // 绘制对象列表（网格布局）
    private void DrawObjectList()
    {
        EditorGUILayout.BeginVertical("helpbox");
        {
            EditorGUILayout.BeginHorizontal();
            GUIStyle leftButtonStyle = new GUIStyle(GUI.skin.button);
            leftButtonStyle.alignment = TextAnchor.MiddleLeft;
            leftButtonStyle.fontSize = 12;
            leftButtonStyle.fontStyle = FontStyle.Bold;

            string listBtnText = showObjectList ? "▼ 预制体/网格库" : "▶ 预制体/网格库";
            if (GUILayout.Button(listBtnText, leftButtonStyle, GUILayout.Height(22), GUILayout.ExpandWidth(true)))
            {
                showObjectList = !showObjectList;
            }
            EditorGUILayout.EndHorizontal();

            if (showObjectList)
            {
                EditorGUILayout.Space(5);

                // 操作按钮行
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("拖拽预制体或网格到下方网格", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("添加空项", GUILayout.Width(80)))
                {
                    placeableObjects.Add(new PlaceableObject());
                }
                if (GUILayout.Button("启用所有", GUILayout.Width(80)))
                {
                    foreach (var obj in placeableObjects)
                    {
                        obj.isEnabled = true;
                    }
                }
                if (GUILayout.Button("禁用所有", GUILayout.Width(80)))
                {
                    foreach (var obj in placeableObjects)
                    {
                        obj.isEnabled = false;
                    }
                }
                if (GUILayout.Button("清空列表", GUILayout.Width(80)))
                {
                    if (EditorUtility.DisplayDialog("确认清空", "确定要清空整个列表吗？", "确定", "取消"))
                    {
                        placeableObjects.Clear();
                        selectedObjectIndex = -1;
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(3);

                // 网格布局区域
                Rect gridArea = GUILayoutUtility.GetRect(0, GRID_LIST_HEIGHT, GUILayout.ExpandWidth(true));
                GUI.Box(gridArea, "", EditorStyles.helpBox);

                HandleDragAndDrop(gridArea);

                objectListScrollPosition = GUI.BeginScrollView(
                    new Rect(gridArea.x + 5, gridArea.y + 5, gridArea.width - 10, gridArea.height - 10),
                    objectListScrollPosition,
                    new Rect(0, 0, CalculateGridContentWidth(), CalculateGridContentHeight())
                );

                DrawGridLayout();

                GUI.EndScrollView();

                // 统计信息
                int enabledCount = GetEnabledObjectsCount();
                string statusText = "";
                if (placeableObjects.Count > 0)
                {
                    statusText = $"总数: {placeableObjects.Count}, 已启用: {enabledCount}";
                    if (selectedObjectIndex >= 0 && selectedObjectIndex < placeableObjects.Count)
                    {
                        statusText += $", 已选中: {placeableObjects[selectedObjectIndex].GetName()}";
                    }
                }
                else
                {
                    statusText = "将预制体或网格拖拽到上方网格中";
                }

                EditorGUILayout.LabelField(statusText, EditorStyles.miniLabel);
            }
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(3);
    }

    // 计算网格内容宽度
    private float CalculateGridContentWidth()
    {
        float availableWidth = position.width - 40f;
        int itemsPerRow = Mathf.FloorToInt(availableWidth / (GRID_ITEM_SIZE + GRID_SPACING));
        if (itemsPerRow < 1) itemsPerRow = 1;
        return itemsPerRow * (GRID_ITEM_SIZE + GRID_SPACING);
    }

    // 计算网格内容高度
    private float CalculateGridContentHeight()
    {
        float availableWidth = position.width - 40f;
        int itemsPerRow = Mathf.FloorToInt(availableWidth / (GRID_ITEM_SIZE + GRID_SPACING));
        if (itemsPerRow < 1) itemsPerRow = 1;

        int rows = Mathf.CeilToInt((float)placeableObjects.Count / itemsPerRow);
        return Mathf.Max(rows * (GRID_ITEM_SIZE + GRID_SPACING), GRID_LIST_HEIGHT - 20f);
    }

    // 绘制网格布局
    private void DrawGridLayout()
    {
        if (placeableObjects.Count == 0)
        {
            GUIStyle centeredStyle = new GUIStyle(EditorStyles.label);
            centeredStyle.alignment = TextAnchor.MiddleCenter;
            centeredStyle.normal.textColor = Color.gray;
            float gridWidth = Mathf.Min(CalculateGridContentWidth(), position.width - 40f);
            float gridX = (position.width - gridWidth) / 2f;
            Rect emptyRect = new Rect(gridX, GRID_LIST_HEIGHT * 0.4f, gridWidth, 30);
            GUI.Label(emptyRect, "拖拽预制体或网格到此处", centeredStyle);
            return;
        }

        float availableWidth = position.width - 40f;
        int itemsPerRow = Mathf.FloorToInt(availableWidth / (GRID_ITEM_SIZE + GRID_SPACING));
        if (itemsPerRow < 1) itemsPerRow = 1;

        for (int i = 0; i < placeableObjects.Count; i++)
        {
            int row = i / itemsPerRow;
            int col = i % itemsPerRow;

            float x = col * (GRID_ITEM_SIZE + GRID_SPACING);
            float y = row * (GRID_ITEM_SIZE + GRID_SPACING);

            Rect itemRect = new Rect(x, y, GRID_ITEM_SIZE, GRID_ITEM_SIZE);
            DrawGridItem(itemRect, i);
        }
    }

    // 绘制网格单个项目
    private void DrawGridItem(Rect rect, int index)
    {
        PlaceableObject obj = placeableObjects[index];

        GUI.Box(rect, "", EditorStyles.helpBox);

        Rect previewRect = new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4);

        Texture2D preview = obj.GetPreviewTexture();
        if (preview != null)
        {
            GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit, true);
        }
        else
        {
            GUIStyle iconStyle = new GUIStyle(EditorStyles.label);
            iconStyle.alignment = TextAnchor.MiddleCenter;
            iconStyle.fontSize = 24;
            GUIContent iconContent = obj.prefab != null ? EditorGUIUtility.IconContent("Prefab Icon") : EditorGUIUtility.IconContent("MeshRenderer Icon");
            GUI.Label(previewRect, iconContent, iconStyle);
        }

        // 添加启用/禁用复选框在左上角
        float checkboxSize = 16f;
        Rect checkboxRect = new Rect(rect.x + 2, rect.y + 2, checkboxSize, checkboxSize);

        // 直接绘制复选框（不加Box背景）
        bool newEnabled = GUI.Toggle(checkboxRect, obj.isEnabled, "");
        if (newEnabled != obj.isEnabled)
        {
            obj.isEnabled = newEnabled;
            // 保持选中状态，用户仍可在设置面板中查看和修改
        }

        if (!obj.isEnabled)
        {
            Color maskColor = new Color(0.1f, 0.1f, 0.1f, 0.5f);
            EditorGUI.DrawRect(previewRect, maskColor);
        }

        if (index == selectedObjectIndex)
        {
            Color borderColor = new Color(1f, 0.7f, 0.2f, 1f);
            float borderWidth = 3f;
            Rect borderRect = new Rect(previewRect.x - borderWidth / 2, previewRect.y - borderWidth / 2, previewRect.width + borderWidth, previewRect.height + borderWidth);
            Handles.BeginGUI();
            Handles.DrawSolidRectangleWithOutline(borderRect, Color.clear, borderColor);
            Handles.EndGUI();
        }

        if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
        {
            // 检查是否点击在复选框区域，如果是则不处理选中逻辑
            if (!checkboxRect.Contains(Event.current.mousePosition))
            {
                selectedObjectIndex = index;
                showObjectSettings = true;
                Event.current.Use();
            }
        }
    }

    // 绘制选中对象的设置
    private void DrawObjectSettings()
    {
        if (selectedObjectIndex < 0 || selectedObjectIndex >= placeableObjects.Count)
        {
            return;
        }

        PlaceableObject selectedObj = placeableObjects[selectedObjectIndex];

        EditorGUILayout.BeginVertical("helpbox");
        {
            EditorGUILayout.BeginHorizontal();
            GUIStyle leftButtonStyle = new GUIStyle(GUI.skin.button);
            leftButtonStyle.alignment = TextAnchor.MiddleLeft;
            leftButtonStyle.fontSize = 12;
            leftButtonStyle.fontStyle = FontStyle.Bold;

            string settingsText = showObjectSettings ? $"▼ 对象设置 - {selectedObj.GetName()}" : $"▶ 对象设置 - {selectedObj.GetName()}";
            if (GUILayout.Button(settingsText, leftButtonStyle, GUILayout.Height(22), GUILayout.ExpandWidth(true)))
            {
                showObjectSettings = !showObjectSettings;
            }
            EditorGUILayout.EndHorizontal();

            if (showObjectSettings)
            {
                EditorGUILayout.Space(5);

                if (selectedObj.prefab != null)
                {
                    GameObject newPrefab = (GameObject)EditorGUILayout.ObjectField("预制体", selectedObj.prefab, typeof(GameObject), false);
                    if (newPrefab != selectedObj.prefab)
                    {
                        if (newPrefab == null || PrefabUtility.IsPartOfPrefabAsset(newPrefab))
                        {
                            selectedObj.prefab = newPrefab;
                            selectedObj.mesh = null;
                            selectedObj.material = null;
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("错误", "请选择预制体资源", "确定");
                        }
                    }
                }
                else
                {
                    selectedObj.mesh = (Mesh)EditorGUILayout.ObjectField("网格", selectedObj.mesh, typeof(Mesh), false);
                    selectedObj.material = (Material)EditorGUILayout.ObjectField("材质", selectedObj.material, typeof(Material), false);

                    if (selectedObj.mesh != null && GUILayout.Button("转换为预制体模式"))
                    {
                        selectedObj.mesh = null;
                        selectedObj.material = null;
                    }
                }

                EditorGUILayout.Space(3);

                selectedObj.isEnabled = EditorGUILayout.ToggleLeft("启用此对象", selectedObj.isEnabled);

                EditorGUILayout.LabelField("随机权重", EditorStyles.boldLabel);
                selectedObj.weight = EditorGUILayout.Slider("权重", selectedObj.weight, 0.1f, 10f);
                EditorGUILayout.HelpBox($"权重越高，被随机选中的概率越大\n当前权重: {selectedObj.weight:F1}", MessageType.Info);

                EditorGUILayout.Space(3);

                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("复制对象"))
                    {
                        PlaceableObject copy = new PlaceableObject
                        {
                            prefab = selectedObj.prefab,
                            mesh = selectedObj.mesh,
                            material = selectedObj.material,
                            weight = selectedObj.weight,
                            isEnabled = selectedObj.isEnabled
                        };
                        placeableObjects.Insert(selectedObjectIndex + 1, copy);
                    }

                    if (GUILayout.Button("删除对象"))
                    {
                        if (EditorUtility.DisplayDialog("确认删除", $"确定要删除 '{selectedObj.GetName()}' 吗？", "确定", "取消"))
                        {
                            placeableObjects.RemoveAt(selectedObjectIndex);
                            selectedObjectIndex = -1;
                            showObjectSettings = false;
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(3);
    }

    // 处理拖拽操作
    private void HandleDragAndDrop(Rect dropArea)
    {
        Event evt = Event.current;

        switch (evt.type)
        {
            case EventType.DragUpdated:
            case EventType.DragPerform:
                if (!dropArea.Contains(evt.mousePosition))
                    return;

                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();

                    foreach (Object draggedObject in DragAndDrop.objectReferences)
                    {
                        // 文件夹处理
                        if (draggedObject is DefaultAsset)
                        {
                            string folderPath = AssetDatabase.GetAssetPath(draggedObject);
                            string[] guids = AssetDatabase.FindAssets("t:GameObject t:Mesh", new[] { folderPath });
                            foreach (string guid in guids)
                            {
                                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                                Object assetObj = AssetDatabase.LoadAssetAtPath<Object>(assetPath);

                                // 去重判断
                                bool exists = false;
                                if (assetObj is GameObject go)
                                {
                                    exists = placeableObjects.Exists(o => o.prefab == go);
                                }
                                else if (assetObj is Mesh mesh)
                                {
                                    exists = placeableObjects.Exists(o => o.mesh == mesh);
                                }
                                if (exists) continue;

                                PlaceableObject newObj = new PlaceableObject();

                                if (assetObj is GameObject go2)
                                {
                                    if (PrefabUtility.IsPartOfPrefabAsset(go2))
                                    {
                                        newObj.prefab = go2;
                                    }
                                    else
                                    {
                                        Debug.LogWarning($"对象 {go2.name} 不是预制体资源");
                                        continue;
                                    }
                                }
                                else if (assetObj is Mesh mesh2)
                                {
                                    newObj.mesh = mesh2;
                                }
                                else
                                {
                                    Debug.LogWarning($"不支持的对象类型: {assetObj.GetType()}");
                                    continue;
                                }

                                placeableObjects.Add(newObj);
                            }
                            continue;
                        }

                        // 去重判断
                        bool alreadyExists = false;
                        if (draggedObject is GameObject goObj)
                        {
                            alreadyExists = placeableObjects.Exists(o => o.prefab == goObj);
                        }
                        else if (draggedObject is Mesh meshObj)
                        {
                            alreadyExists = placeableObjects.Exists(o => o.mesh == meshObj);
                        }
                        if (alreadyExists) continue;

                        PlaceableObject obj = new PlaceableObject();

                        if (draggedObject is GameObject goObj2)
                        {
                            if (PrefabUtility.IsPartOfPrefabAsset(goObj2))
                            {
                                obj.prefab = goObj2;
                            }
                            else
                            {
                                Debug.LogWarning($"对象 {goObj2.name} 不是预制体资源");
                                continue;
                            }
                        }
                        else if (draggedObject is Mesh meshObj2)
                        {
                            obj.mesh = meshObj2 as Mesh;
                        }
                        else
                        {
                            Debug.LogWarning($"不支持的对象类型: {draggedObject.GetType()}");
                            continue;
                        }

                        placeableObjects.Add(obj);
                    }
                }
                break;
        }
    }

    private void DrawSettings()
    {
        EditorGUILayout.BeginVertical("helpbox");
        {
            EditorGUILayout.LabelField("基础设置", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            placementMode = (PlacementMode)EditorGUILayout.EnumPopup("放置模式", placementMode);

            EditorGUILayout.Space(3);
            DrawModeSpecificSettings();
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(3);

        DrawAdvancedOptions();

        if (placementMode != PlacementMode.SinglePlacement)
        {
            DrawRandomSettings();
        }

        DrawDirectionSettings();
    }

    private void DrawModeSpecificSettings()
    {
        EditorGUILayout.BeginVertical("helpbox");

        switch (placementMode)
        {
            case PlacementMode.SinglePlacement:
                EditorGUILayout.LabelField("在场景中点击放置单个对象", EditorStyles.miniLabel);
                break;

            case PlacementMode.StraightLineArrangement:
                placementCount = EditorGUILayout.IntSlider("放置数量", placementCount, 2, 50);
                lineSpacing = EditorGUILayout.Slider("间隔距离", lineSpacing, 0.5f, 10f);
                lineRandomOffset = EditorGUILayout.Slider("随机偏移", lineRandomOffset, 0f, 5f);

                if (lineRandomOffset > 0)
                {
                    EditorGUILayout.LabelField("对象将沿直线排列并带有随机偏移", EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.LabelField("对象将沿直线均匀排列", EditorStyles.miniLabel);
                }
                break;

            case PlacementMode.RangePlacement:
                placementCount = EditorGUILayout.IntSlider("放置数量", placementCount, 2, 100);
                placementRadius = EditorGUILayout.Slider("圆形半径", placementRadius, 1f, 50f);
                distributionType = (DistributionType)EditorGUILayout.EnumPopup("分布类型", distributionType);

                if (distributionType == DistributionType.UniformDistribution)
                {
                    EditorGUILayout.LabelField("对象将在圆形范围内均匀分布", EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.LabelField("对象将在圆形范围内随机分布", EditorStyles.miniLabel);
                }
                break;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawAdvancedOptions()
    {
        EditorGUILayout.BeginVertical("helpbox");
        {
            EditorGUILayout.BeginHorizontal();
            GUIStyle leftButtonStyle = new GUIStyle(GUI.skin.button);
            leftButtonStyle.alignment = TextAnchor.MiddleLeft;
            leftButtonStyle.fontSize = 12;
            leftButtonStyle.fontStyle = FontStyle.Bold;

            string advBtnText = showAdvancedOptions ? "▼ 高级选项" : "▶ 高级选项";
            if (GUILayout.Button(advBtnText, leftButtonStyle, GUILayout.Height(22), GUILayout.ExpandWidth(true)))
            {
                showAdvancedOptions = !showAdvancedOptions;
            }
            EditorGUILayout.EndHorizontal();

            if (showAdvancedOptions)
            {
                GUIStyle leftLabel = new GUIStyle(EditorStyles.label);
                leftLabel.alignment = TextAnchor.MiddleLeft;

                randomRotation = EditorGUILayout.ToggleLeft("随机旋转", randomRotation, leftLabel);
                if (randomRotation)
                {
                    EditorGUI.indentLevel++;
                    randomRotationX = EditorGUILayout.ToggleLeft("X轴随机", randomRotationX, leftLabel);
                    randomRotationY = EditorGUILayout.ToggleLeft("Y轴随机", randomRotationY, leftLabel);
                    randomRotationZ = EditorGUILayout.ToggleLeft("Z轴随机", randomRotationZ, leftLabel);
                    EditorGUI.indentLevel--;
                }

                randomScale = EditorGUILayout.ToggleLeft("随机缩放", randomScale, leftLabel);
                if (randomScale)
                {
                    EditorGUI.indentLevel++;
                    minScale = EditorGUILayout.Slider("最小缩放", minScale, 0.1f, 99f);
                    maxScale = EditorGUILayout.Slider("最大缩放", maxScale, 0.1f, 100f);

                    if (minScale > maxScale)
                    {
                        maxScale = minScale;
                    }
                    EditorGUI.indentLevel--;
                }

                alignToSurface = EditorGUILayout.ToggleLeft("贴合表面", alignToSurface, leftLabel);

                // 新增：法线对齐选项
                if (alignToSurface)
                {
                    EditorGUI.indentLevel++;
                    alignToSurfaceNormal = EditorGUILayout.ToggleLeft("对齐法线方向", alignToSurfaceNormal, leftLabel);
                    EditorGUI.indentLevel--;

                    surfaceLayer = LayerMaskField("表面图层", surfaceLayer);
                }

                showPreview = EditorGUILayout.ToggleLeft("显示预览", showPreview, leftLabel);
            }
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawRandomSettings()
    {
        EditorGUILayout.BeginVertical("helpbox");
        {
            EditorGUILayout.BeginHorizontal();
            GUIStyle leftButtonStyle = new GUIStyle(GUI.skin.button);
            leftButtonStyle.alignment = TextAnchor.MiddleLeft;
            leftButtonStyle.fontSize = 12;
            leftButtonStyle.fontStyle = FontStyle.Bold;

            string randomBtnText = showRandomSettings ? "▼ 随机设置" : "▶ 随机设置";
            if (GUILayout.Button(randomBtnText, leftButtonStyle, GUILayout.Height(22), GUILayout.ExpandWidth(true)))
            {
                showRandomSettings = !showRandomSettings;
            }
            EditorGUILayout.EndHorizontal();

            if (showRandomSettings)
            {
                randomSeed = Mathf.Max(0, EditorGUILayout.IntField("随机种子", randomSeed));

                if (GUILayout.Button("重新生成随机数", GUILayout.Height(25)))
                {
                    randomSeed = Random.Range(0, 10000);
                    InitializeRandom(); // 仅影响对象选择与旋转/缩放随机
                }
                EditorGUILayout.HelpBox("相同种子 -> 相同权重随机选择序列", MessageType.None);
            }
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawDirectionSettings()
    {
        EditorGUILayout.BeginVertical("helpbox");
        {
            EditorGUILayout.BeginHorizontal();
            GUIStyle leftButtonStyle = new GUIStyle(GUI.skin.button);
            leftButtonStyle.alignment = TextAnchor.MiddleLeft;
            leftButtonStyle.fontSize = 12;
            leftButtonStyle.fontStyle = FontStyle.Bold;

            string dirBtnText = showDirectionSettings ? "▼ 方向设置" : "▶ 方向设置";
            if (GUILayout.Button(dirBtnText, leftButtonStyle, GUILayout.Height(22), GUILayout.ExpandWidth(true)))
            {
                showDirectionSettings = !showDirectionSettings;
            }
            EditorGUILayout.EndHorizontal();

            if (showDirectionSettings)
            {
                EditorGUILayout.BeginVertical("helpbox");
                {
                    placementRotation = EditorGUILayout.Slider("方向角度", placementRotation, 0f, 360f);
                }
                EditorGUILayout.EndVertical();
            }
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawPlacementControls()
    {
        EditorGUILayout.BeginVertical("helpbox");
        {
            EditorGUILayout.LabelField("放置控制", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            {
                GUI.backgroundColor = isInPlacementMode ? new Color(1f, 0.5f, 0.5f) : new Color(0.5f, 1f, 0.5f);
                if (!isInPlacementMode && !isInCleanupMode)
                {
                    if (GUILayout.Button("开始放置", GUILayout.Height(24)))
                    {
                        StartPlacementMode();
                    }
                }
                else if (isInPlacementMode)
                {
                    if (GUILayout.Button("取消放置", GUILayout.Height(24)))
                    {
                        CancelPlacementMode();
                    }
                }

                GUI.backgroundColor = isInCleanupMode ? new Color(1f, 0.5f, 0.5f) : new Color(1f, 0.8f, 0.3f);
                if (!isInPlacementMode && !isInCleanupMode)
                {
                    if (GUILayout.Button("清理模式", GUILayout.Height(24)))
                    {
                        StartCleanupMode();
                    }
                }
                else if (isInCleanupMode)
                {
                    if (GUILayout.Button("退出清理", GUILayout.Height(24)))
                    {
                        CancelCleanupMode();
                    }
                }

                GUI.backgroundColor = Color.white;

                GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                if (GUILayout.Button("清除记录", GUILayout.Height(24), GUILayout.Width(100)))
                {
                    ClearAllPlacedObjects();
                }
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndHorizontal();

            if (isInCleanupMode)
            {
                EditorGUILayout.Space(3);
                EditorGUILayout.BeginVertical("helpbox");
                {
                    EditorGUILayout.LabelField("清理模式", EditorStyles.miniBoldLabel);
                    EditorGUILayout.HelpBox("点击场景中清理范围内所有启用的预制体/网格类型", MessageType.Warning);

                    int enabledCount = GetEnabledObjectsCount();
                    EditorGUILayout.LabelField($"当前清理目标: 所有启用的对象类型 ({enabledCount}个)", EditorStyles.miniLabel);
                }
                EditorGUILayout.EndVertical();
            }

            if (GetEnabledObjectsCount() > 0)
            {
                EditorGUILayout.Space(3);
                EditorGUILayout.BeginVertical("helpbox");
                {
                    EditorGUILayout.LabelField("当前配置", EditorStyles.miniBoldLabel);

                    string modeText = GetModeDisplayText();
                    EditorGUILayout.LabelField($"模式: {modeText}", EditorStyles.miniLabel);

                    if (placementMode != PlacementMode.SinglePlacement)
                    {
                        EditorGUILayout.LabelField($"数量: {placementCount}", EditorStyles.miniLabel);
                    }

                    EditorGUILayout.LabelField($"方向: {placementRotation:0.0}°", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField($"可用对象: {GetEnabledObjectsCount()} 个", EditorStyles.miniLabel);

                    if (placedObjects.Count > 0)
                    {
                        EditorGUILayout.LabelField($"已放置: {placedObjects.Count} 个对象", EditorStyles.miniLabel);
                    }
                }
                EditorGUILayout.EndVertical();
            }
        }
        EditorGUILayout.EndVertical();
    }

    // 启动清理模式
    private void StartCleanupMode()
    {
        if (GetEnabledObjectsCount() == 0)
        {
            EditorUtility.DisplayDialog("错误", "请先启用至少一个预制体", "确定");
            return;
        }

        isInCleanupMode = true;
        SceneView.duringSceneGui += OnCleanupSceneGUI;
        Debug.Log("进入清理模式:点击场景中清理范围内的启用对象,按ESC退出");

        SceneView.RepaintAll();
        Repaint();
    }

    // 取消清理模式
    private void CancelCleanupMode()
    {
        isInCleanupMode = false;
        SceneView.duringSceneGui -= OnCleanupSceneGUI;
        Debug.Log("已退出清理模式");

        SceneView.RepaintAll();
        Repaint();
    }

    // 清理模式场景GUI处理
    private void OnCleanupSceneGUI(SceneView sceneView)
    {
        Event e = Event.current;

        if (e.type == EventType.ScrollWheel)
        {
            float delta = e.delta.y;
            if (e.control && e.alt)
            {
                placementRadius = Mathf.Clamp(placementRadius - delta * 0.5f, 1f, 50f);
                Repaint();
                e.Use();
                return;
            }
        }

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            CancelCleanupMode();
            e.Use();
            return;
        }

        if (e.type == EventType.MouseDown && e.button == 0 && !e.control)
        {
            Vector3 center = GetMouseWorldPosition(e.mousePosition);

            Collider[] colliders = Physics.OverlapSphere(center, placementRadius);
            int cleanedCount = 0;

            List<GameObject> objectsToDestroy = new List<GameObject>();

            foreach (var col in colliders)
            {
                GameObject go = col.gameObject;
                if (ShouldCleanup(go))
                {
                    GameObject rootObject = GetRootObject(go);
                    if (!objectsToDestroy.Contains(rootObject))
                    {
                        objectsToDestroy.Add(rootObject);
                    }
                }
            }

            foreach (GameObject obj in objectsToDestroy)
            {
                Undo.DestroyObjectImmediate(obj);
                placedObjects.Remove(obj);
                cleanedCount++;
            }

            Debug.Log($"清理范围内对象数量: {cleanedCount}");
            Repaint();
            e.Use();
            return;
        }

        if (e.type == EventType.Repaint)
        {
            DrawCleanupIndicator();
        }
    }

    // 获取对象的根对象（预制体根或自身）
    private GameObject GetRootObject(GameObject obj)
    {
        GameObject prefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(obj);
        return prefabRoot != null ? prefabRoot : obj;
    }

    // 判断对象是否应该被清理
    private bool ShouldCleanup(GameObject targetObject)
    {
        foreach (var obj in placeableObjects)
        {
            if (!obj.isEnabled || !obj.IsValid()) continue;

            if (obj.prefab != null)
            {
                // 修复预制体检测逻辑
                GameObject prefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(targetObject);
                if (prefabRoot != null)
                {
                    // 直接检查预制体实例是否来源于我们的预制体资源
                    GameObject originalPrefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(prefabRoot);
                    if (originalPrefab == obj.prefab)
                    {
                        return true;
                    }

                    // 备用检查：通过预制体连接检查
                    if (PrefabUtility.GetPrefabAssetType(obj.prefab) != PrefabAssetType.NotAPrefab)
                    {
                        GameObject instancePrefab = PrefabUtility.GetCorrespondingObjectFromSource(prefabRoot);
                        if (instancePrefab == obj.prefab)
                        {
                            return true;
                        }
                    }
                }
            }
            else if (obj.mesh != null)
            {
                MeshFilter meshFilter = targetObject.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh == obj.mesh)
                {
                    // 如果指定了材质，也要检查材质是否匹配
                    if (obj.material != null)
                    {
                        MeshRenderer meshRenderer = targetObject.GetComponent<MeshRenderer>();
                        if (meshRenderer != null && meshRenderer.sharedMaterial == obj.material)
                        {
                            return true;
                        }
                    }
                    else
                    {
                        return true; // 没有指定材质时，只要网格匹配即可
                    }
                }
            }
        }
        return false;
    }

    // 绘制清理模式指示器
    private void DrawCleanupIndicator()
    {
        Vector3 mouseWorldPos = GetMouseWorldPosition(Event.current.mousePosition);

        Handles.color = new Color(1f, 0.3f, 0.3f, 0.15f);
        Handles.DrawSolidDisc(mouseWorldPos, Vector3.up, placementRadius);
        Handles.color = Color.red;
        Handles.DrawWireDisc(mouseWorldPos, Vector3.up, placementRadius);

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.normal.textColor = Color.white;
        labelStyle.alignment = TextAnchor.MiddleCenter;
        labelStyle.fontStyle = FontStyle.Bold;
        labelStyle.fontSize = 12;

        int enabledCount = GetEnabledObjectsCount();
        string infoText = $"清理模式\n目标: 所有启用对象 ({enabledCount}个)";

        Handles.Label(mouseWorldPos + Vector3.up * 2f, infoText, labelStyle);
    }

    void OnDestroy()
    {
        if (isInPlacementMode)
        {
            CancelPlacementMode();
        }
        if (isInCleanupMode)
        {
            CancelCleanupMode();
        }
    }

    // 获取启用对象数量
    private int GetEnabledObjectsCount()
    {
        int count = 0;
        foreach (var obj in placeableObjects)
        {
            if (obj.isEnabled && obj.IsValid()) count++;
        }
        return count;
    }

    // 获取模式显示文本
    private string GetModeDisplayText()
    {
        switch (placementMode)
        {
            case PlacementMode.SinglePlacement:
                return "单个放置";
            case PlacementMode.StraightLineArrangement:
                return $"直线排列 (间隔: {lineSpacing:0.0})";
            case PlacementMode.RangePlacement:
                return $"圆形范围 ({distributionType}) (半径: {placementRadius:0.0})";
            default:
                return "";
        }
    }

    // 绘制范围指示器
    private void DrawRangeIndicator()
    {
        Vector3 placementPosition = GetMouseWorldPosition(lastMousePosition);
        Quaternion rotation = Quaternion.Euler(0, placementRotation, 0);

        switch (placementMode)
        {
            case PlacementMode.SinglePlacement:
                DrawSinglePlacementIndicator(placementPosition);
                break;
            case PlacementMode.StraightLineArrangement:
                DrawLinearRange(placementPosition, rotation);
                break;
            case PlacementMode.RangePlacement:
                DrawCircularArea(placementPosition);
                break;
        }

        Handles.color = Color.red;
        Handles.SphereHandleCap(0, placementPosition, Quaternion.identity, 0.1f, EventType.Repaint);

        DrawDirectionIndicator(placementPosition, rotation);

        if (showPreview && placementMode != PlacementMode.SinglePlacement)
        {
            DrawPlacementPreviews(placementPosition, rotation);
        }

        DrawInfoLabel(placementPosition);
    }

    // 绘制单个放置指示器
    private void DrawSinglePlacementIndicator(Vector3 center)
    {
        Handles.color = new Color(0, 1, 0, 0.1f); // 绿色半透明
        Handles.DrawSolidDisc(center, Vector3.up, 0.25f);
        Handles.color = new Color(0, 1, 0, 0.35f); // 绿色描边
        Handles.DrawWireDisc(center, Vector3.up, 0.25f);
    }

    // 绘制信息标签
    private void DrawInfoLabel(Vector3 position)
    {
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.normal.textColor = Color.white;
        labelStyle.alignment = TextAnchor.MiddleCenter;
        labelStyle.fontStyle = FontStyle.Bold;
        labelStyle.fontSize = 12;

        string infoText = GetModeDisplayText();
        if (placementMode != PlacementMode.SinglePlacement)
        {
            infoText += $"\n数量: {placementCount}";
        }
        infoText += $"\n方向: {placementRotation:0.0}°";
        infoText += $"\n对象: {GetEnabledObjectsCount()} 个可用";

        Handles.Label(position + Vector3.up * 2f, infoText, labelStyle);
    }

    // 绘制直线范围 (修复版本，与实际放置位置一致)
    private void DrawLinearRange(Vector3 center, Quaternion rotation)
    {
        Handles.color = new Color(0, 1, 0, 0.1f); // 绿色半透明

        // 绘制实际的放置点位置预览
        for (int i = 0; i < placementCount; i++)
        {
            Vector3 point = center + rotation * Vector3.right * (i - (placementCount - 1) * 0.5f) * lineSpacing;

            if (lineRandomOffset > 0)
            {
                // 显示随机偏移范围
                Handles.DrawSolidDisc(point, Vector3.up, lineRandomOffset);
            }
            else
            {
                // 没有随机偏移时，显示精确位置
                Handles.color = new Color(0, 1, 0, 0.1f); // 绿色半透明
                Handles.DrawSolidDisc(point, Vector3.up, 0.15f);
                Handles.color = new Color(0, 1, 0, 0.35f); // 绿色描边
            }
        }
    }

    // 绘制方向指示器
    private void DrawDirectionIndicator(Vector3 center, Quaternion rotation)
    {
        Vector3 direction = rotation * Vector3.right;

        Handles.color = Color.yellow;
        Handles.ArrowHandleCap(0, center, Quaternion.LookRotation(direction), 1f, EventType.Repaint);

        Handles.color = new Color(1, 1, 0, 0.5f);
        Handles.DrawDottedLine(center, center + direction, 5f);
    }

    // 绘制放置预览（支持法线对齐预览）
    private void DrawPlacementPreviews(Vector3 center, Quaternion rotation)
    {
        Vector3[] positions = CalculatePlacementPositions(center, rotation);
        Quaternion[] rotations = CalculatePlacementRotations(rotation, positions);

        Handles.color = new Color(1, 0, 1, 0.8f);
        foreach (Vector3 pos in positions)
        {
            Handles.SphereHandleCap(0, pos, Quaternion.identity, 0.15f, EventType.Repaint);
        }

        if (GetEnabledObjectsCount() > 0)
        {
            Handles.color = new Color(1, 1, 1, 0.3f);
            for (int i = 0; i < positions.Length; i++)
            {
                // 使用计算出的旋转来显示预览
                Handles.CubeHandleCap(0, positions[i], rotations[i], 0.3f, EventType.Repaint);

                // 如果启用了法线对齐，显示法线方向箭头（只设置箭头颜色，不影响Cube颜色）
                if (alignToSurface && alignToSurfaceNormal)
                {
                    Color arrowColor = new Color(0.5f, 0.2f, 1f, 0.85f); // 蓝紫色
                    Handles.color = arrowColor;
                    Vector3 up = rotations[i] * Vector3.up;
                    Handles.ArrowHandleCap(0, positions[i], Quaternion.LookRotation(up), 0.5f, EventType.Repaint);

                    Handles.color = new Color(1, 1, 1, 0.3f); // 恢复Cube颜色，防止后续影响
                }
            }
        }
    }

    // 获取鼠标世界坐标
    private Vector3 GetMouseWorldPosition(Vector2 mousePosition)
    {
        Ray worldRay = HandleUtility.GUIPointToWorldRay(mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(worldRay, out hit, Mathf.Infinity, surfaceLayer) && alignToSurface)
        {
            return hit.point;
        }
        else
        {
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            float distance;
            if (groundPlane.Raycast(worldRay, out distance))
            {
                return worldRay.GetPoint(distance);
            }
        }

        return Vector3.zero;
    }

    // 调整位置贴合表面
    private Vector3 AdjustPositionToSurface(Vector3 position, out Quaternion surfaceRotation)
    {
        surfaceRotation = Quaternion.identity;

        if (alignToSurface)
        {
            RaycastHit hit;
            if (Physics.Raycast(position + Vector3.up * 10f, Vector3.down, out hit, Mathf.Infinity, surfaceLayer))
            {
                // 如果启用了法线对齐，计算表面旋转
                if (alignToSurfaceNormal)
                {
                    // 计算从Vector3.up到hit.normal的旋转
                    surfaceRotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
                }

                return hit.point;
            }
        }
        return position;
    }

    // 重载原来的方法以保持兼容性
    private Vector3 AdjustPositionToSurface(Vector3 position)
    {
        Quaternion surfaceRotation;
        return AdjustPositionToSurface(position, out surfaceRotation);
    }

    // 计算放置旋转角度（修复版本，避免随机旋转一直变化）
    private Quaternion[] CalculatePlacementRotations(Quaternion baseRotation, Vector3[] positions)
    {
        List<Quaternion> rotations = new List<Quaternion>();
        int count = positions.Length;

        for (int i = 0; i < count; i++)
        {
            Quaternion rotation = baseRotation;

            // 随机旋转各轴 - 使用固定的种子确保旋转稳定
            if (randomRotation)
            {
                // 为每个位置使用不同但固定的种子
                System.Random rotRandom = new System.Random(randomSeed + 1000 + i * 7919);

                float rx = randomRotationX ? (float)rotRandom.NextDouble() * 360f : 0f;
                float ry = randomRotationY ? (float)rotRandom.NextDouble() * 360f : 0f;
                float rz = randomRotationZ ? (float)rotRandom.NextDouble() * 360f : 0f;
                Quaternion randRot = Quaternion.Euler(rx, ry, rz);

                if (alignToSurface && alignToSurfaceNormal)
                {
                    Quaternion surfaceRotation;
                    AdjustPositionToSurface(positions[i], out surfaceRotation);
                    rotation = surfaceRotation * randRot;
                }
                else
                {
                    rotation = baseRotation * randRot;
                }
            }
            else if (alignToSurface && alignToSurfaceNormal)
            {
                Quaternion surfaceRotation;
                AdjustPositionToSurface(positions[i], out surfaceRotation);
                rotation = surfaceRotation * baseRotation;
            }

            rotations.Add(rotation);
        }

        return rotations.ToArray();
    }

    // 计算放置缩放（修复版本，使用固定种子）
    private Vector3[] CalculatePlacementScales()
    {
        List<Vector3> scales = new List<Vector3>();
        int count = placementMode == PlacementMode.SinglePlacement ? 1 : placementCount;

        for (int i = 0; i < count; i++)
        {
            Vector3 scale = Vector3.one;
            if (randomScale)
            {
                // 为每个位置使用不同但固定的种子
                System.Random scaleRand = new System.Random(randomSeed + 2000 + i * 104729);
                float randomScaleValue = Mathf.Lerp(minScale, maxScale, (float)scaleRand.NextDouble());
                scale = Vector3.one * randomScaleValue;
            }
            scales.Add(scale);
        }

        return scales.ToArray();
    }

    // 在鼠标位置放置对象（更新版本）
    private void PlaceObjectsAtMousePosition()
    {
        if (GetEnabledObjectsCount() == 0)
        {
            EditorUtility.DisplayDialog("错误", "没有可用的对象进行放置", "确定");
            return;
        }

        Vector3 placementPosition = GetMouseWorldPosition(lastMousePosition);
        Quaternion baseRotation = Quaternion.Euler(0, placementRotation, 0);
        Vector3[] positions = CalculatePlacementPositions(placementPosition, baseRotation);
        Quaternion[] rotations = CalculatePlacementRotations(baseRotation, positions);
        Vector3[] scales = CalculatePlacementScales();

        for (int i = 0; i < positions.Length; i++)
        {
            PlaceableObject selectedObj = GetRandomObject();
            if (selectedObj == null) continue;

            GameObject newObject = null;

            if (selectedObj.prefab != null)
            {
                newObject = (GameObject)PrefabUtility.InstantiatePrefab(selectedObj.prefab);
            }
            else if (selectedObj.mesh != null)
            {
                newObject = new GameObject($"PlacedMesh_{selectedObj.mesh.name}_{placedObjects.Count + i}");
                MeshFilter meshFilter = newObject.AddComponent<MeshFilter>();
                MeshRenderer meshRenderer = newObject.AddComponent<MeshRenderer>();

                meshFilter.mesh = selectedObj.mesh;
                if (selectedObj.material != null)
                {
                    meshRenderer.material = selectedObj.material;
                }
                else
                {
                    meshRenderer.material = new Material(Shader.Find("Standard"));
                }
            }

            if (newObject != null)
            {
                newObject.transform.position = positions[i];
                newObject.transform.rotation = rotations[i];
                newObject.transform.localScale = scales[i];

                placedObjects.Add(newObject);
                Undo.RegisterCreatedObjectUndo(newObject, "Place Object");
            }
        }

        Debug.Log($"成功放置 {positions.Length} 个对象，模式: {placementMode}，方向: {placementRotation:0}°");
        Repaint();
    }

    // 根据权重随机选择一个启用的对象
    private PlaceableObject GetRandomObject()
    {
        if (placeableObjects == null || placeableObjects.Count == 0)
            return null;

        // 获取所有启用且有效的对象
        List<PlaceableObject> enabledObjects = new List<PlaceableObject>();
        foreach (var obj in placeableObjects)
        {
            if (obj.isEnabled && obj.IsValid())
            {
                enabledObjects.Add(obj);
            }
        }

        if (enabledObjects.Count == 0)
            return null;

        // 如果只有一个对象，直接返回
        if (enabledObjects.Count == 1)
            return enabledObjects[0];

        // 计算总权重
        float totalWeight = 0f;
        foreach (var obj in enabledObjects)
        {
            totalWeight += obj.weight;
        }

        if (totalWeight <= 0f)
            return enabledObjects[0]; // 如果权重都是0或负数，返回第一个

        // 确保随机数生成器已初始化
        if (random == null)
        {
            random = new System.Random(randomSeed);
        }

        // 生成随机值
        float randomValue = (float)random.NextDouble() * totalWeight;

        // 根据权重选择对象
        float currentWeight = 0f;
        foreach (var obj in enabledObjects)
        {
            currentWeight += obj.weight;
            if (randomValue <= currentWeight)
            {
                return obj;
            }
        }

        // 兜底返回最后一个对象（理论上不应该执行到这里）
        return enabledObjects[enabledObjects.Count - 1];
    }

    // 清除所有已放置对象
    private void ClearAllPlacedObjects()
    {
        if (placedObjects.Count == 0)
        {
            Debug.Log("没有需要清除的对象");
            return;
        }

        if (EditorUtility.DisplayDialog("确认清除", $"确定要删除所有 {placedObjects.Count} 个已放置的对象吗?", "确定", "取消"))
        {
            foreach (GameObject obj in placedObjects)
            {
                if (obj != null)
                {
                    Undo.DestroyObjectImmediate(obj);
                }
            }
            placedObjects.Clear();
            Debug.Log("已清除所有放置的对象");
            Repaint();
        }
    }

    // 启动放置模式
    private void StartPlacementMode()
    {
        if (GetEnabledObjectsCount() == 0)
        {
            EditorUtility.DisplayDialog("错误", "没有可用的对象进行放置", "确定");
            return;
        }

        isInPlacementMode = true;
        InitializeRandom();
        SceneView.duringSceneGui += OnPlacementSceneGUI;
        Debug.Log("进入放置模式:在场景中点击放置对象,按ESC退出");

        SceneView.RepaintAll();
        Repaint();
    }

    // 取消放置模式
    private void CancelPlacementMode()
    {
        isInPlacementMode = false;
        SceneView.duringSceneGui -= OnPlacementSceneGUI;
        Debug.Log("已退出放置模式");

        SceneView.RepaintAll();
        Repaint();
    }

    // 初始化随机数生成器
    private void InitializeRandom()
    {
        random = new System.Random(randomSeed);
        rotationRandom = new System.Random(randomSeed + 1000);
        scaleRandom = new System.Random(randomSeed + 2000);
    }

    // 放置模式场景GUI处理
    private void OnPlacementSceneGUI(SceneView sceneView)
    {
        Event e = Event.current;

        lastMousePosition = e.mousePosition;

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            CancelPlacementMode();
            e.Use();
            return;
        }

        if (e.type == EventType.ScrollWheel)
        {
            float delta = e.delta.y;

            if (e.control && e.alt)
            {
                // Ctrl+Alt+滚轮：调整范围/间隔
                if (placementMode == PlacementMode.RangePlacement)
                {
                    placementRadius = Mathf.Clamp(placementRadius - delta * 0.5f, 1f, 50f);
                }
                else if (placementMode == PlacementMode.StraightLineArrangement)
                {
                    lineSpacing = Mathf.Clamp(lineSpacing - delta * 0.1f, 0.5f, 10f);
                }
            }
            else if (e.alt)
            {
                // Alt+滚轮：调整放置数量（仅在非单个放置模式下生效）
                if (placementMode != PlacementMode.SinglePlacement)
                {
                    int step = delta > 0 ? -1 : 1;
                    placementCount = Mathf.Clamp(placementCount + step, 2, placementMode == PlacementMode.RangePlacement ? 100 : 50);
                    Debug.Log($"Alt+滚轮: 放置数量调整为 {placementCount}");
                    Repaint();
                }
            }
            else if (e.control)
            {
                // Ctrl+滚轮：旋转角度
                placementRotation = (placementRotation - delta * 5f) % 360f;
                if (placementRotation < 0) placementRotation += 360f;
            }
            else if (e.shift)
            {
                // Shift+滚轮：随机种子变化（修复随机旋转问题的关键）
                int step;
                if (e.delta.y > 0)
                    step = -1; // 下滑减少
                else
                    step = 1;  // 上滑增加

                randomSeed = Mathf.Max(0, randomSeed + step);
                Debug.Log($"Shift+滚轮: 随机种子调整为 {randomSeed}");
                // 移除 InitializeRandom() 调用，因为我们现在在计算时使用固定种子
            }
            Repaint();
            e.Use();
            return;
        }

        if (e.type == EventType.MouseDown && e.button == 0 && !e.control)
        {
            PlaceObjectsAtMousePosition();
            e.Use();
            return;
        }

        if (e.type == EventType.Repaint)
        {
            DrawRangeIndicator();
        }
    }

    // 计算放置位置 (修复版本，修复均匀分布和直线预览问题)
    private Vector3[] CalculatePlacementPositions(Vector3 center, Quaternion rotation)
    {
        List<Vector3> positions = new List<Vector3>();
        int count = placementMode == PlacementMode.SinglePlacement ? 1 : placementCount;
        int positionSeedBase = randomSeed * 99991;

        switch (placementMode)
        {
            case PlacementMode.SinglePlacement:
                positions.Add(AdjustPositionToSurface(center));
                break;

            case PlacementMode.StraightLineArrangement:
                for (int i = 0; i < count; i++)
                {
                    // 修复：使用与预览相同的计算方式
                    Vector3 basePosition = center + rotation * Vector3.right * (i - (count - 1) * 0.5f) * lineSpacing;
                    if (lineRandomOffset > 0f)
                    {
                        System.Random r = new System.Random(positionSeedBase + i * 7919);
                        float ox = ((float)r.NextDouble() - 0.5f) * 2f; // -1到1的范围
                        float oz = ((float)r.NextDouble() - 0.5f) * 2f; // -1到1的范围
                        basePosition += new Vector3(ox, 0, oz) * lineRandomOffset;
                    }
                    positions.Add(AdjustPositionToSurface(basePosition));
                }
                break;

            case PlacementMode.RangePlacement:
                for (int i = 0; i < count; i++)
                {
                    Vector3 pos;
                    if (distributionType == DistributionType.UniformDistribution)
                    {
                        if (count == 1)
                        {
                            pos = center;
                        }
                        else
                        {
                            // 网格式均匀分布
                            int gridSize = Mathf.CeilToInt(Mathf.Sqrt(count));
                            int row = i / gridSize;
                            int col = i % gridSize;
                            float stepX = (placementRadius * 2f) / gridSize;
                            float stepZ = (placementRadius * 2f) / gridSize;
                            float x = (col - gridSize * 0.5f + 0.5f) * stepX;
                            float z = (row - gridSize * 0.5f + 0.5f) * stepZ;
                            Vector3 localPos = new Vector3(x, 0, z);

                            // 确保在圆形范围内
                            if (localPos.magnitude <= placementRadius)
                            {
                                pos = center + Quaternion.Euler(0, placementRotation, 0) * localPos;
                            }
                            else
                            {
                                // 如果超出圆形，投影到圆边界
                                localPos = localPos.normalized * placementRadius;
                                pos = center + Quaternion.Euler(0, placementRotation, 0) * localPos;
                            }
                        }
                    }
                    else
                    {
                        System.Random r = new System.Random(positionSeedBase + i * 104729);
                        float x, z;
                        do
                        {
                            x = ((float)r.NextDouble() * 2f - 1f) * placementRadius;
                            z = ((float)r.NextDouble() * 2f - 1f) * placementRadius;
                        } while (x * x + z * z > placementRadius * placementRadius);

                        Vector3 localPos = new Vector3(x, 0, z);
                        pos = center + Quaternion.Euler(0, placementRotation, 0) * localPos;
                    }
                    positions.Add(AdjustPositionToSurface(pos));
                }
                break;
        }

        return positions.ToArray();
    }

    // 绘制圆形区域
    private void DrawCircularArea(Vector3 center)
    {
        Handles.color = new Color(0, 1, 0, 0.1f); // 绿色半透明
        Handles.DrawSolidDisc(center, Vector3.up, placementRadius);

        Handles.color = new Color(0, 1, 0, 0.35f); // 绿色描边
        Handles.DrawWireDisc(center, Vector3.up, placementRadius);
    }

    // LayerMask字段
    private LayerMask LayerMaskField(string label, LayerMask layerMask)
    {
        List<string> layers = new List<string>();
        List<int> layerNumbers = new List<int>();

        for (int i = 0; i < 32; i++)
        {
            string layerName = LayerMask.LayerToName(i);
            if (layerName != "")
            {
                layers.Add(layerName);
                layerNumbers.Add(i);
            }
        }

        int maskWithoutEmpty = 0;
        for (int i = 0; i < layerNumbers.Count; i++)
        {
            if (((1 << layerNumbers[i]) & layerMask.value) > 0)
                maskWithoutEmpty |= 1 << i;
        }

        maskWithoutEmpty = EditorGUILayout.MaskField(label, maskWithoutEmpty, layers.ToArray());
        int mask = 0;
        for (int i = 0; i < layerNumbers.Count; i++)
        {
            if ((maskWithoutEmpty & (1 << i)) != 0)
                mask |= 1 << layerNumbers[i];
        }

        return mask;
    }
}