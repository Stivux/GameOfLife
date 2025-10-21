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
    [SerializeField] private Tile player1Tile;
    [SerializeField] private Tile player2Tile;
    [SerializeField] private Tile deadTile;
    [SerializeField] private Pattern pattern;
    [SerializeField] private float updateInterval = 0.05f;
    [SerializeField] private TextMeshProUGUI speedText;

    [SerializeField] private TextMeshProUGUI infoTextUI;
    [SerializeField] private TextMeshProUGUI player1ScoreText;
    [SerializeField] private TextMeshProUGUI player2ScoreText;

    private readonly Dictionary<Vector3Int, int> _aliveCellsColors = new();
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

    private bool _isPVP;
    private int _player1Score;
    private int _player2Score;
    private bool _placementPhase;

    private void Start()
    {
        _mainCamera = Camera.main;

        _isPVP = PlayerPrefs.GetInt("IsPVP", 0) == 1;

        if (_isPVP)
        {
            _placementPhase = true;
            _player1Score = 0;
            _player2Score = 0;
            Clear();
            _paused = true;
            UpdateScoresUI();
            UpdateTurnText();
        }
        else
        {
            SetPattern(pattern);
        }

        UpdateSpeedText();

        _simulationCoroutine = StartCoroutine(Simulate());
    }

    private void Update()
    {
        if (!_mainCamera) return;

        HandleMouseInput();

        HandleKeyboardInput();

        if (_isPVP && _placementPhase)
        {
            UpdateTurnText();
        }
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
                var color = _isPVP ? Random.Range(1, 3) : 1;
                var cell = new Vector3Int(x, y, 0);
                SetCell(cell, color);
            }
        }

        Population = _aliveCellsColors.Count;
        Iterations = 0;
        Time = 0f;
    }

    private BoundsInt GetCameraBounds()
    {
        if (!_mainCamera)
            _mainCamera = Camera.main;

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
        var leftClick = Input.GetMouseButtonDown(0);
        var rightClick = Input.GetMouseButtonDown(1);

        if (!leftClick && !rightClick || (rightClick && !_isPVP)) return;
        var worldPos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
        var cell = currentState.WorldToCell(worldPos);

        var player = 1;
        if (rightClick && _isPVP)
        {
            player = 2;
        }

        if (_isPVP && _placementPhase)
        {
            if (_aliveCellsColors.TryGetValue(cell, out var existingColor))
            {
                if (existingColor != player)
                    return;

                SetCell(cell, 0);
            }
            else
            {
                SetCell(cell, player);
            }
        }
        else
        {
            if (IsAlive(cell))
            {
                if (_isPVP && _aliveCellsColors[cell] != player)
                    return;
                SetCell(cell, 0);
            }
            else
            {
                SetCell(cell, player);
            }
        }
    }

    private void HandleKeyboardInput()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (_isPVP && _placementPhase)
            {
                _placementPhase = false;
            }

            _paused = !_paused;
            UpdateTurnText();
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

    private void SetCell(Vector3Int cell, int color)
    {
        if (color == 0)
        {
            currentState.SetTile(cell, deadTile);
            _aliveCellsColors.Remove(cell);
        }
        else
        {
            var tile = color == 1 ? player1Tile : player2Tile;
            currentState.SetTile(cell, tile);
            _aliveCellsColors[cell] = color;
        }

        Population = _aliveCellsColors.Count;
    }

    private IEnumerator Simulate()
    {
        while (enabled)
        {
            while (_paused || (_isPVP && _placementPhase))
            {
                yield return null;
            }

            UpdateState();

            Population = _aliveCellsColors.Count;
            Iterations++;
            Time += updateInterval;

            if (_isPVP && CheckSingleColor())
            {
                _paused = true;
                ShowWinner();
            }

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
            SetCell(cell, 1);
        }

        Population = _aliveCellsColors.Count;
    }

    private void Clear()
    {
        _aliveCellsColors.Clear();
        _cellsToCheck.Clear();
        currentState.ClearAllTiles();
        nextState.ClearAllTiles();
        Population = 0;
        Iterations = 0;
        Time = 0f;
        _paused = true;

        if (_isPVP)
        {
            _player1Score = 0;
            _player2Score = 0;
            UpdateScoresUI();
        }
    }

    private void UpdateState()
    {
        _cellsToCheck.Clear();

        foreach (var cell in _aliveCellsColors.Keys)
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
                {
                    var color = GetMajorityColor(cell);
                    SetNextCell(cell, color);
                    _aliveCellsColors[cell] = color;

                    if (_isPVP)
                    {
                        if (color == 1) _player1Score++;
                        else _player2Score++;
                        UpdateScoresUI();
                    }

                    break;
                }
                case true when (neighbors < 2 || neighbors > 3):

                    SetNextCell(cell, 0);
                    _aliveCellsColors.Remove(cell);
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

    private int GetMajorityColor(Vector3Int cell)
    {
        var count1 = 0;
        var count2 = 0;

        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) continue;
                var neighbor = cell + new Vector3Int(x, y);
                if (!_aliveCellsColors.TryGetValue(neighbor, out var color)) continue;
                switch (color)
                {
                    case 1:
                        count1++;
                        break;
                    case 2:
                        count2++;
                        break;
                }
            }
        }

        return count1 > count2 ? 1 : 2;
    }

    private void SetNextCell(Vector3Int cell, int color)
    {
        var tile = color switch
        {
            1 => player1Tile,
            2 => player2Tile,
            _ => deadTile
        };
        nextState.SetTile(cell, tile);
    }

    private bool IsAlive(Vector3Int cell)
    {
        return _aliveCellsColors.ContainsKey(cell);
    }

    private bool CheckSingleColor()
    {
        if (Population == 0) return true;

        var hasPlayer1 = false;
        var hasPlayer2 = false;

        foreach (var color in _aliveCellsColors.Values)
        {
            switch (color)
            {
                case 1:
                    hasPlayer1 = true;
                    break;
                case 2:
                    hasPlayer2 = true;
                    break;
            }

            if (hasPlayer1 && hasPlayer2) return false;
        }

        return true;
    }

    private void UpdateTurnText()
    {
        if (!infoTextUI) return;
        if (_placementPhase)
        {
            infoTextUI.text = "Space to start";
        }
        else if (_paused)
        {
            infoTextUI.text = "Paused";
        }
        else
        {
            infoTextUI.text = "";
        }
    }

    private void UpdateScoresUI()
    {
        if (player1ScoreText) player1ScoreText.text = $"P1: {_player1Score}";
        if (player2ScoreText) player2ScoreText.text = $"P2: {_player2Score}";
    }

    private void ShowWinner()
    {
        if (!infoTextUI) return;
        if (_player1Score > _player2Score)
            infoTextUI.text = "P1 wins!";
        else if (_player2Score > _player1Score)
            infoTextUI.text = "P2 wins!";
        else
            infoTextUI.text = "Draw!";
    }
}