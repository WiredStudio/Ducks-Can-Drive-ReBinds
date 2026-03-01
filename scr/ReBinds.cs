using MelonLoader;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[assembly: MelonInfo(typeof(ReBinds.VehicleControlMod), "Ducks Can Drive - ReBinds", "1.0.0", "WiredStudio")]
[assembly: MelonGame("Joseph Cook", "Ducks Can Drive")]

//Welcome to my hell, this took way longer than I expected.
namespace ReBinds
{
    public static class Bindings
    {
        private static MelonPreferences_Category category;
        public static MelonPreferences_Entry<KeyCode> ForwardKey;
        public static MelonPreferences_Entry<KeyCode> BackwardKey;
        public static MelonPreferences_Entry<KeyCode> LeftKey;
        public static MelonPreferences_Entry<KeyCode> RightKey;
        public static MelonPreferences_Entry<KeyCode> DriftKey;
        public static MelonPreferences_Entry<KeyCode> HonkKey;
        public static MelonPreferences_Entry<KeyCode> RestartKey;

        public static void Initialize()
        {
            category = MelonPreferences.CreateCategory("KeyBindings", "Vehicle Control Key Bindings");
            ForwardKey = category.CreateEntry("Forward", KeyCode.W, "Forward");
            BackwardKey = category.CreateEntry("Backward", KeyCode.S, "Backward");
            LeftKey = category.CreateEntry("Left", KeyCode.A, "Left");
            RightKey = category.CreateEntry("Right", KeyCode.D, "Right");
            DriftKey = category.CreateEntry("Drift", KeyCode.Space, "Drift (Jump)");
            HonkKey = category.CreateEntry("Honk", KeyCode.H, "Honk (Quack)");
            RestartKey = category.CreateEntry("Restart", KeyCode.R, "Restart (Not Implemented)");
        }

        public static IEnumerable<MelonPreferences_Entry<KeyCode>> AllBindings
        {
            get
            {
                yield return ForwardKey;
                yield return BackwardKey;
                yield return LeftKey;
                yield return RightKey;
                yield return DriftKey;
                yield return HonkKey;
                yield return RestartKey;
            }
        }
    }

    public class VehicleControlMod : MelonMod
    {
        private static AudioSource quackSource;
        private bool showBindingsWindow = false;
        private string currentBindingAction = "";
        private KeyCode pendingKey;
        private bool conflictDetected = false;
        private string conflictingAction = "";
        private Vector2 scrollPos;
        private bool buttonAdded = false;
        private int findAttempts;

        private bool showRestartMessage = false;
        private float restartMessageTimer = 0f;

        private readonly float windowWidth = 450;
        private readonly float windowHeight = 350;

        private List<GraphicRaycaster> disabledRaycasters = new List<GraphicRaycaster>();
        private bool wasModalActive = false;

        private GUIStyle windowStyle;
        private GUIStyle labelStyle;
        private GUIStyle buttonStyle;
        private GUIStyle conflictBoxStyle;
        private GUIStyle messageStyle;

        private GameObject controlsButton;

        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("Vehicle Control Mod initializing...");
            Bindings.Initialize();
            HarmonyInstance.PatchAll();
            MelonLogger.Msg("Harmony patches applied.");
        }

        public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
        {
            if (disabledRaycasters.Count > 0)
            {
                DisableModalMode();
            }
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            MelonLogger.Msg($"Scene loaded: {sceneName}");
            if (sceneName == "Menu")
            {
                buttonAdded = false;
                controlsButton = null;
                if (showBindingsWindow)
                {
                    showBindingsWindow = false;
                    DisableModalMode();
                }
                MelonCoroutines.Start(WaitForMenuAndAddButton());
            }
        }

        private IEnumerator WaitForMenuAndAddButton()
        {
            yield return null;

            float timeout = Time.time + 5f;
            GameObject settingsMenu = null;
            Canvas canvas = null;

            while (Time.time < timeout)
            {
                settingsMenu = FindInactiveGameObject("SettingsMenu");
                if (settingsMenu != null)
                {
                    canvas = settingsMenu.GetComponentInParent<Canvas>();
                    if (canvas != null && canvas.enabled)
                    {
                        GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
                        if (raycaster != null && raycaster.enabled)
                        {
                            break;
                        }
                    }
                }
                yield return new WaitForSeconds(0.2f);
            }

            if (settingsMenu != null && canvas != null)
            {
                MelonLogger.Msg("SettingsMenu is ready. Adding button.");
                AddControlsButton(settingsMenu);
                buttonAdded = true;
            }
            else
            {
                MelonLogger.Warning("SettingsMenu not ready after timeout. Creating standalone button.");
                CreateStandaloneButton();
                buttonAdded = true;
            }
        }

