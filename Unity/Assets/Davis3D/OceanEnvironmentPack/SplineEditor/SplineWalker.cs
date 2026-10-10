using UnityEngine;

public class SplineWalker : MonoBehaviour
{

    public BezierSpline spline;

    public float duration;

    public bool lookForward;

    public bool randomStart;

    public SplineWalkerMode mode;

    // Optional formation offsets used by the additional underwater maps.
    public Vector3 pathOffset;
    public float initialProgress = -1f;

    private float progress;
    private bool goingForward = true;

    private void Start()
    {
        if (initialProgress >= 0f)
            progress = Mathf.Repeat(initialProgress, 1f);
        else if (randomStart)
            progress = Random.value;
    }

    private void Update()
    {
        if (!spline || duration <= 0f) return;
        if (goingForward)
        {
            progress += Time.deltaTime / duration;
            if (progress > 1f)
            {
                if (mode == SplineWalkerMode.Once)
                {
                    progress = 1f;
                }
                else if (mode == SplineWalkerMode.Loop)
                {
                    progress = Mathf.Repeat(progress, 1f);
                }
                else
                {
                    progress = 2f - progress;
                    goingForward = false;
                }
            }
        }
        else
        {
            progress -= Time.deltaTime / duration;
            if (progress < 0f)
            {
                progress = -progress;
                goingForward = true;
            }
        }

        Vector3 position = spline.GetPoint(progress) + pathOffset;
        // BezierSpline.GetPoint returns world space, even inside a translated school root.
        transform.position = position;
        if (lookForward)
        {
            transform.LookAt(position + spline.GetDirection(progress));
        }
    }
}
