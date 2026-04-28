using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns obstacles off the right edge and moves them left.
/// Camera stays static; movement comes from obstacles moving left.
/// </summary>
[DisallowMultipleComponent]
public sealed class ObstacleSpawner : MonoBehaviour
{
    [Header("Spawn Timing")]
    [SerializeField] private float spawnIntervalMin = 0.9f;
    [SerializeField] private float spawnIntervalMax = 1.5f;

    [Header("Spawn Position")]
    [SerializeField] private float spawnXPadding = 2.5f;
    [SerializeField] private float spawnYMin = -2.8f;
    [SerializeField] private float spawnYMax = 1.5f;

    [Header("Speed")]
    [SerializeField] private float baseSpeed = 6.5f;
    [SerializeField] private float speedIncreasePerSecond = 0.12f;
    [SerializeField] private float maxSpeed = 18f;

    [Header("Obstacle Shape (runtime fallback)")]
    [SerializeField] private Vector2 obstacleSize = new Vector2(0.9f, 1.4f);

    private Camera _cam;
    private GameObject _obstaclePrefab;
    private readonly List<Rigidbody2D> _obstacleBodies = new();

    private bool _running;
    private float _nextSpawnTime;
    private float _runTime;

    private void Awake()
    {
        _cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        _obstaclePrefab = Resources.Load<GameObject>("Prefabs/Obstacle");
        ScheduleNextSpawn();
    }

    private void Update()
    {
        if (!_running)
            return;

        _runTime += Time.deltaTime;

        if (Time.time >= _nextSpawnTime)
        {
            SpawnObstacle();
            ScheduleNextSpawn();
        }

        // Cleanup off-screen.
        float leftCullX = GetLeftWorldX() - 6f;
        for (int i = _obstacleBodies.Count - 1; i >= 0; i--)
        {
            var rb = _obstacleBodies[i];
            if (rb == null)
            {
                _obstacleBodies.RemoveAt(i);
                continue;
            }

            if (rb.transform.position.x < leftCullX)
            {
                _obstacleBodies.RemoveAt(i);
                Destroy(rb.gameObject);
            }
        }
    }

    private void FixedUpdate()
    {
        if (!_running)
            return;

        float speed = GetCurrentSpeed();
        Vector2 step = Vector2.left * speed * Time.fixedDeltaTime;

        // Move via physics so collisions are reliable.
        for (int i = 0; i < _obstacleBodies.Count; i++)
        {
            var rb = _obstacleBodies[i];
            if (rb == null) continue;
            rb.MovePosition(rb.position + step);
        }
    }

    public void SetRunning(bool running)
    {
        _running = running;
        if (running)
        {
            _runTime = 0f;
            ScheduleNextSpawn();
        }
    }

    private void SpawnObstacle()
    {
        GameObject o = _obstaclePrefab != null ? Instantiate(_obstaclePrefab) : CreateRuntimeObstacle();

        // Tag in code (requested).
        TryTagAsObstacle(o);

        // Ensure a non-trigger collider exists.
        EnsureObstacleCollider(o);

        // Ensure Rigidbody2D exists and is kinematic (we move it with MovePosition).
        var rb = o.GetComponent<Rigidbody2D>();
        if (rb == null) rb = o.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        float x = GetRightWorldX() + spawnXPadding;
        float y = Random.Range(spawnYMin, spawnYMax);
        rb.position = new Vector2(x, y);

        o.name = "Obstacle";
        _obstacleBodies.Add(rb);
    }

    private GameObject CreateRuntimeObstacle()
    {
        var go = new GameObject("Obstacle_Runtime");

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = RuntimeSpriteFactory.GetWhiteSprite();
        sr.color = new Color(0.85f, 0.2f, 0.2f, 1f);
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = obstacleSize;
        sr.sortingOrder = -5;

        var bc = go.AddComponent<BoxCollider2D>();
        bc.size = obstacleSize;
        bc.isTrigger = false;

        return go;
    }

    private void EnsureObstacleCollider(GameObject obstacle)
    {
        var existing = obstacle.GetComponentInChildren<Collider2D>();
        if (existing != null)
        {
            existing.isTrigger = false;
            return;
        }

        var bc = obstacle.AddComponent<BoxCollider2D>();
        bc.isTrigger = false;

        var sr = obstacle.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            Vector2 approx = sr.bounds.size;
            bc.size = new Vector2(Mathf.Max(0.1f, approx.x), Mathf.Max(0.1f, approx.y));
        }
        else
        {
            bc.size = obstacleSize;
        }
    }

    private void ScheduleNextSpawn()
    {
        _nextSpawnTime = Time.time + Random.Range(spawnIntervalMin, spawnIntervalMax);
    }

    private float GetCurrentSpeed()
    {
        float speed = baseSpeed + _runTime * speedIncreasePerSecond;
        return Mathf.Min(speed, maxSpeed);
    }

    private float GetLeftWorldX()
    {
        if (_cam == null) return -20f;
        return _cam.ViewportToWorldPoint(new Vector3(0f, 0.5f, 0f)).x;
    }

    private float GetRightWorldX()
    {
        if (_cam == null) return 20f;
        return _cam.ViewportToWorldPoint(new Vector3(1f, 0.5f, 0f)).x;
    }

    private static void TryTagAsObstacle(GameObject o)
    {
        try
        {
            o.tag = "Obstacle";
        }
        catch (UnityException)
        {
            Debug.LogWarning("Tag 'Obstacle' is not defined in Tags & Layers. Define it to enable obstacle tagging.");
        }
    }

    private static class RuntimeSpriteFactory
    {
        private static Sprite _white;

        public static Sprite GetWhiteSprite()
        {
            if (_white != null) return _white;

            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);

            _white = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return _white;
        }
    }
}