        private void EnableModalMode()
        {
            disabledRaycasters.Clear();
            GraphicRaycaster[] allRaycasters = Resources.FindObjectsOfTypeAll<GraphicRaycaster>();
            foreach (var raycaster in allRaycasters)
            {
                if (raycaster.enabled)
                {
                    raycaster.enabled = false;
                    disabledRaycasters.Add(raycaster);
                }
            }
        }

        private void DisableModalMode()
        {
            foreach (var raycaster in disabledRaycasters)
            {
                if (raycaster != null)
                    raycaster.enabled = true;
            }
            disabledRaycasters.Clear();
        }

        private GameObject FindInactiveGameObject(string name)
        {
            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go.name == name)
                    return go;
            }
            return null;
        }

        private void AddControlsButton(GameObject settingsMenu)
        {
            Transform parent = settingsMenu.transform;
            Transform buttonPanel = FindButtonPanel(settingsMenu.transform);
            if (buttonPanel != null)
                parent = buttonPanel;

            if (parent.Find("ControlsButton") != null)
                return;

            controlsButton = new GameObject("ControlsButton", typeof(RectTransform), typeof(Image), typeof(Button));
            controlsButton.transform.SetParent(parent, false);

            RectTransform rect = controlsButton.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(240, 60);
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(0, 0);
            rect.pivot = new Vector2(0, 0);
            rect.anchoredPosition = new Vector2(10, 10);

            Image img = controlsButton.GetComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            img.raycastTarget = true;

            Button btn = controlsButton.GetComponent<Button>();
            btn.targetGraphic = img;

            GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(controlsButton.transform, false);
            Text txt = textObj.GetComponent<Text>();

            Button existingButton = parent.GetComponentInChildren<Button>();
            if (existingButton != null)
            {
                Text existingText = existingButton.GetComponentInChildren<Text>();
                if (existingText != null)
                {
                    txt.font = existingText.font;
                    txt.fontSize = existingText.fontSize;
                    txt.color = existingText.color;
                    txt.fontStyle = existingText.fontStyle;
                    txt.alignment = existingText.alignment;
                }
                else
                {
                    SetDefaultTextStyle(txt);
                }
            }
            else
            {
                SetDefaultTextStyle(txt);
            }

            txt.text = "Controls";
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            btn.onClick.AddListener(() => { showBindingsWindow = true; });

            MelonLogger.Msg("Controls button added.");
        }

        private void SetDefaultTextStyle(Text txt)
        {
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (txt.font == null)
            {
                Font[] fonts = Resources.FindObjectsOfTypeAll<Font>();
                if (fonts.Length > 0)
                    txt.font = fonts[0];
            }
            txt.fontSize = 24;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
        }

        private Transform FindButtonPanel(Transform root)
        {
            foreach (Transform child in root)
            {
                if (child.name.Contains("Panel") || child.name.Contains("Content") || child.name.Contains("Buttons"))
                {
                    if (child.GetComponentInChildren<Button>() != null)
                        return child;
                }
                Transform deeper = FindButtonPanel(child);
                if (deeper != null)
                    return deeper;
            }
            return null;
        }

        private void CreateStandaloneButton()
        {
            Canvas canvas = GameObject.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                MelonLogger.Error("No canvas found. Cannot create standalone button.");
                return;
            }

            controlsButton = new GameObject("ControlsButton", typeof(RectTransform), typeof(Image), typeof(Button));
            controlsButton.transform.SetParent(canvas.transform, false);

            RectTransform rect = controlsButton.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(240, 60);
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(0, 0);
            rect.pivot = new Vector2(0, 0);
            rect.anchoredPosition = new Vector2(10, 10);

            Image img = controlsButton.GetComponent<Image>();
            img.color = Color.gray;

            Button btn = controlsButton.GetComponent<Button>();
            btn.targetGraphic = img;

            GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(controlsButton.transform, false);
            Text txt = textObj.GetComponent<Text>();
            txt.text = "Controls";
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (txt.font == null)
            {
                Font[] fonts = Resources.FindObjectsOfTypeAll<Font>();
                if (fonts.Length > 0)
                    txt.font = fonts[0];
            }
            txt.fontSize = 24;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            btn.onClick.AddListener(() => { showBindingsWindow = true; });

            MelonLogger.Msg("Standalone button created.");
        }

        public override void OnUpdate()
        {
            if (quackSource == null)
            {
                GameObject quackObj = FindInactiveGameObject("Quack");
                if (quackObj != null)
                {
                    quackSource = quackObj.GetComponent<AudioSource>();
                    if (quackSource != null)
                        MelonLogger.Msg("Found Quack audio source.");
                }
            }

            if (quackSource != null && Input.GetKeyDown(Bindings.HonkKey.Value))
            {
                quackSource.pitch = Random.Range(0.8f, 1.2f);
                quackSource.Play();
            }

            if (Input.GetKeyDown(Bindings.RestartKey.Value))
            {
                MelonLogger.Msg("Restart key pressed - not implemented.");
                showRestartMessage = true;
                restartMessageTimer = 3f;
            }

            if (showRestartMessage)
            {
                restartMessageTimer -= Time.deltaTime;
                if (restartMessageTimer <= 0)
                    showRestartMessage = false;
            }

            if (showBindingsWindow != wasModalActive)
            {
                if (showBindingsWindow)
                    EnableModalMode();
                else
                    DisableModalMode();
                wasModalActive = showBindingsWindow;
            }
        }

        public override void OnGUI()
        {
            if (showRestartMessage)
            {
                if (messageStyle == null)
                {
                    messageStyle = new GUIStyle(GUI.skin.box);
                    messageStyle.normal.background = MakeTex(2, 2, new Color(0.2f, 0.2f, 0.2f, 0.9f));
                    messageStyle.normal.textColor = Color.yellow;
                    messageStyle.fontSize = 20;
                    messageStyle.alignment = TextAnchor.MiddleCenter;
                }
                float msgWidth = 300;
                float msgHeight = 60;
                Rect msgRect = new Rect(Screen.width / 2 - msgWidth / 2, Screen.height / 2 - msgHeight / 2, msgWidth, msgHeight);
                GUI.Box(msgRect, "Restart not implemented yet!", messageStyle);
            }

            if (showBindingsWindow)
            {
                if (windowStyle == null)
                {
                    windowStyle = new GUIStyle();
                    windowStyle.normal.background = MakeTex(2, 2, new Color(0.1f, 0.1f, 0.1f, 0.95f));
                    windowStyle.normal.textColor = Color.white;
                    windowStyle.fontSize = 20;
                    windowStyle.fontStyle = FontStyle.Bold;
                    windowStyle.alignment = TextAnchor.UpperCenter;
                    windowStyle.padding = new RectOffset(10, 10, 10, 10);

                    labelStyle = new GUIStyle(GUI.skin.label);
                    labelStyle.normal.textColor = Color.white;
                    labelStyle.fontSize = 16;

                    buttonStyle = new GUIStyle(GUI.skin.button);
                    buttonStyle.normal.background = MakeTex(2, 2, new Color(0.3f, 0.3f, 0.3f, 1f));
                    buttonStyle.hover.background = MakeTex(2, 2, new Color(0.5f, 0.5f, 0.5f, 1f));
                    buttonStyle.active.background = MakeTex(2, 2, new Color(0.2f, 0.2f, 0.2f, 1f));
                    buttonStyle.normal.textColor = Color.white;
                    buttonStyle.fontSize = 14;

                    conflictBoxStyle = new GUIStyle(GUI.skin.box);
                    conflictBoxStyle.normal.background = MakeTex(2, 2, new Color(0.8f, 0.2f, 0.2f, 0.8f));
                    conflictBoxStyle.normal.textColor = Color.white;
                    conflictBoxStyle.fontSize = 14;
                    conflictBoxStyle.alignment = TextAnchor.MiddleCenter;
                }

                Rect windowRect = new Rect(Screen.width / 2 - windowWidth / 2, Screen.height / 2 - windowHeight / 2, windowWidth, windowHeight);
                GUI.Box(windowRect, "Key Bindings", windowStyle);
                GUILayout.BeginArea(new Rect(windowRect.x + 10, windowRect.y + 30, windowRect.width - 20, windowRect.height - 40));
                DrawBindingsContent();
                GUILayout.EndArea();
            }
        }

        private void DrawBindingsContent()
        {
            if (conflictDetected)
            {
                GUILayout.Box($"Warning: {pendingKey} is already used for {conflictingAction}. Override?", conflictBoxStyle);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Yes", buttonStyle, GUILayout.Height(30)))
                {
                    AssignKey(currentBindingAction, pendingKey, true);
                    conflictDetected = false;
                    currentBindingAction = "";
                }
                if (GUILayout.Button("No", buttonStyle, GUILayout.Height(30)))
                {
                    conflictDetected = false;
                    currentBindingAction = "";
                }
                GUILayout.EndHorizontal();
            }
            else if (!string.IsNullOrEmpty(currentBindingAction))
            {
                if (currentBindingAction == "Restart")
                {
                    GUILayout.Label("Restart functionality is not implemented yet.\nYou can bind a key, but it won't do anything.", labelStyle, GUILayout.Height(60));
                }
                GUILayout.Label($"Press any key for {currentBindingAction}...", labelStyle, GUILayout.Height(30));
                if (Event.current.isKey && Event.current.keyCode != KeyCode.None)
                {
                    pendingKey = Event.current.keyCode;
                    var conflict = Bindings.AllBindings.FirstOrDefault(b => b.Value == pendingKey && b.DisplayName != currentBindingAction);
                    if (conflict != null)
                    {
                        conflictingAction = conflict.DisplayName;
                        conflictDetected = true;
                    }
                    else
                    {
                        AssignKey(currentBindingAction, pendingKey, false);
                        currentBindingAction = "";
                    }
                }
            }
            else
            {
                scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.Width(windowWidth - 20), GUILayout.Height(200));
                DrawBindingRow("Forward", Bindings.ForwardKey.Value);
                DrawBindingRow("Backward", Bindings.BackwardKey.Value);
                DrawBindingRow("Left", Bindings.LeftKey.Value);
                DrawBindingRow("Right", Bindings.RightKey.Value);
                DrawBindingRow("Drift", Bindings.DriftKey.Value);
                DrawBindingRow("Honk", Bindings.HonkKey.Value);
                DrawBindingRow("Restart", Bindings.RestartKey.Value);
                GUILayout.EndScrollView();

                if (GUILayout.Button("Close", buttonStyle, GUILayout.Height(40)))
                {
                    showBindingsWindow = false;
                }
            }
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        private void DrawBindingRow(string action, KeyCode currentKey)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(action + ":", labelStyle, GUILayout.Width(100));
            if (GUILayout.Button(currentKey.ToString(), buttonStyle, GUILayout.Width(150), GUILayout.Height(30)))
            {
                currentBindingAction = action;
                conflictDetected = false;
            }
            GUILayout.EndHorizontal();
        }

        private void AssignKey(string action, KeyCode key, bool overrideConflict)
        {
            if (overrideConflict)
            {
                var conflict = Bindings.AllBindings.FirstOrDefault(b => b.Value == key && b.DisplayName != action);
                if (conflict != null)
                    conflict.Value = KeyCode.None;
            }

            switch (action)
            {
                case "Forward": Bindings.ForwardKey.Value = key; break;
                case "Backward": Bindings.BackwardKey.Value = key; break;
                case "Left": Bindings.LeftKey.Value = key; break;
                case "Right": Bindings.RightKey.Value = key; break;
                case "Drift": Bindings.DriftKey.Value = key; break;
                case "Honk": Bindings.HonkKey.Value = key; break;
                case "Restart": Bindings.RestartKey.Value = key; break;
                default: return;
            }
            MelonPreferences.Save();
            MelonLogger.Msg($"Bound {action} to {key}");
        }
    }

    [HarmonyPatch]
    public static class InputPatches
    {
        [HarmonyPatch(typeof(Input), "GetAxis", typeof(string))]
        [HarmonyPrefix]
        public static bool GetAxisPrefix(string axisName, ref float __result)
        {
            if (axisName == "Horizontal")
            {
                float val = 0f;
                if (Input.GetKey(Bindings.LeftKey.Value)) val -= 1f;
                if (Input.GetKey(Bindings.RightKey.Value)) val += 1f;
                __result = val;
                return false;
            }
            if (axisName == "Vertical")
            {
                float val = 0f;
                if (Input.GetKey(Bindings.ForwardKey.Value)) val += 1f;
                if (Input.GetKey(Bindings.BackwardKey.Value)) val -= 1f;
                __result = val;
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(Input), "GetButton", typeof(string))]
        [HarmonyPrefix]
        public static bool GetButtonPrefix(string buttonName, ref bool __result)
        {
            if (buttonName == "Jump")
            {
                __result = Input.GetKey(Bindings.DriftKey.Value);
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(Input), "GetButtonDown", typeof(string))]
        [HarmonyPrefix]
        public static bool GetButtonDownPrefix(string buttonName, ref bool __result)
        {
            if (buttonName == "Jump")
            {
                __result = Input.GetKeyDown(Bindings.DriftKey.Value);
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(Input), "GetButtonUp", typeof(string))]
        [HarmonyPrefix]
        public static bool GetButtonUpPrefix(string buttonName, ref bool __result)
        {
            if (buttonName == "Jump")
            {
                __result = Input.GetKeyUp(Bindings.DriftKey.Value);
                return false;
            }
            return true;
        }
    }
}

