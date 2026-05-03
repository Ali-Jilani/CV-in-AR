using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class LegoGuidancePanel : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private LegoBuildManager _buildManager;

    [Header("UI References")]
    [Tooltip("Header text, e.g. \"[ STEP 2/3 ]\". Auto-discovered as a child named 'Header' if left empty.")]
    [SerializeField] private TextMeshProUGUI _headerText;
    [Tooltip("Body text - the per-step instruction. Auto-discovered as a child named 'Body' if left empty.")]
    [SerializeField] private TextMeshProUGUI _bodyText;

    [Header("HUD Pose (relative to Camera.main)")]
    [Tooltip("Local offset under the camera transform. X = right (+) / left (-), Y = up (+) / down (-), Z = forward distance. Default places the panel in the upper-right of the user's view at 0.5 m depth.")]
    [SerializeField] private Vector3 _hudOffset = new Vector3(0.18f, 0.12f, 0.5f);
    [Tooltip("If true, the panel always faces the camera (identity local rotation). Disable to apply a custom local rotation set in the inspector.")]
    [SerializeField] private bool _hudFaceCamera = true;

    [Header("Copy")]
    [Tooltip("Shown in the header (replacing the step counter) once the build is complete. Body is left empty.")]
    [SerializeField] private string _completionMessage = "[ TASK COMPLETE ]";

    private Transform _originalParent;
    private Vector3 _originalLocalPosition;
    private Quaternion _originalLocalRotation;
    private bool _hasOriginalParent;

    private void Awake()
    {
        if (!_headerText)
        {
            var header = transform.Find("Canvas/Header");
            if (header) _headerText = header.GetComponent<TextMeshProUGUI>();
        }
        if (!_bodyText)
        {
            var body = transform.Find("Canvas/Body");
            if (body) _bodyText = body.GetComponent<TextMeshProUGUI>();
        }
    }

    private void OnEnable()
    {
        if (_buildManager)
        {
            _buildManager.StepCommitted += OnStepCommitted;
            _buildManager.BuildCompleted += OnBuildCompleted;
        }
        AttachToCamera();
        Refresh();
    }

    private void OnDisable()
    {
        if (_buildManager)
        {
            _buildManager.StepCommitted -= OnStepCommitted;
            _buildManager.BuildCompleted -= OnBuildCompleted;
        }
        DetachFromCamera();
    }

    private void AttachToCamera()
    {
        var cam = Camera.main;
        if (!cam)
        {
            Debug.LogWarning("[LegoGuidancePanel] No Camera.main found; HUD will stay at scene-root pose.");
            return;
        }
        if (!_hasOriginalParent)
        {
            _originalParent = transform.parent;
            _originalLocalPosition = transform.localPosition;
            _originalLocalRotation = transform.localRotation;
            _hasOriginalParent = true;
        }
        transform.SetParent(cam.transform, worldPositionStays: false);
        transform.localPosition = _hudOffset;
        transform.localRotation = _hudFaceCamera ? Quaternion.identity : transform.localRotation;
    }

    private void DetachFromCamera()
    {
        if (!_hasOriginalParent) return;
        transform.SetParent(_originalParent, worldPositionStays: false);
        transform.localPosition = _originalLocalPosition;
        transform.localRotation = _originalLocalRotation;
        _hasOriginalParent = false;
    }

    private void OnStepCommitted(int newStepIndex)
    {
        Refresh();
    }

    private void OnBuildCompleted()
    {
        if (_headerText) _headerText.text = _completionMessage;
        if (_bodyText) _bodyText.text = string.Empty;
    }

    private void Refresh()
    {
        if (!_buildManager) return;
        if (_buildManager.IsComplete)
        {
            if (_headerText) _headerText.text = _completionMessage;
            if (_bodyText) _bodyText.text = string.Empty;
            return;
        }
        var stepNumber = _buildManager.CurrentStep + 1;
        var total = _buildManager.RecipeCount;
        if (_headerText) _headerText.text = $"[ STEP {stepNumber}/{total} ]";
        if (_bodyText) _bodyText.text = _buildManager.CurrentDescription;
    }
}
