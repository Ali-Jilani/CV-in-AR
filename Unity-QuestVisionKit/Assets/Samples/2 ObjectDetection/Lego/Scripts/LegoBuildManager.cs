using System;
using System.Collections.Generic;
using UnityEngine;

public enum BrickHighlightState
{
    Hidden,
    Red,
    Yellow,
    Green,
    Grey,
}

public class LegoBuildManager : MonoBehaviour
{
    [Serializable]
    public struct BuildStep
    {
        public string brickClass;
        public float proximityTolerance;
    }

    [Header("Recipe (ordered list of bricks the user must place)")]
    [Tooltip("Each step: a brick class name (must match Resources/LegoClasses.txt) and a tolerance in metres for the spatial proximity check against the previous step's last-known position.")]
    [SerializeField] private List<BuildStep> _recipe = new();

    [Header("Step 1 debounce")]
    [Tooltip("How long the first brick must be held still before step 1 commits. Subsequent steps commit immediately on the first frame the proximity check passes.")]
    [SerializeField] private float _firstStepHoldSeconds = 1.0f;
    [Tooltip("Tolerance in metres for the position-stillness check during the first-step hold.")]
    [SerializeField] private float _firstStepStillnessRadius = 0.03f;

    [Header("Audio")]
    [SerializeField] private AudioSource _successChime;

    private int _currentStep;
    private readonly List<Vector3> _placedPositions = new();
    private readonly HashSet<string> _legoClassSet = new();
    private bool _legoClassSetReady;
    private int _lastCommitFrame = -1;

    private Vector3 _firstStepCandidatePos;
    private float _firstStepCandidateSince;
    private bool _firstStepHasCandidate;

    public int CurrentStep => _currentStep;
    public bool IsComplete => _currentStep >= _recipe.Count;

    public void RegisterLegoClasses(IEnumerable<string> classNames)
    {
        _legoClassSet.Clear();
        foreach (var c in classNames)
        {
            if (!string.IsNullOrWhiteSpace(c))
            {
                _legoClassSet.Add(c.Trim());
            }
        }
        _legoClassSetReady = _legoClassSet.Count > 0;
    }

    public BrickHighlightState Classify(string className, Vector3 worldPos)
    {
        if (string.IsNullOrEmpty(className))
        {
            return BrickHighlightState.Hidden;
        }

        if (IsComplete)
        {
            return IsLegoClass(className) ? BrickHighlightState.Grey : BrickHighlightState.Hidden;
        }

        var current = _recipe[_currentStep];

        if (className != current.brickClass)
        {
            return IsLegoClass(className) ? BrickHighlightState.Grey : BrickHighlightState.Hidden;
        }

        if (_lastCommitFrame == Time.frameCount)
        {
            return BrickHighlightState.Red;
        }

        if (_currentStep == 0)
        {
            if (TryCommitFirstStep(worldPos))
            {
                return BrickHighlightState.Green;
            }
            return BrickHighlightState.Red;
        }

        var lastPos = _placedPositions[_currentStep - 1];
        if (Vector3.Distance(worldPos, lastPos) <= current.proximityTolerance)
        {
            CommitStep(worldPos);
            return BrickHighlightState.Green;
        }
        return BrickHighlightState.Red;
    }

    private bool TryCommitFirstStep(Vector3 worldPos)
    {
        if (!_firstStepHasCandidate)
        {
            _firstStepCandidatePos = worldPos;
            _firstStepCandidateSince = Time.time;
            _firstStepHasCandidate = true;
            return false;
        }

        if (Vector3.Distance(worldPos, _firstStepCandidatePos) > _firstStepStillnessRadius)
        {
            _firstStepCandidatePos = worldPos;
            _firstStepCandidateSince = Time.time;
            return false;
        }

        if (Time.time - _firstStepCandidateSince < _firstStepHoldSeconds)
        {
            return false;
        }

        CommitStep(worldPos);
        _firstStepHasCandidate = false;
        return true;
    }

    private void CommitStep(Vector3 worldPos)
    {
        _placedPositions.Add(worldPos);
        _currentStep++;
        _lastCommitFrame = Time.frameCount;
        if (_successChime)
        {
            _successChime.Play();
        }
    }

    private bool IsLegoClass(string className)
    {
        if (!_legoClassSetReady)
        {
            return true;
        }
        return _legoClassSet.Contains(className);
    }
}
