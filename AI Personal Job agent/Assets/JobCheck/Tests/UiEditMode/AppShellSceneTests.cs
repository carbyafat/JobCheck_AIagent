using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace JobCheck.Ui.Editor.Tests
{
    public sealed class AppShellSceneTests
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string AllJobsPrefabPath = "Assets/Prefab/AllJobPage.prefab";
        private const string JobDetailPrefabPath = "Assets/Prefab/SinglePanel_Detail.prefab";
        private const string FilterPanelPrefabPath = "Assets/Prefab/FilterPanels.prefab";
        private const string AnalyticsPanelPrefabPath = "Assets/Prefab/Panel_Analytics.prefab";
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;
        private const float TargetWindowWidth = 1600f;
        private const float TargetWindowHeight = 900f;

        [Test]
        public void Player_DefaultsToFixedSixteenHundredByNineHundredWindow()
        {
            Assert.That(PlayerSettings.defaultScreenWidth, Is.EqualTo((int)TargetWindowWidth));
            Assert.That(PlayerSettings.defaultScreenHeight, Is.EqualTo((int)TargetWindowHeight));
            Assert.That(PlayerSettings.fullScreenMode, Is.EqualTo(FullScreenMode.Windowed));
            Assert.That(PlayerSettings.resizableWindow, Is.False);
        }

        [Test]
        public void Canvas_ScalesUniformlyFromReferenceToTargetWindow()
        {
            WithScene(scene =>
            {
                CanvasScaler scaler = FindRoot(scene, "Canvas").GetComponent<CanvasScaler>();
                Assert.That(scaler, Is.Not.Null);
                Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
                Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(ReferenceWidth, ReferenceHeight)));
                Assert.That(scaler.screenMatchMode, Is.EqualTo(CanvasScaler.ScreenMatchMode.MatchWidthOrHeight));
                Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(0f));

                float widthScale = TargetWindowWidth / ReferenceWidth;
                float heightScale = TargetWindowHeight / ReferenceHeight;
                Assert.That(widthScale, Is.EqualTo(heightScale).Within(0.0001f));
                Assert.That(ReferenceHeight * widthScale, Is.EqualTo(TargetWindowHeight).Within(0.01f));
            });
        }

        [Test]
        public void JobsPage_ContainsExistingListAndDetailPrefabs()
        {
            WithScene(scene =>
            {
                Transform canvas = FindRoot(scene, "Canvas").transform;
                Transform appShell = RequireChild(canvas, "AppShell");
                Transform contentRoot = RequireChild(appShell, "ContentRoot");
                Transform jobsPage = RequireChild(contentRoot, "Page_Jobs");
                Transform allJobPage = RequireChild(jobsPage, "AllJobPage");
                Transform detailPage = RequireChild(jobsPage, "SinglePanel_Detail");

                Assert.That(allJobPage.gameObject.activeSelf, Is.True);
                Assert.That(detailPage.gameObject.activeSelf, Is.False);

                MonoBehaviour listController = FindComponent(allJobPage, "AllJobPage");
                var listData = new SerializedObject(listController);
                Component detailReference = listData.FindProperty("jobDetailPanel").objectReferenceValue as Component;
                Assert.That(detailReference, Is.Not.Null);
                Assert.That(detailReference.transform, Is.EqualTo(detailPage));

                MonoBehaviour detailController = FindComponent(detailPage, "Panel_JobDetail");
                var detailData = new SerializedObject(detailController);
                Assert.That(detailData.FindProperty("buttonBack").objectReferenceValue, Is.Not.Null);
            });
        }

        [Test]
        public void Navigator_SwitchesOnlyTopLevelPages()
        {
            var host = new GameObject("NavigatorHost");
            var home = new GameObject("Page_Home");
            var jobs = new GameObject("Page_Jobs");
            var resume = new GameObject("Page_Resume");
            home.transform.SetParent(host.transform);
            jobs.transform.SetParent(host.transform);
            resume.transform.SetParent(host.transform);

            try
            {
                Type navigatorType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType("AppPageNavigator", false))
                    .FirstOrDefault(type => type != null);
                Assert.That(navigatorType, Is.Not.Null);
                MonoBehaviour navigator = host.AddComponent(navigatorType) as MonoBehaviour;
                Assert.That(navigator, Is.Not.Null);

                var data = new SerializedObject(navigator);
                data.FindProperty("pageHome").objectReferenceValue = home;
                data.FindProperty("pageJobs").objectReferenceValue = jobs;
                data.FindProperty("pageResume").objectReferenceValue = resume;
                data.ApplyModifiedPropertiesWithoutUndo();

                Invoke(navigator, "ShowResume");
                Assert.That(home.activeSelf, Is.False);
                Assert.That(jobs.activeSelf, Is.False);
                Assert.That(resume.activeSelf, Is.True);

                Invoke(navigator, "ShowJobs");
                Assert.That(home.activeSelf, Is.False);
                Assert.That(jobs.activeSelf, Is.True);
                Assert.That(resume.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void AppShell_HasFinishedSidebarNavigation()
        {
            WithScene(scene =>
            {
                Transform appShell = RequireChild(FindRoot(scene, "Canvas").transform, "AppShell");
                MonoBehaviour presenter = FindComponent(appShell, "AppShellThemePresenter");
                var data = new SerializedObject(presenter);

                Assert.That(data.FindProperty("theme").objectReferenceValue, Is.Not.Null);
                Assert.That(data.FindProperty("appBackground").objectReferenceValue, Is.Not.Null);
                Assert.That(data.FindProperty("sidebarBackground").objectReferenceValue, Is.Not.Null);
                Assert.That(data.FindProperty("versionText").objectReferenceValue, Is.Not.Null);
                Assert.That(data.FindProperty("activeNavigationIndicator").objectReferenceValue, Is.Not.Null);
                Assert.That(data.FindProperty("buttonHome").objectReferenceValue, Is.Not.Null);
                Assert.That(data.FindProperty("buttonJobs").objectReferenceValue, Is.Not.Null);
                Assert.That(data.FindProperty("buttonResume").objectReferenceValue, Is.Not.Null);

                Transform sidebar = RequireChild(appShell, "Sidebar");
                Assert.That(sidebar.Find("Text_PrototypeNote"), Is.Null);
                Text version = RequireChild(sidebar, "Text_Version").GetComponent<Text>();
                Assert.That(version, Is.Not.Null);
                Assert.That(version.text, Is.EqualTo("v0.2.7"));
                Assert.That(version.gameObject.activeSelf, Is.True);
                Assert.That(RequireChild(sidebar, "Navigation_ActiveIndicator").GetComponent<Image>(),
                    Is.Not.Null);

                Assert.That(RequireChild(RequireChild(sidebar, "Button_Home"), "Text").GetComponent<Text>().text,
                    Does.StartWith("⌂"));
                Assert.That(RequireChild(RequireChild(sidebar, "Button_Jobs"), "Text").GetComponent<Text>().text,
                    Does.StartWith("▤"));
                Assert.That(RequireChild(RequireChild(sidebar, "Button_Resume"), "Text").GetComponent<Text>().text,
                    Does.StartWith("▣"));
            });
        }

        [Test]
        public void Scene_ReservesTwoHundredFortyPixelsForSidebar()
        {
            WithScene(scene =>
            {
                Transform appShell = RequireChild(FindRoot(scene, "Canvas").transform, "AppShell");
                RectTransform sidebar = RequireChild(appShell, "Sidebar") as RectTransform;
                RectTransform contentRoot = RequireChild(appShell, "ContentRoot") as RectTransform;

                Assert.That(sidebar, Is.Not.Null);
                Assert.That(contentRoot, Is.Not.Null);
                Assert.That(sidebar.sizeDelta.x, Is.EqualTo(240f));
                Assert.That(contentRoot.sizeDelta.x, Is.EqualTo(-240f));
                Assert.That(contentRoot.anchoredPosition.x, Is.EqualTo(120f));
            });
        }

        [Test]
        public void JobsPagePrimaryControls_FitInsideAvailableWidth()
        {
            GameObject page = AssetDatabase.LoadAssetAtPath<GameObject>(AllJobsPrefabPath);
            Assert.That(page, Is.Not.Null);

            float scale = TargetWindowWidth / ReferenceWidth;
            float sidebarWidth = 240f * scale;
            float contentCenter = sidebarWidth + (TargetWindowWidth - sidebarWidth) * 0.5f;

            foreach (string name in new[]
                     {
                         "Button_AddJobPosting",
                         "Button_ShowFilter",
                         "Button_Load",
                         "Button_MoreActions",
                         "Button_LastPage",
                         "Button_NextPage",
                         "TMP_Date",
                         "JobPanels"
                     })
            {
                RectTransform item = FindDescendant(page.transform, name) as RectTransform;
                Assert.That(item, Is.Not.Null, name);
                float left = item.anchoredPosition.x - item.sizeDelta.x * item.pivot.x;
                float right = item.anchoredPosition.x + item.sizeDelta.x * (1f - item.pivot.x);
                Assert.That(left, Is.GreaterThanOrEqualTo(-840f), name + " left");
                Assert.That(right, Is.LessThanOrEqualTo(840f), name + " right");

                float screenLeft = contentCenter + left * scale;
                float screenRight = contentCenter + right * scale;
                Assert.That(screenLeft, Is.GreaterThanOrEqualTo(sidebarWidth - 0.01f), name + " 1600x900 left");
                Assert.That(screenRight, Is.LessThanOrEqualTo(TargetWindowWidth + 0.01f), name + " 1600x900 right");
            }

            Transform morePanel = FindDescendant(page.transform, "Panel_MoreActions");
            Assert.That(morePanel, Is.Not.Null);
            Assert.That(morePanel.gameObject.activeSelf, Is.False,
                "Low-frequency actions should be hidden until requested.");
            foreach (string name in new[]
                     {
                         "Button_ShowAnalytics",
                         "Button_PortableTransfer",
                         "Button_TrashManagement"
                     })
            {
                Assert.That(FindDescendant(morePanel, name), Is.Not.Null, name);
            }

            GameObject detail = AssetDatabase.LoadAssetAtPath<GameObject>(JobDetailPrefabPath);
            Assert.That(detail, Is.Not.Null);
            RectTransform scrollView = detail.transform.Find("Scroll View") as RectTransform;
            Assert.That(scrollView, Is.Not.Null);
            Assert.That(scrollView.sizeDelta.x, Is.LessThanOrEqualTo(1600f));
        }

        [Test]
        public void JobsPageAndCards_HaveBoundThemeAssets()
        {
            GameObject page = AssetDatabase.LoadAssetAtPath<GameObject>(AllJobsPrefabPath);
            Assert.That(page, Is.Not.Null);
            MonoBehaviour pagePresenter = FindComponent(page.transform, "JobsPageThemePresenter");
            Assert.That(new SerializedObject(pagePresenter).FindProperty("theme").objectReferenceValue, Is.Not.Null);

            MonoBehaviour moreActions = FindComponent(page.transform, "MoreActionsMenu");
            var moreActionsData = new SerializedObject(moreActions);
            Assert.That(moreActionsData.FindProperty("buttonMoreActions").objectReferenceValue, Is.Not.Null);
            Assert.That(moreActionsData.FindProperty("panelMoreActions").objectReferenceValue, Is.Not.Null);
            Assert.That(moreActionsData.FindProperty("menuActionButtons").arraySize, Is.EqualTo(3));

            const string cardPath = "Assets/Prefab/Panel_SingleJob.prefab";
            GameObject card = AssetDatabase.LoadAssetAtPath<GameObject>(cardPath);
            Assert.That(card, Is.Not.Null);
            MonoBehaviour cardController = FindComponent(card.transform, "Panel_SingleJob");
            Assert.That(new SerializedObject(cardController).FindProperty("theme").objectReferenceValue, Is.Not.Null);
        }

        [Test]
        public void FilterPanel_StretchesToJobsPageAndKeepsActionsInsideContent()
        {
            GameObject panel = AssetDatabase.LoadAssetAtPath<GameObject>(FilterPanelPrefabPath);
            Assert.That(panel, Is.Not.Null);

            RectTransform root = panel.transform as RectTransform;
            Assert.That(root, Is.Not.Null);
            Assert.That(root.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(root.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(root.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(root.sizeDelta, Is.EqualTo(Vector2.zero));

            foreach (string name in new[] { "Button_ClearAll", "Button_AppliedFilter" })
            {
                RectTransform button = panel.transform.Find(name) as RectTransform;
                Assert.That(button, Is.Not.Null, name);
                float left = button.anchoredPosition.x - button.sizeDelta.x * button.pivot.x;
                float right = button.anchoredPosition.x + button.sizeDelta.x * (1f - button.pivot.x);
                Assert.That(left, Is.GreaterThanOrEqualTo(-840f), name + " left");
                Assert.That(right, Is.LessThanOrEqualTo(840f), name + " right");
            }
        }

        [Test]
        public void JobModals_UseOneOverlayCardAndFixedChromePattern()
        {
            GameObject jobsPage = AssetDatabase.LoadAssetAtPath<GameObject>(AllJobsPrefabPath);
            GameObject filterPanel = AssetDatabase.LoadAssetAtPath<GameObject>(FilterPanelPrefabPath);
            GameObject analyticsPanel = AssetDatabase.LoadAssetAtPath<GameObject>(AnalyticsPanelPrefabPath);
            Assert.That(jobsPage, Is.Not.Null);
            Assert.That(filterPanel, Is.Not.Null);
            Assert.That(analyticsPanel, Is.Not.Null);

            Transform[] modalRoots =
            {
                filterPanel.transform,
                FindDescendant(jobsPage.transform, "Panel_TrashManagement"),
                FindDescendant(jobsPage.transform, "Panel_PortableTransfer"),
                analyticsPanel.transform,
                FindDescendant(jobsPage.transform, "Panel_JobPostingCreate")
            };

            foreach (Transform modal in modalRoots)
            {
                Assert.That(modal, Is.Not.Null);
                Image overlay = modal.GetComponent<Image>();
                Assert.That(overlay, Is.Not.Null, modal.name + " overlay");
                Assert.That(overlay.color.a, Is.EqualTo(0.65f).Within(0.001f), modal.name + " overlay alpha");

                RectTransform card = FindDescendant(modal, "Card") as RectTransform;
                Assert.That(card, Is.Not.Null, modal.name + " card");
                Assert.That(card.sizeDelta.x, Is.LessThanOrEqualTo(1400f), modal.name + " width");
                Assert.That(card.sizeDelta.y, Is.LessThanOrEqualTo(900f), modal.name + " height");
                Assert.That(FindDescendant(modal, "Button_CloseTop"), Is.Not.Null,
                    modal.name + " top-right close");
                Assert.That(FindDescendant(modal, "Title") ?? FindDescendant(modal, "Text_Title"), Is.Not.Null,
                    modal.name + " fixed title");
            }

            Assert.That(FindDescendant(analyticsPanel.transform, "Viewport").GetComponent<ScrollRect>(), Is.Not.Null);
            Assert.That(FindDescendant(modalRoots[4], "Scroll_Form").GetComponent<ScrollRect>(), Is.Not.Null);

            RectTransform analyticsProfile = FindDescendant(analyticsPanel.transform, "Text_Profile") as RectTransform;
            RectTransform analyticsClose = FindDescendant(analyticsPanel.transform, "Button_CloseTop") as RectTransform;
            Assert.That(analyticsProfile, Is.Not.Null);
            Assert.That(analyticsClose, Is.Not.Null);
            float profileRight = analyticsProfile.anchoredPosition.x + analyticsProfile.sizeDelta.x * 0.5f;
            float closeLeft = analyticsClose.anchoredPosition.x - analyticsClose.sizeDelta.x * 0.5f;
            Assert.That(profileRight, Is.LessThanOrEqualTo(closeLeft - 24f),
                "Analytics profile label must reserve space for the top-right close button.");
        }

        [Test]
        public void ResumePage_HasBoundProfileDisplayController()
        {
            WithScene(scene =>
            {
                Transform appShell = RequireChild(FindRoot(scene, "Canvas").transform, "AppShell");
                Transform resume = RequireChild(RequireChild(appShell, "ContentRoot"), "Page_Resume");
                Transform display = RequireChild(resume, "ResumeProfileDisplay");
                MonoBehaviour controller = FindComponent(resume, "CareerProfilePage");
                var data = new SerializedObject(controller);

                Assert.That(data.FindProperty("displayText").objectReferenceValue, Is.Not.Null);
                Assert.That(data.FindProperty("displayText").objectReferenceValue, Is.EqualTo(
                    display.GetComponent<TMPro.TMP_Text>()));
                Assert.That(data.FindProperty("fontAsset").objectReferenceValue, Is.Not.Null);
                Assert.That(data.FindProperty("theme").objectReferenceValue, Is.Not.Null);
                Assert.That(controller.GetType().GetMethod("OpenEditor"), Is.Not.Null);
                Assert.That(controller.GetType().GetMethod("SaveEditor"), Is.Not.Null);
                Assert.That(controller.GetType().GetMethod("CancelEditor"), Is.Not.Null);
                Assert.That(controller.GetType().GetMethod("OpenTransfer"), Is.Not.Null);
                Assert.That(controller.GetType().GetMethod("CloseTransfer"), Is.Not.Null);
                Assert.That(controller.GetType().GetMethod("ChooseExportPath"), Is.Not.Null);
                Assert.That(controller.GetType().GetMethod("ChooseImportFile"), Is.Not.Null);
            });
        }

        [Test]
        public void HomePage_HasBoundDashboardController()
        {
            WithScene(scene =>
            {
                Transform appShell = RequireChild(FindRoot(scene, "Canvas").transform, "AppShell");
                Transform home = RequireChild(RequireChild(appShell, "ContentRoot"), "Page_Home");
                MonoBehaviour controller = FindComponent(home, "HomeDashboardPage");
                var data = new SerializedObject(controller);

                Assert.That(data.FindProperty("theme").objectReferenceValue, Is.Not.Null);
                Assert.That(data.FindProperty("fontAsset").objectReferenceValue, Is.Not.Null);
                Assert.That(data.FindProperty("placeholderRoot").objectReferenceValue,
                    Is.EqualTo(home.Find("Placeholder_Home").gameObject));
                Assert.That(controller.GetType().GetMethod("Refresh"), Is.Not.Null);
                Assert.That(controller.GetType().GetMethod("OpenAddJob"), Is.Not.Null);
                Assert.That(controller.GetType().GetMethod("OpenJobs"), Is.Not.Null);
                Assert.That(controller.GetType().GetMethod("OpenAnalytics"), Is.Not.Null);
                Assert.That(controller.GetType().GetMethod("OpenResumeEditor"), Is.Not.Null);
            });
        }

        [Test]
        public void HomeDashboard_BuildsMetricsStatusAndQuickActions()
        {
            Type controllerType = Type.GetType("HomeDashboardPage, Assembly-CSharp");
            Assert.That(controllerType, Is.Not.Null);
            var root = new GameObject("Page_Home", typeof(RectTransform));
            root.SetActive(false);
            try
            {
                root.GetComponent<RectTransform>().sizeDelta = new Vector2(1360f, 900f);
                var placeholder = new GameObject(
                    "Placeholder_Home", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                placeholder.transform.SetParent(root.transform, false);
                var controller = (MonoBehaviour)root.AddComponent(controllerType);
                var data = new SerializedObject(controller);
                data.FindProperty("fontAsset").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(
                        "Assets/Font/Yozai-Light SDF.asset");
                data.FindProperty("theme").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                        "Assets/JobCheckUiTheme.asset");
                data.FindProperty("placeholderRoot").objectReferenceValue = placeholder;
                data.ApplyModifiedPropertiesWithoutUndo();

                MethodInfo ensure = controllerType.GetMethod("EnsureUi",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(ensure, Is.Not.Null);
                ensure.Invoke(controller, null);

                Transform dashboard = root.transform.Find("HomeDashboard");
                Assert.That(dashboard, Is.Not.Null);
                Assert.That(placeholder.activeSelf, Is.False);
                Assert.That(FindDescendant(dashboard, "DashboardScroll")
                    .GetComponent<ScrollRect>(), Is.Not.Null);
                Assert.That(FindDescendant(dashboard, "MetricRow"), Is.Not.Null);
                Assert.That(FindDescendant(dashboard, "Metric_Jobs"), Is.Not.Null);
                Assert.That(FindDescendant(dashboard, "Metric_Active"), Is.Not.Null);
                Assert.That(FindDescendant(dashboard, "Metric_Review"), Is.Not.Null);
                Assert.That(FindDescendant(dashboard, "Metric_NoResponse"), Is.Not.Null);
                Assert.That(FindDescendant(dashboard, "Card_Status"), Is.Not.Null);
                Assert.That(FindDescendant(dashboard, "Card_Attention"), Is.Not.Null);
                Assert.That(FindDescendant(dashboard, "Card_Profile"), Is.Not.Null);
                Assert.That(FindDescendant(dashboard, "Button_AddJob"), Is.Not.Null);
                Assert.That(FindDescendant(dashboard, "Button_ViewJobs"), Is.Not.Null);
                Assert.That(FindDescendant(dashboard, "Button_Analytics"), Is.Not.Null);
                Assert.That(FindDescendant(dashboard, "Button_EditResume"), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SharedColumnsLayout_UsesFortySixtySplitAndTallestColumnHeight()
        {
            Type layoutType = Type.GetType("CareerProfileColumnsLayout, Assembly-CSharp");
            Assert.That(layoutType, Is.Not.Null);
            var root = new GameObject("SharedColumns", typeof(RectTransform), layoutType);
            var left = new GameObject("Left", typeof(RectTransform), typeof(LayoutElement));
            var right = new GameObject("Right", typeof(RectTransform), typeof(LayoutElement));
            try
            {
                RectTransform rootRect = root.GetComponent<RectTransform>();
                rootRect.sizeDelta = new Vector2(1200f, 0f);
                left.transform.SetParent(root.transform, false);
                right.transform.SetParent(root.transform, false);
                left.GetComponent<LayoutElement>().preferredHeight = 240f;
                right.GetComponent<LayoutElement>().preferredHeight = 420f;

                var layout = (LayoutGroup)root.GetComponent(layoutType);
                layoutType.GetMethod("Configure").Invoke(layout, new object[] { 0.4f, 20f });
                layout.CalculateLayoutInputHorizontal();
                layout.SetLayoutHorizontal();
                layout.CalculateLayoutInputVertical();
                layout.SetLayoutVertical();

                Assert.That(left.GetComponent<RectTransform>().rect.width,
                    Is.EqualTo(472f).Within(0.1f));
                Assert.That(right.GetComponent<RectTransform>().rect.width,
                    Is.EqualTo(708f).Within(0.1f));
                Assert.That(layout.preferredHeight, Is.EqualTo(420f).Within(0.1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ResumeDisplay_BuildsSingleColumnCards()
        {
            Type controllerType = Type.GetType("CareerProfilePage, Assembly-CSharp");
            Assert.That(controllerType, Is.Not.Null);
            var root = new GameObject("Page_Resume", typeof(RectTransform));
            root.SetActive(false);
            try
            {
                var display = new GameObject("ResumeProfileDisplay", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image), typeof(TMPro.TextMeshProUGUI));
                display.transform.SetParent(root.transform, false);
                var controller = (MonoBehaviour)root.AddComponent(controllerType);
                var data = new SerializedObject(controller);
                data.FindProperty("fontAsset").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(
                        "Assets/Font/Yozai-Light SDF.asset");
                data.FindProperty("displayText").objectReferenceValue =
                    display.GetComponent<TMPro.TMP_Text>();
                data.ApplyModifiedPropertiesWithoutUndo();

                controllerType.GetMethod("EnsureDisplayUi",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);

                Transform content = FindDescendant(display.transform, "Content");
                Assert.That(content, Is.Not.Null);
                Assert.That(FindDescendant(display.transform, "DisplayColumns"), Is.Null);
                string[] cards =
                {
                    "Card_summary", "Card_skills", "Card_links", "Card_languages",
                    "Card_experiences", "Card_projects", "Card_educations"
                };
                Assert.That(content.childCount, Is.EqualTo(cards.Length));
                for (int index = 0; index < cards.Length; index++)
                    Assert.That(content.GetChild(index).name, Is.EqualTo(cards[index]));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ResumeEditor_BuildsSummarySkillLinkAndLanguageFormsInSingleColumn()
        {
            Type controllerType = Type.GetType("CareerProfilePage, Assembly-CSharp");
            Assert.That(controllerType, Is.Not.Null);
            var root = new GameObject("Page_Resume", typeof(RectTransform));
            root.SetActive(false);
            try
            {
                root.GetComponent<RectTransform>().sizeDelta = new Vector2(1360f, 900f);
                var display = new GameObject("ResumeProfileDisplay", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image), typeof(TMPro.TextMeshProUGUI));
                display.transform.SetParent(root.transform, false);
                var controller = (MonoBehaviour)root.AddComponent(controllerType);
                var data = new SerializedObject(controller);
                data.FindProperty("fontAsset").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(
                        "Assets/Font/Yozai-Light SDF.asset");
                data.FindProperty("displayText").objectReferenceValue =
                    display.GetComponent<TMPro.TMP_Text>();
                data.FindProperty("theme").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                        "Assets/JobCheckUiTheme.asset");
                data.ApplyModifiedPropertiesWithoutUndo();

                MethodInfo ensure = controllerType.GetMethod("EnsureEditorUi",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(ensure, Is.Not.Null);
                ensure.Invoke(controller, null);

                Transform editor = root.transform.Find("CareerProfileEditor");
                Assert.That(editor, Is.Not.Null);
                Assert.That(FindDescendant(editor, "EditorScroll").GetComponent<ScrollRect>(),
                    Is.Not.Null);
                Transform content = FindDescendant(editor, "Content");
                Assert.That(content, Is.Not.Null);
                Assert.That(content.childCount, Is.EqualTo(7));
                Assert.That(content.GetChild(0).name, Is.EqualTo("Field_summary"));
                Assert.That(content.GetChild(1).name, Is.EqualTo("Field_skills"));
                Assert.That(content.GetChild(2).name, Is.EqualTo("Field_links"));
                Assert.That(content.GetChild(3).name, Is.EqualTo("Field_experiences"));
                Assert.That(content.GetChild(4).name, Is.EqualTo("Field_projects"));
                Assert.That(content.GetChild(5).name, Is.EqualTo("Field_educations"));
                Assert.That(content.GetChild(6).name, Is.EqualTo("Field_languages"));
                Assert.That(FindDescendant(editor, "EditorColumns"), Is.Null);
                Assert.That(FindDescendant(editor, "ExperienceList"), Is.Not.Null);
                Assert.That(FindDescendant(editor, "Button_AddExperience"), Is.Not.Null);
                Transform experienceForm = FindDescendant(editor, "ExperienceForm");
                Assert.That(experienceForm, Is.Not.Null);
                Assert.That(experienceForm.gameObject.activeSelf, Is.False);
                Assert.That(FindDescendant(experienceForm, "Input_ExperienceOrganization"),
                    Is.Not.Null);
                Assert.That(FindDescendant(experienceForm, "Input_ExperienceRole"),
                    Is.Not.Null);
                Assert.That(FindDescendant(experienceForm, "Input_ExperienceStartYear"),
                    Is.Not.Null);
                Assert.That(FindDescendant(experienceForm, "Dropdown_ExperienceStartMonth"),
                    Is.Not.Null);
                Assert.That(FindDescendant(experienceForm, "CurrentEmployment")
                    .GetComponent<Toggle>(), Is.Not.Null);
                Assert.That(FindDescendant(experienceForm, "Input_ExperienceEndYear"),
                    Is.Not.Null);
                Assert.That(FindDescendant(experienceForm, "Dropdown_ExperienceEndMonth"),
                    Is.Not.Null);
                Assert.That(FindDescendant(experienceForm, "ExperienceSkillChoices"),
                    Is.Not.Null);
                Assert.That(FindDescendant(experienceForm, "Button_SaveExperience"),
                    Is.Not.Null);
                Assert.That(FindDescendant(editor, "ProjectList"), Is.Not.Null);
                Assert.That(FindDescendant(editor, "Button_AddProject"), Is.Not.Null);
                Transform projectForm = FindDescendant(editor, "ProjectForm");
                Assert.That(projectForm, Is.Not.Null);
                Assert.That(projectForm.gameObject.activeSelf, Is.False);
                Assert.That(FindDescendant(projectForm, "Input_ProjectName"), Is.Not.Null);
                Assert.That(FindDescendant(projectForm, "Input_ProjectDescription"),
                    Is.Not.Null);
                Assert.That(FindDescendant(projectForm, "ProjectSkillChoices"),
                    Is.Not.Null);
                Assert.That(FindDescendant(projectForm, "Input_ProjectUrl"), Is.Not.Null);
                Assert.That(FindDescendant(projectForm, "Button_SaveProject"), Is.Not.Null);
                Assert.That(FindDescendant(editor, "EducationList"), Is.Not.Null);
                Assert.That(FindDescendant(editor, "Button_AddEducation"), Is.Not.Null);
                Transform educationForm = FindDescendant(editor, "EducationForm");
                Assert.That(educationForm, Is.Not.Null);
                Assert.That(educationForm.gameObject.activeSelf, Is.False);
                Assert.That(FindDescendant(educationForm, "Input_EducationInstitution"), Is.Not.Null);
                Assert.That(FindDescendant(educationForm, "Input_EducationProgram"), Is.Not.Null);
                Assert.That(FindDescendant(educationForm, "Dropdown_EducationDegree"), Is.Not.Null);
                Assert.That(FindDescendant(educationForm, "Dropdown_EducationStatus"), Is.Not.Null);
                Assert.That(FindDescendant(educationForm, "Input_EducationStartYear"), Is.Not.Null);
                Assert.That(FindDescendant(educationForm, "Dropdown_EducationStartMonth"), Is.Not.Null);
                Assert.That(FindDescendant(educationForm, "Input_EducationEndYear"), Is.Not.Null);
                Assert.That(FindDescendant(educationForm, "Dropdown_EducationEndMonth"), Is.Not.Null);
                Assert.That(FindDescendant(educationForm, "Input_EducationFieldTags"), Is.Not.Null);
                Assert.That(FindDescendant(educationForm, "Button_SaveEducation"), Is.Not.Null);
                Assert.That(FindDescendant(editor, "LanguageList"), Is.Not.Null);
                Assert.That(FindDescendant(editor, "Button_AddLanguage"), Is.Not.Null);
                Transform languageForm = FindDescendant(editor, "LanguageForm");
                Assert.That(languageForm, Is.Not.Null);
                Assert.That(languageForm.gameObject.activeSelf, Is.False);
                Assert.That(FindDescendant(languageForm, "Button_SaveLanguage"), Is.Not.Null);
                var languages = FindDescendant(languageForm, "Dropdown_LanguageName")
                    .GetComponent<TMPro.TMP_Dropdown>();
                var levels = FindDescendant(languageForm, "Dropdown_LanguageLevel")
                    .GetComponent<TMPro.TMP_Dropdown>();
                Assert.That(languages.template.parent, Is.SameAs(editor));
                Assert.That(levels.template.parent, Is.SameAs(editor));
                Assert.That(languages.template.pivot.y, Is.EqualTo(0f),
                    "語言選單應向上展開，避開編輯區頁尾");
                Assert.That(levels.template.pivot.y, Is.EqualTo(0f),
                    "程度選單應向上展開，避開編輯區頁尾");
                Assert.That(languages.template.Find("Viewport/Content")
                    .GetComponent<RectTransform>().sizeDelta.y, Is.EqualTo(48f),
                    "TMP 清單內容初始高度必須包含樣板列，否則最後一項會被裁掉");
                Assert.That(levels.template.Find("Viewport/Content")
                    .GetComponent<RectTransform>().sizeDelta.y, Is.EqualTo(48f));
                Assert.That(languages.GetType().Name, Is.EqualTo("JobCheckPopupDropdown"));
                Assert.That(languages.options.Select(item => item.text),
                    Is.EqualTo(new[] { "請選擇語言", "中文", "英文", "日文" }));
                Assert.That(levels.options.Select(item => item.text),
                    Is.EqualTo(new[] { "請選擇程度", "不會", "略懂", "中等", "精通" }));
                Assert.That(FindDescendant(editor, "LinkList"), Is.Not.Null);
                Assert.That(FindDescendant(editor, "Button_AddLink"), Is.Not.Null);
                Transform linkForm = FindDescendant(editor, "LinkForm");
                Assert.That(linkForm, Is.Not.Null);
                Assert.That(linkForm.gameObject.activeSelf, Is.False);
                Assert.That(FindDescendant(linkForm, "Input_LinkLabel"), Is.Not.Null);
                Assert.That(FindDescendant(linkForm, "Input_LinkUrl"), Is.Not.Null);
                Assert.That(FindDescendant(linkForm, "Button_SaveLink"), Is.Not.Null);
                Assert.That(FindDescendant(editor, "SkillList"), Is.Not.Null);
                Assert.That(FindDescendant(editor, "Button_AddSkill"), Is.Not.Null);
                Transform skillForm = FindDescendant(editor, "SkillForm");
                Assert.That(skillForm, Is.Not.Null);
                Assert.That(skillForm.gameObject.activeSelf, Is.False);
                Assert.That(FindDescendant(skillForm, "Input_Name"), Is.Not.Null);
                Assert.That(FindDescendant(skillForm, "Dropdown_Suggestion"), Is.Null);
                Assert.That(FindDescendant(skillForm, "Dropdown_Years"), Is.Not.Null);
                Assert.That(FindDescendant(skillForm, "Dropdown_Months"), Is.Not.Null);
                Assert.That(FindDescendant(skillForm, "Input_Notes"), Is.Not.Null);
                Assert.That(FindDescendant(skillForm, "Dropdown_Level"), Is.Null);
                var years = FindDescendant(skillForm, "Dropdown_Years")
                    .GetComponent<TMPro.TMP_Dropdown>();
                Assert.That(years.template, Is.Not.Null);
                Assert.That(years.template.parent, Is.SameAs(editor));
                Assert.That(years.template.GetComponentInChildren<Toggle>(true), Is.Not.Null);
                Assert.That(years.options[0].text, Is.EqualTo("未填寫"));
                Assert.That(editor.Find("Footer"), Is.Not.Null);
                Assert.That(FindDescendant(editor, "Button_Save"), Is.Not.Null);
                Assert.That(FindDescendant(editor, "Button_Cancel"), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ResumeEditableDrafts_PreserveOtherSectionsAndExistingData()
        {
            Type controllerType = Type.GetType("CareerProfilePage, Assembly-CSharp");
            MethodInfo create = controllerType.GetMethod("CreateSummaryEditCandidate",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(create, Is.Not.Null);
            var profile = new JobCheck.Domain.CareerProfile
            {
                Id = "profile_1",
                Summary = "原本介紹",
                Links = new System.Collections.Generic.List<JobCheck.Domain.CareerProfileLink>
                    { new JobCheck.Domain.CareerProfileLink
                        { Id = "link_1", Label = "GitHub", Url = "https://github.com" } },
                Skills = new System.Collections.Generic.List<JobCheck.Domain.CareerSkill>
                    { new JobCheck.Domain.CareerSkill
                        { Id = "skill_1", Name = "Unity",
                          Level = JobCheck.Domain.SkillLevel.Basic,
                          ClaimedMonths = 24 } },
                Experiences = new System.Collections.Generic.List<JobCheck.Domain.CareerExperience>
                    { new JobCheck.Domain.CareerExperience { Id = "experience_1" } },
                Projects = new System.Collections.Generic.List<JobCheck.Domain.CareerProject>
                    { new JobCheck.Domain.CareerProject
                        { Id = "project_1", Name = "原專案",
                          Technologies = new System.Collections.Generic.List<string> { "Unity" } } },
                Educations = new System.Collections.Generic.List<JobCheck.Domain.CareerEducation>
                    { new JobCheck.Domain.CareerEducation { Id = "education_1",
                        Institution = "原學校",
                        FieldTags = new System.Collections.Generic.List<string> { "資訊" } } },
                Languages = new System.Collections.Generic.List<JobCheck.Domain.CareerLanguage>
                    { new JobCheck.Domain.CareerLanguage
                        { Id = "language_1", Name = "英文", LanguageId = "en",
                          Proficiency = JobCheck.Domain.LanguageProficiency.Elementary,
                          Notes = "舊備註",
                          Certifications = new System.Collections.Generic.List<string> { "TOEIC" } } }
            };
            var now = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.FromHours(8));
            MethodInfo clone = controllerType.GetMethod("CloneSkills",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(clone, Is.Not.Null);
            var skillDraft = (System.Collections.Generic.List<JobCheck.Domain.CareerSkill>)
                clone.Invoke(null, new object[] { profile.Skills });
            skillDraft[0].Name = "C#";
            MethodInfo cloneLinks = controllerType.GetMethod("CloneLinks",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(cloneLinks, Is.Not.Null);
            var linkDraft = (System.Collections.Generic.List<JobCheck.Domain.CareerProfileLink>)
                cloneLinks.Invoke(null, new object[] { profile.Links });
            linkDraft[0].Label = "作品集";
            MethodInfo cloneLanguages = controllerType.GetMethod("CloneLanguages",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(cloneLanguages, Is.Not.Null);
            var languageDraft = (System.Collections.Generic.List<JobCheck.Domain.CareerLanguage>)
                cloneLanguages.Invoke(null, new object[] { profile.Languages });
            languageDraft[0].Name = "日文";
            languageDraft[0].Certifications.Add("JLPT");

            MethodInfo cloneExperiences = controllerType.GetMethod("CloneExperiences",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(cloneExperiences, Is.Not.Null);
            var experienceDraft =
                (System.Collections.Generic.List<JobCheck.Domain.CareerExperience>)
                    cloneExperiences.Invoke(null, new object[] { profile.Experiences });
            experienceDraft[0].Organization = "新公司";
            MethodInfo cloneProjects = controllerType.GetMethod("CloneProjects",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(cloneProjects, Is.Not.Null);
            var projectDraft =
                (System.Collections.Generic.List<JobCheck.Domain.CareerProject>)
                    cloneProjects.Invoke(null, new object[] { profile.Projects });
            projectDraft[0].Name = "新專案";
            projectDraft[0].Technologies.Add("C#");
            MethodInfo cloneEducations = controllerType.GetMethod("CloneEducations",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(cloneEducations, Is.Not.Null);
            var educationDraft =
                (System.Collections.Generic.List<JobCheck.Domain.CareerEducation>)
                    cloneEducations.Invoke(null, new object[] { profile.Educations });
            educationDraft[0].Institution = "新學校";
            educationDraft[0].FieldTags.Add("工程");

            var candidate = (JobCheck.Domain.CareerProfile)create.Invoke(
                null, new object[]
                    { profile, "新的介紹", linkDraft, skillDraft, experienceDraft,
                        projectDraft, educationDraft, languageDraft, now });

            Assert.That(candidate.Id, Is.EqualTo(profile.Id));
            Assert.That(candidate.Summary, Is.EqualTo("新的介紹"));
            Assert.That(candidate.UpdatedAt, Is.EqualTo(now));
            Assert.That(profile.Links[0].Label, Is.EqualTo("GitHub"));
            Assert.That(candidate.Links, Is.Not.SameAs(profile.Links));
            Assert.That(candidate.Links[0].Label, Is.EqualTo("作品集"));
            Assert.That(candidate.Links[0].Id, Is.EqualTo("link_1"));
            Assert.That(candidate.Links[0].Url, Is.EqualTo("https://github.com"));
            Assert.That(profile.Skills[0].Name, Is.EqualTo("Unity"));
            Assert.That(candidate.Skills, Is.Not.SameAs(profile.Skills));
            Assert.That(candidate.Skills[0].Name, Is.EqualTo("C#"));
            Assert.That(candidate.Skills[0].Id, Is.EqualTo("skill_1"));
            Assert.That(candidate.Skills[0].Level,
                Is.EqualTo(JobCheck.Domain.SkillLevel.Basic));
            Assert.That(candidate.Skills[0].ClaimedMonths, Is.EqualTo(24));
            Assert.That(profile.Experiences[0].Organization, Is.Null);
            Assert.That(candidate.Experiences, Is.Not.SameAs(profile.Experiences));
            Assert.That(candidate.Experiences[0].Organization, Is.EqualTo("新公司"));
            Assert.That(candidate.Experiences[0].Id, Is.EqualTo("experience_1"));
            Assert.That(profile.Projects[0].Name, Is.EqualTo("原專案"));
            Assert.That(profile.Projects[0].Technologies, Is.EqualTo(new[] { "Unity" }));
            Assert.That(candidate.Projects, Is.Not.SameAs(profile.Projects));
            Assert.That(candidate.Projects[0].Name, Is.EqualTo("新專案"));
            Assert.That(candidate.Projects[0].Id, Is.EqualTo("project_1"));
            Assert.That(candidate.Projects[0].Technologies,
                Is.EqualTo(new[] { "Unity", "C#" }));
            Assert.That(profile.Educations[0].Institution, Is.EqualTo("原學校"));
            Assert.That(profile.Educations[0].FieldTags, Is.EqualTo(new[] { "資訊" }));
            Assert.That(candidate.Educations, Is.Not.SameAs(profile.Educations));
            Assert.That(candidate.Educations[0].Institution, Is.EqualTo("新學校"));
            Assert.That(candidate.Educations[0].Id, Is.EqualTo("education_1"));
            Assert.That(candidate.Educations[0].FieldTags, Is.EqualTo(new[] { "資訊", "工程" }));
            Assert.That(profile.Languages[0].Name, Is.EqualTo("英文"));
            Assert.That(profile.Languages[0].Certifications, Is.EqualTo(new[] { "TOEIC" }));
            Assert.That(candidate.Languages, Is.Not.SameAs(profile.Languages));
            Assert.That(candidate.Languages[0].Name, Is.EqualTo("日文"));
            Assert.That(candidate.Languages[0].Id, Is.EqualTo("language_1"));
            Assert.That(candidate.Languages[0].Notes, Is.EqualTo("舊備註"));
            Assert.That(candidate.Languages[0].Proficiency,
                Is.EqualTo(JobCheck.Domain.LanguageProficiency.Elementary));
        }

        [TestCase("https://example.com", true)]
        [TestCase("http://example.com/project", true)]
        [TestCase("", false)]
        [TestCase("   ", false)]
        [TestCase("example.com", true)]
        [TestCase("file:///C:/private.txt", true)]
        [TestCase(@"C:\portfolio\demo", true)]
        public void ResumeLinkEditor_AcceptsAnyNonEmptyAddress(string url, bool expected)
        {
            Type controllerType = Type.GetType("CareerProfilePage, Assembly-CSharp");
            Assert.That(controllerType, Is.Not.Null);
            MethodInfo validate = controllerType.GetMethod("HasLinkValue",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(validate, Is.Not.Null);
            Assert.That(validate.Invoke(null, new object[] { url }), Is.EqualTo(expected));
        }

        [Test]
        public void ResumeLanguageDisplay_UsesFourVisibleLevels()
        {
            Type controllerType = Type.GetType("CareerProfilePage, Assembly-CSharp");
            Assert.That(controllerType, Is.Not.Null);
            MethodInfo format = controllerType.GetMethod("FormatProfile",
                BindingFlags.Static | BindingFlags.Public);
            var profile = new JobCheck.Domain.CareerProfile
            {
                Languages = new System.Collections.Generic.List<JobCheck.Domain.CareerLanguage>
                {
                    new JobCheck.Domain.CareerLanguage
                        { Name = "中文", Proficiency = JobCheck.Domain.LanguageProficiency.None },
                    new JobCheck.Domain.CareerLanguage
                        { Name = "英文", Proficiency = JobCheck.Domain.LanguageProficiency.Elementary },
                    new JobCheck.Domain.CareerLanguage
                        { Name = "日文", Proficiency = JobCheck.Domain.LanguageProficiency.UpperIntermediate }
                }
            };

            string output = (string)format.Invoke(null, new object[] { profile });
            Assert.That(output, Does.Contain("中文｜不會"));
            Assert.That(output, Does.Contain("英文｜略懂"));
            Assert.That(output, Does.Contain("日文｜中等"));
            Assert.That(output, Does.Not.Contain("等級 4"));
        }

        [Test]
        public void ResumeSkillDisplay_DoesNotShowLegacyLevel()
        {
            Type controllerType = Type.GetType("CareerProfilePage, Assembly-CSharp");
            Assert.That(controllerType, Is.Not.Null);
            MethodInfo format = controllerType.GetMethod("FormatProfile",
                BindingFlags.Static | BindingFlags.Public);
            Assert.That(format, Is.Not.Null);
            var profile = new JobCheck.Domain.CareerProfile
            {
                Skills = new System.Collections.Generic.List<JobCheck.Domain.CareerSkill>
                {
                    new JobCheck.Domain.CareerSkill
                    {
                        Name = "Unity", Level = JobCheck.Domain.SkillLevel.Basic,
                        ClaimedMonths = 24
                    }
                }
            };

            string output = (string)format.Invoke(null, new object[] { profile });
            Assert.That(output, Does.Contain("Unity"));
            Assert.That(output, Does.Contain("使用 2 年"));
            Assert.That(output, Does.Not.Contain("等級"));
        }

        [Test]
        public void ResumeTransfer_BuildsStandardScrollableModalWithDangerousReplaceAction()
        {
            Type controllerType = Type.GetType("CareerProfilePage, Assembly-CSharp");
            Assert.That(controllerType, Is.Not.Null);
            var root = new GameObject("Page_Resume", typeof(RectTransform));
            root.SetActive(false);
            try
            {
                root.GetComponent<RectTransform>().sizeDelta = new Vector2(1360f, 900f);
                var display = new GameObject("ResumeProfileDisplay", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image), typeof(TMPro.TextMeshProUGUI));
                display.transform.SetParent(root.transform, false);
                var controller = (MonoBehaviour)root.AddComponent(controllerType);
                var data = new SerializedObject(controller);
                data.FindProperty("fontAsset").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(
                        "Assets/Font/Yozai-Light SDF.asset");
                data.FindProperty("displayText").objectReferenceValue =
                    display.GetComponent<TMPro.TMP_Text>();
                ScriptableObject theme = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    "Assets/JobCheckUiTheme.asset");
                data.FindProperty("theme").objectReferenceValue = theme;
                data.ApplyModifiedPropertiesWithoutUndo();

                MethodInfo ensure = controllerType.GetMethod("EnsureTransferUi",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(ensure, Is.Not.Null);
                ensure.Invoke(controller, null);

                Transform modal = root.transform.Find("CareerProfileTransfer");
                Assert.That(modal, Is.Not.Null);
                Transform card = modal.Find("Card");
                Assert.That(card, Is.Not.Null);
                Assert.That(card.GetComponent(Type.GetType(
                    "JobCheckModalCardSizer, Assembly-CSharp")), Is.Not.Null);
                Assert.That(FindDescendant(card, "Button_CloseTop"), Is.Not.Null);
                Assert.That(FindDescendant(card, "MessageViewport").GetComponent<ScrollRect>(),
                    Is.Not.Null);
                Assert.That(card.Find("Footer"), Is.Not.Null);
                Assert.That(FindDescendant(card, "Button_Import"), Is.Not.Null);
                Button replace = FindDescendant(card, "Button_Replace").GetComponent<Button>();
                var themeData = new SerializedObject(theme);
                Assert.That(replace.colors.normalColor,
                    Is.EqualTo(themeData.FindProperty("danger").colorValue));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void WithScene(Action<Scene> assertion)
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedForTest = !scene.IsValid() || !scene.isLoaded;
            if (openedForTest)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }

            try
            {
                assertion(scene);
            }
            finally
            {
                if (openedForTest)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            GameObject root = scene.GetRootGameObjects().FirstOrDefault(item => item.name == name);
            Assert.That(root, Is.Not.Null, "Missing root: " + name);
            return root;
        }

        private static Transform RequireChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            Assert.That(child, Is.Not.Null, "Missing child: " + parent.name + "/" + name);
            return child;
        }

        private static MonoBehaviour FindComponent(Transform root, string typeName)
        {
            MonoBehaviour component = root.GetComponents<MonoBehaviour>()
                .FirstOrDefault(item => item != null && item.GetType().Name == typeName);
            Assert.That(component, Is.Not.Null, "Missing component: " + typeName);
            return component;
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(item => item.name == name);
        }

        private static void Invoke(MonoBehaviour target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, "Missing method: " + methodName);
            method.Invoke(target, arguments);
        }
    }
}
