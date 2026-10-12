
using _Game.Scripts;
using UnityEngine;

public class LissajousCurveTestScript : MonoBehaviour
{
    public LissajousCurve lissajousCurve;

    // Start is called before the first frame update
    void Start()
    {
        lissajousCurve.OnStart(gameObject);
    }

    // Update is called once per frame
    void Update()
    {
        transform.position = lissajousCurve.Calculate(transform);
    }
    
    private void OnValidate()
    {
        if(lissajousCurve == null || lissajousCurve.tr == null) return;
        
        lissajousCurve.tr.Clear();

        Invoke(nameof(EnableTrailRenderer), 0.1f);
    }

    private void EnableTrailRenderer()
    {
        lissajousCurve?.CheckTrailRenderer();
    }
}
