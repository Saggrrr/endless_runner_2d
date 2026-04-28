using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Game state + score + lives + UI.
/// Also enforces a static camera (illusion of running comes from world moving left).
/// </summary>
public sealed class GameManager : MonoBehaviour
{
    public enum GameState { Waiting, Playing, Dead }

    public static GameManager Instance { get; private set; }

    public GameState State { get; private set; } = GameState.Waiting;
    public float ScoreSeconds { get; private set; }
    public int Lives { get; private set; } = 3;

    private PlayerController _player;
    private GroundSpawner _groundSpawner;
    private ObstacleSpawner _obstacleSpawner;

    private float _runStartTime;

    // HUD
    private Text _hudText;

    // Camera lock (keeps camera completely static).
    private Camera _cam;
    private Vector3 _camStartPos;
    private Quaternion _camStartRot;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Always keep the camera static (even if other scripts try to move it).
        _cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        if (_cam != null)
        {
            _camStartPos = _cam.transform.position;
            _camStartRot = _cam.transform.rotation;
        }

        AutoWireScene();
        BuildHud();

        // Start paused until the first tap.
        Time.timeScale = 0f;
        UpdateHud();
    }

    private void LateUpdate()
    {
        // Enforce static camera: the illusion of motion comes from ground/obstacles moving left.
        if (_cam == null) return;
        _cam.transform.position = _camStartPos;
        _cam.transform.rotation = _camStartRot;
    }

    private void AutoWireScene()
    {
        _player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        _groundSpawner = FindFirstObjectByType<GroundSpawner>(FindObjectsInactive.Include);
        _obstacleSpawner = FindFirstObjectByType<ObstacleSpawner>(FindObjectsInactive.Include);

        if (_groundSpawner == null)
            _groundSpawner = new GameObject("GroundSpawner").AddComponent<GroundSpawner>();

        if (_obstacleSpawner == null)
            _obstacleSpawner = new GameObject("ObstacleSpawner").AddComponent<ObstacleSpawner>();
    }

    private void Update()
    {
        bool tapped = IsTapPressedThisFrame();
        bool space = IsSpacePressedThisFrame();

        if (State == GameState.Waiting)
        {
            if (tapped || space) StartRun();
            return;
        }

        if (State == GameState.Playing)
        {
            ScoreSeconds = Time.time - _runStartTime;
            UpdateHud();
            return;
        }

        // Dead
        if (tapped || space)
            RestartScene();
    }

    private void StartRun()
    {
        State = GameState.Playing;
        Lives = 3;
        ScoreSeconds = 0f;
        _runStartTime = Time.time;

        // Unpause gameplay: spawners use deltaTime / FixedUpdate so they stop when timeScale = 0.
        Time.timeScale = 1f;

        // Start moving world.
        if (_groundSpawner != null) _groundSpawner.SetRunning(true);
        if (_obstacleSpawner != null) _obstacleSpawner.SetRunning(true);

        // Enable player input.
        if (_player != null) _player.SetRunStarted(true);

        UpdateHud();
    }

    /// <summary>Called when the player collides with an obstacle.</summary>
    public void RegisterObstacleHit(GameObject obstacleColliderOrRoot)
    {
        if (State != GameState.Playing)
            return;

        Lives = Mathf.Max(0, Lives - 1);

        // Destroy the obstacle object that was hit so it can't drain multiple lives.
        if (obstacleColliderOrRoot != null)
            Destroy(obstacleColliderOrRoot.transform.root.gameObject);

        if (Lives <= 0)
            KillPlayer();
        else
            UpdateHud();
    }

    public void KillPlayer()
    {
        if (State != GameState.Playing)
            return;

        // Freeze the final score.
        ScoreSeconds = Time.time - _runStartTime;

        State = GameState.Dead;

        // 1) Stop all movement immediately.
        // This also prevents spawners/movers from progressing.
        Time.timeScale = 0f;

        // Make sure spawners stop even if they use unscaled time in the future.
        if (_groundSpawner != null) _groundSpawner.SetRunning(false);
        if (_obstacleSpawner != null) _obstacleSpawner.SetRunning(false);

        // 3) Keep player visible: freeze its rigidbody.
        if (_player != null)
        {
            _player.SetRunStarted(false);
            _player.FreezeOnDeath();
        }

        UpdateHud();
    }

    private void RestartScene()
    {
        Time.timeScale = 1f;
        Scene scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex);
    }

    private void BuildHud()
    {
        var canvasGO = new GameObject("HUD");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        var textGO = new GameObject("HUDText");
        textGO.transform.SetParent(canvasGO.transform, worldPositionStays: false);

        _hudText = textGO.AddComponent<Text>();
        _hudText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _hudText.fontSize = 28;
        _hudText.alignment = TextAnchor.UpperLeft;
        _hudText.color = Color.white;

        var rt = _hudText.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(18f, -18f);
        rt.sizeDelta = new Vector2(900f, 240f);
    }

    private void UpdateHud()
    {
        if (_hudText == null) return;

        if (State == GameState.Waiting)
        {
            _hudText.text =
                $"Lives: {Lives}\n" +
                "Score: 0.0\n\n" +
                "Tap / Click / Space to start";
            return;
        }

        if (State == GameState.Playing)
        {
            _hudText.text =
                $"Lives: {Lives}\n" +
                $"Score: {ScoreSeconds:0.0}";
            return;
        }

        _hudText.text =
            "GAME OVER\n" +
            $"Score: {ScoreSeconds:0.0}\n\n" +
            "Tap / Click / Space to restart";
    }

    private static bool IsTapPressedThisFrame()
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            return true;
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            return true;
        return false;
    }

    private static bool IsSpacePressedThisFrame()
    {
        return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
    }
}

