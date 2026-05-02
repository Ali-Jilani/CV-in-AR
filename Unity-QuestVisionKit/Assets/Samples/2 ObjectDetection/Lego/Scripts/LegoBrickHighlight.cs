using System.Collections;
using UnityEngine;

public class LegoBrickHighlight : MonoBehaviour
{
    [Header("Renderer")]
    [SerializeField] private MeshRenderer meshRenderer;

    [Header("State Materials")]
    [SerializeField] private Material redMaterial;
    [SerializeField] private Material yellowMaterial;
    [SerializeField] private Material greenMaterial;
    [SerializeField] private Material greyMaterial;

    [Header("Green Pulse")]
    [SerializeField] private float pulseDuration = 0.4f;
    [SerializeField] private float pulseScale = 1.2f;

    private Coroutine _pulseCoroutine;
    private Vector3 _baseScale;
    private BrickHighlightState _lastState = BrickHighlightState.Hidden;

    private void Awake()
    {
        if (!meshRenderer)
        {
            meshRenderer = GetComponentInChildren<MeshRenderer>();
        }
    }

    private void OnEnable()
    {
        _lastState = BrickHighlightState.Hidden;
    }

    public void SetState(BrickHighlightState state)
    {
        if (!meshRenderer)
        {
            return;
        }

        switch (state)
        {
            case BrickHighlightState.Red:
                meshRenderer.sharedMaterial = redMaterial;
                break;
            case BrickHighlightState.Yellow:
                meshRenderer.sharedMaterial = yellowMaterial;
                break;
            case BrickHighlightState.Green:
                meshRenderer.sharedMaterial = greenMaterial;
                if (_lastState != BrickHighlightState.Green)
                {
                    StartPulse();
                }
                break;
            case BrickHighlightState.Grey:
                meshRenderer.sharedMaterial = greyMaterial;
                break;
            case BrickHighlightState.Hidden:
                gameObject.SetActive(false);
                break;
        }
        _lastState = state;
    }

    private void StartPulse()
    {
        if (_pulseCoroutine != null)
        {
            StopCoroutine(_pulseCoroutine);
        }
        _baseScale = transform.localScale;
        _pulseCoroutine = StartCoroutine(PulseRoutine());
    }

    private IEnumerator PulseRoutine()
    {
        var t = 0f;
        while (t < pulseDuration)
        {
            t += Time.deltaTime;
            var n = t / pulseDuration;
            var s = 1f + (pulseScale - 1f) * Mathf.Sin(n * Mathf.PI);
            transform.localScale = _baseScale * s;
            yield return null;
        }
        transform.localScale = _baseScale;
        _pulseCoroutine = null;
    }
}
