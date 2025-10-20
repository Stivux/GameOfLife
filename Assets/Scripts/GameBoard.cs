using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-1)]
public class GameBoard : MonoBehaviour
{
    [SerializeField] private Tilemap currentState;
    [SerializeField] private Tilemap nextState;
    [SerializeField] private Tile aliveTile;
    [SerializeField] private Tile deadTile;
    [SerializeField] private Pattern pattern;
    [SerializeField] private float updateInterval = 0.05f;
    [SerializeField] private TextMeshProUGUI speedText;

    private readonly HashSet<Vector3Int> _aliveCells = new();
    private readonly HashSet<Vector3Int> _cellsToCheck = new();

    private Camera _mainCamera;
    private Coroutine _simulationCoroutine;
    private bool _paused = true;

    private float _speedChangeCooldown;
    private const float InitialCooldown = 0.1f;
    private const float ContinuousCooldown = 0.05f;

    public int Population { get; private set; }
    public int Iterations { get; private set; }
    public float Time { get; private set; }

    private void Start()
    {
        _mainCamera = Camera.main;

        SetPattern(pattern);
        UpdateSpeedText();

        _simulationCoroutine = StartCoroutine(Simulate());
    }

    private void Update()
    {
        if (!_mainCamera) return;

        HandleMouseInput();

        HandleKeyboardInput();
    }

    private void OnDisable()
    {
        if (_simulationCoroutine != null)
        {
            StopCoroutine(_simulationCoroutine);
        }
    }

    private void GenerateRandomState(float density = 0.3f)
    {
        Clear();

        var cameraBounds = GetCameraBounds();

        for (var x = cameraBounds.xMin; x <= cameraBounds.xMax; x++)
        {
            for (var y = cameraBounds.yMin; y <= cameraBounds.yMax; y++)
            {
                if (!(Random.value < density)) continue;
                var cell = new Vector3Int(x, y, 0);
                currentState.SetTile(cell, aliveTile);
                _aliveCells.Add(cell);
            }
        }

        Population = _aliveCells.Count;
        Iterations = 0;
        Time = 0f;
    }

    private BoundsInt GetCameraBounds()
    {
        if (!_mainCamera)
            _mainCamera = Camera.main;

        // Получаем углы видимой области в мировых координатах
        var bottomLeft = _mainCamera.ScreenToWorldPoint(Vector3.zero);
        var topRight = _mainCamera.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, 0));

        var cellBottomLeft = currentState.WorldToCell(bottomLeft);
        var cellTopRight = currentState.WorldToCell(topRight);

        return new BoundsInt(
            cellBottomLeft.x - 1,
            cellBottomLeft.y - 1,
            0,
            cellTopRight.x - cellBottomLeft.x + 2,
            cellTopRight.y - cellBottomLeft.y + 2,
            1
        );
    }

    private void HandleMouseInput()
    {
        if (!_paused) return;

        if (!Input.GetMouseButtonDown(0)) return;

        var worldPos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);

        var cell = currentState.WorldToCell(worldPos);

        ToggleCell(cell);
    }

    private void HandleKeyboardInput()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            _paused = !_paused;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SceneManager.LoadScene("MenuScene");
        }
        
        if (!_paused) return;
        if (Input.GetKeyDown(KeyCode.R))
            GenerateRandomState();
        HandleSpeedControl();
    }

    private void HandleSpeedControl()
    {
        var upArrowPressed = Input.GetKey(KeyCode.UpArrow);
        var downArrowPressed = Input.GetKey(KeyCode.DownArrow);

        if (!upArrowPressed && !downArrowPressed)
        {
            _speedChangeCooldown = 0f;
            return;
        }

        _speedChangeCooldown -= UnityEngine.Time.deltaTime;

        if (!(_speedChangeCooldown <= 0f)) return;
        if (upArrowPressed)
        {
            DecreaseSpeed();
        }
        else
        {
            IncreaseSpeed();
        }

        _speedChangeCooldown = _speedChangeCooldown <= 0f ? InitialCooldown : ContinuousCooldown;
    }

    private void IncreaseSpeed()
    {
        updateInterval = Mathf.Max(0.01f, updateInterval - 0.01f);
        UpdateSpeedText();
    }

    private void DecreaseSpeed()
    {
        updateInterval = Mathf.Min(1f, updateInterval + 0.01f);
        UpdateSpeedText();
    }

    private void UpdateSpeedText()
    {
        if (speedText)
        {
            speedText.text = $"DELAY: {updateInterval:F2}";
        }
    }

    private void ToggleCell(Vector3Int cell)
    {
        if (IsAlive(cell))
        {
            currentState.SetTile(cell, deadTile);
            _aliveCells.Remove(cell);
        }
        else
        {
            currentState.SetTile(cell, aliveTile);
            _aliveCells.Add(cell);
        }

        Population = _aliveCells.Count;
    }

    private IEnumerator Simulate()
    {
        while (enabled)
        {
            while (_paused)
            {
                yield return null;
            }

            UpdateState();

            Population = _aliveCells.Count;
            Iterations++;
            Time += updateInterval;

            yield return new WaitForSeconds(updateInterval);
        }
    }

    private void SetPattern(Pattern patternSet)
    {
        Clear();

        if (!patternSet) return;
        var center = patternSet.GetCenter();

        foreach (var t in patternSet.cells)
        {
            var cell = (Vector3Int)(t - center);
            currentState.SetTile(cell, aliveTile);
            _aliveCells.Add(cell);
        }

        Population = _aliveCells.Count;
    }

    private void Clear()
    {
        _aliveCells.Clear();
        _cellsToCheck.Clear();
        currentState.ClearAllTiles();
        nextState.ClearAllTiles();
        Population = 0;
        Iterations = 0;
        Time = 0f;
        _paused = true;
    }

    private void UpdateState()
    {
        _cellsToCheck.Clear();

        foreach (var cell in _aliveCells)
        {
            for (var x = -1; x <= 1; x++)
            {
                for (var y = -1; y <= 1; y++)
                {
                    _cellsToCheck.Add(cell + new Vector3Int(x, y));
                }
            }
        }

        foreach (var cell in _cellsToCheck)
        {
            var neighbors = CountNeighbors(cell);
            var alive = IsAlive(cell);

            switch (alive)
            {
                case false when neighbors == 3:
                    nextState.SetTile(cell, aliveTile);
                    _aliveCells.Add(cell);
                    break;
                case true when neighbors is < 2 or > 3:
                    nextState.SetTile(cell, deadTile);
                    _aliveCells.Remove(cell);
                    break;

                default:
                    nextState.SetTile(cell, currentState.GetTile(cell));
                    break;
            }
        }

        (currentState, nextState) = (nextState, currentState);
        nextState.ClearAllTiles();
    }

    private int CountNeighbors(Vector3Int cell)
    {
        var count = 0;

        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                var neighbor = cell + new Vector3Int(x, y);
                if (IsAlive(neighbor) && !(x == 0 && y == 0))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private bool IsAlive(Vector3Int cell)
    {
        return currentState.GetTile(cell) == aliveTile;
    }
}