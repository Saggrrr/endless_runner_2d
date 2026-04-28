using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns ground chunks infinitely and scrolls them left.
/// Camera stays static; movement comes from ground moving left.
/// </summary>
[DisallowMultipleComponent]
public sealed class GroundSpawner : MonoBehaviour
{
    [SerializeField] private float chunkWidth = 12f;
    [SerializeField] private float groundY = -3.5f;
    [SerializeField] private int initialChunks = 6;
    [SerializeField] private float scrollSpeed = 6f;

    private readonly List<GameObject> _chunks = new();
    private GameObject _chunkPrefab;
    private Camera _cam;
    private bool _running;
    private float _nextSpawnX;

    private void Awake()
    {
        _cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        _chunkPrefab = Resources.Load<GameObject>("Prefabs/GroundChunk");
        BuildInitialGround();
    }

    private void BuildInitialGround()
    {
        float startX = GetLeftWorldX() - chunkWidth;
        _nextSpawnX = startX;

        for (int i = 0; i < Mathf.Max(1, initialChunks); i++)
        {
            SpawnChunkAt(_nextSpawnX);
            _nextSpawnX += chunkWidth;
        }
    }

    private void Update()
    {
        if (!_running)
            return;

        float dx = scrollSpeed * Time.deltaTime;
        for (int i = 0; i < _chunks.Count; i++)
        {
            if (_chunks[i] == null) continue;
            _chunks[i].transform.position += Vector3.left * dx;
        }

        float rightEdge = GetRightWorldX();
        while (_nextSpawnX < rightEdge + chunkWidth * 2f)
        {
            SpawnChunkAt(_nextSpawnX);
            _nextSpawnX += chunkWidth;
        }

        float leftCullX = GetLeftWorldX() - chunkWidth * 3f;
        for (int i = _chunks.Count - 1; i >= 0; i--)
        {
            var c = _chunks[i];
            if (c == null)
            {
                _chunks.RemoveAt(i);
                continue;
            }

            float chunkRightX = c.transform.position.x + chunkWidth * 0.5f;
            if (chunkRightX < leftCullX)
            {
                _chunks.RemoveAt(i);
                Destroy(c);
            }
        }
    }

    public void SetRunning(bool running) => _running = running;

    private void SpawnChunkAt(float x)
    {
        GameObject chunk = _chunkPrefab != null ? Instantiate(_chunkPrefab) : CreateRuntimeChunk();
        chunk.transform.position = new Vector3(x + chunkWidth * 0.5f, groundY, 0f);
        _chunks.Add(chunk);
    }

    private GameObject CreateRuntimeChunk()
    {
        var go = new GameObject("GroundChunk_Runtime");

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = RuntimeSpriteFactory.GetWhiteSprite();
        sr.color = new Color(0.18f, 0.18f, 0.18f, 1f);
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = new Vector2(chunkWidth, 1.2f);
        sr.sortingOrder = -10;

        var bc = go.AddComponent<BoxCollider2D>();
        bc.size = new Vector2(chunkWidth, 1.2f);
        bc.isTrigger = false;

        return go;
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

