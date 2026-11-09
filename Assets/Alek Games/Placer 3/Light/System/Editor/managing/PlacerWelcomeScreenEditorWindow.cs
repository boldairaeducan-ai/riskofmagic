using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

using UnityEngine;

using UnityEditor;

using AlekGames.Placer3.Shared;

namespace AlekGames.Placer3.Editor
{
    [InitializeOnLoad]
    public class PlacerWelcomeScreenEditorWindow : EditorWindow
    {
        private static bool welcomeOpening;
        private static bool addOpening;
        public static async void checkOpen()
        {
            placerProVersionDetection.Refresh();

            PlacerDeafultsData data = PlacerDeafultsData.getDataInProject(true);
            if (data == null)
            {
                await Task.Yield();
                await Task.Yield();
                data = PlacerDeafultsData.getDataInProject();
            }

            if (data != null && !data.projectOpened) // first project refresh since last quit
            {
                bool allowAd = true;
                if (data.showWelcomeScreen)
                {
                    openWelcomeSoon();
                    allowAd = false; 
                }

                if(allowAd)
                {
                    if (Mathf.Abs(data.applicationClosesSinceAd) >= 10)
                    {
                        data.applicationClosesSinceAd = 0;
                        if (!placerProVersionDetection.isProVersion)
                        {
                            openAdSoon();
                        }
                    }
                    //else Debug.Log("no ad " + data.applicationClosesSinceAd);
                }
                data.projectOpened = true;
                EditorUtility.SetDirty(data);
            }
        }

        static PlacerWelcomeScreenEditorWindow()
        {
            checkOpen();
            EditorApplication.quitting += Quit;
        }

        static void Quit()
        {
            PlacerDeafultsData data = PlacerDeafultsData.getDataInProject();
            data.projectOpened = false;
            data.applicationClosesSinceAd++;
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
        }

        private static async void openWelcomeSoon()
        {
            await Task.Yield();
            await Task.Delay(200); //wait for stuff to update. no idea how long should i wait, so i put some randome values. seems to work tho
            await Task.Yield();

            OpenWelcome();
        }
        private static async void openAdSoon()
        {
            await Task.Yield();
            await Task.Delay(200); //wait for stuff to update. no idea how long should i wait, so i put some randome values. seems to work tho
            await Task.Yield();

            OpenAd();
        }

        [MenuItem("Window/Alek Games/" + PlacerDeafultsData.productName + "/Placer Pro Ad")]
        public static void OpenAd()
        {
            EditorWindow w = GetWindow<PlacerProReccomendWindow>(PlacerDeafultsData.productName + " Pro");
            w.position = new Rect(w.position.x, w.position.y, 600, 250);
        }


        [MenuItem("Window/Alek Games/" + PlacerDeafultsData.productName + "/Welcome Screen")]
        public static void OpenWelcome()
        {
            EditorWindow w = GetWindow<PlacerWelcomeScreenEditorWindow>(PlacerDeafultsData.productName + " Welcome Screen");
            w.position = new Rect(w.position.x, w.position.y, 900, 350);
        }

        private void OnEnable()
        {
            init();
        }

        private PlacerDeafultsData deafults;

        public void init()
        {
            deafults = PlacerDeafultsData.getDataInProject();
            deafults.showWelcomeScreen = false;
            EditorUtility.SetDirty(deafults);
        }

        private void OnGUI()
        {
            using (EditorGUILayout.HorizontalScope l = new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("--- Welcome To " + PlacerDeafultsData.productName + " ---", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
            }

            GUILayout.Space(10);

            GUILayout.Label("Hi there fellow developer. thanks for getting " + PlacerDeafultsData.productName + ". hope you like it!\n", EditorStyles.boldLabel);
            GUILayout.Label("A new world of level designing has just opened in front of you.\nLet the power of " + PlacerDeafultsData.productName + " amaze you!\nhave fun :)", EditorStyles.boldLabel);

            GUILayout.Space(20);

            GUILayout.Label("to get started i advise to read the doc's");
            if (GUILayout.Button("Select Doc's")) SaveStuffSystem.selectObjectOnPath(PlacerEditorHelper.docksFilePath);

            GUILayout.Space(10);
            if (placerProVersionDetection.isProVersion)
            {
                GUILayout.Label("It has been detected that you are using the PRO version of " + PlacerDeafultsData.productName + ". Thanks a lot for getting it, hope you like it!");
                GUILayout.Label("you can open the Placer manager with the shortcut:\nLShift + LAlt + P.\nyou will find all the " + PlacerDeafultsData.productName + " features linked to buttons there");
            }
            else GUILayout.Label("It has been detected that you are using the light version of placer.\n" +
                "since it is free you could you consider leaving a review or if you like it, buy the PRO version of " + PlacerDeafultsData.productName + ", to help with development?\n" +
                "thanks in advance, there will be a system reminder about the better version once in a while");

            GUILayout.Space(10);
            GUILayout.Label("for help with the asset, go to my discord channel, i am almost always there :)");
            if (GUILayout.Button("Go to Alek Games Discord Channel")) Application.OpenURL("https://discord.gg/erzHBcx3ES");

            GUILayout.Space(10);
            GUILayout.Label("And once you test out the asset remembre to leave a review :). it really helps", EditorStyles.boldLabel);
            

            GUILayout.Space(20);
            bool before = deafults.showWelcomeScreen;
            deafults.showWelcomeScreen = EditorGUILayout.Toggle("show again", deafults.showWelcomeScreen);
            if(deafults.showWelcomeScreen != before) EditorUtility.SetDirty(deafults);
        }
    }
}