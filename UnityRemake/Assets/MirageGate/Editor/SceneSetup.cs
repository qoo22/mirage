using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MirageGate;
using MirageGate.Systems;
using MirageGate.View;

namespace MirageGate.EditorTools
{
    /// <summary>
    /// 現在のシーンにプレイ可能な最小構成を自動構築する（手作業の結線不要）。
    /// メニュー "MirageGate ▸ Setup Play Scene"。実行後にPlayするとデバッグ出撃が始まる。
    /// 前提：先に "Import ALL" でGameDatabase.assetを作成しておくこと。
    /// </summary>
    public static class SceneSetup
    {
        const string DbPath = "Assets/MirageGate/ScriptableObjects/GameDatabase.asset";

        [MenuItem("MirageGate/Setup Play Scene", priority = 10)]
        public static void Setup()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DbPath);
            if (db == null)
            {
                EditorUtility.DisplayDialog("MirageGate",
                    "GameDatabase.asset が見つかりません。\n先に メニュー▸MirageGate▸Import Data▸★Import ALL を実行してください。", "OK");
                return;
            }

            // ルート（全システム＋ビューを集約）
            var root = GameObject.Find("MirageGate") ?? new GameObject("MirageGate");
            var gc = Get<GameController>(root);
            var turn = Get<TurnManager>(root);
            var ai = Get<EnemyAI>(root);
            var feel = Get<GameFeelDirector>(root);
            var dlg = Get<DialogueManager>(root);
            var tut = Get<TutorialManager>(root);
            var board = Get<GameBoardView>(root);
            var input = Get<GameInput>(root);
            var hud = Get<Hud>(root);
            var sfx = Get<SfxPlayer>(root);
            var slash = Get<SlashFxPlayer>(root);
            var screens = Get<GameScreens>(root);
            // BGMはSEと別AudioSourceにするため子オブジェクトに
            var musicGo = GameObject.Find("MirageGate Music") ?? new GameObject("MirageGate Music");
            musicGo.transform.SetParent(root.transform);
            Get<MusicPlayer>(musicGo);

            // カメラ
            var camGo = Camera.main ? Camera.main.gameObject : new GameObject("Main Camera");
            if (!camGo.GetComponent<Camera>()) camGo.AddComponent<Camera>();
            if (!camGo.GetComponent<AudioListener>()) camGo.AddComponent<AudioListener>();
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = 6f;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.04f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            var rig = Get<CameraRig>(camGo);

            // 結線
            gc.db = db;
            gc.turnManager = turn; gc.enemyAI = ai; gc.feel = feel;
            gc.dialogue = dlg; gc.tutorial = tut;
            gc.board = board; gc.cameraRig = rig; gc.input = input; gc.hud = hud;
            gc.skipTitle = false; // タイトル画面から開始
            screens.gc = gc;

            rig.feel = feel;
            input.turnManager = turn; input.db = db;
            hud.cam = cam; hud.feel = feel; hud.db = db; hud.tileSize = board.tileSize;
            board.feel = feel;
            feel.sfx = sfx;
            feel.slashFx = slash;
            slash.tileSize = board.tileSize;

            EditorUtility.SetDirty(gc); EditorUtility.SetDirty(rig);
            EditorUtility.SetDirty(input); EditorUtility.SetDirty(hud);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Selection.activeGameObject = root;
            Debug.Log("[MirageGate] Play Scene を構築しました。▶Play でデバッグ出撃します（矢印/WASD移動・1〜5でカード）。");
        }

        static T Get<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }
    }
}
