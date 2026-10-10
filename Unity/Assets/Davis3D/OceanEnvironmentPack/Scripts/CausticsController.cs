using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class CausticsController : MonoBehaviour
{
    [Header("Textures"), SerializeField]
    private Texture2D CausticsTex;

    [SerializeField]
    private Texture2D FlowMapTex;

    [Header("Scales"), SerializeField]
    private float GlobalScale = 1.0f;

    [SerializeField]
    private float
        FlowMapScale_1 = 0.1f,
        FlowMapScale_2 = 0.15f,
        FlowMapScale_3 = 0.2f,
        CausticsScale_1 = 0.1f,
        CausticsScale_2 = 0.2f,
        CausticsScale_3 = 0.3f;

    [Header("Speeds"), SerializeField]
    private float GlobalSpeed = 1.0f;

    [SerializeField]
    private float CausticsSpeed_1 = 0.05f,
        CausticsSpeed_2 = 0.075f,
        CausticsSpeed_3 = 0.1f;

    [Header("Intensities"), SerializeField]
    private float FlowMapIntensity = 0.3f;

    [SerializeField]
    private float CausticsIntensity = 0.75f;

    // Start is called before the first frame update
    private void Start()
    {

    }

    // Update is called once per frame
    private void Update()
    {
        Shader.SetGlobalFloat("_GlobalScale", GlobalScale);

        Shader.SetGlobalFloat("_FlowMapScale_1", FlowMapScale_1);
        Shader.SetGlobalFloat("_FlowMapScale_2", FlowMapScale_2);
        Shader.SetGlobalFloat("_FlowMapScale_3", FlowMapScale_3);

        Shader.SetGlobalFloat("_CausticsScale_1", CausticsScale_1);
        Shader.SetGlobalFloat("_CausticsScale_2", CausticsScale_2);
        Shader.SetGlobalFloat("_CausticsScale_3", CausticsScale_3);

        Shader.SetGlobalFloat("_GlobalSpeed", GlobalSpeed);

        Shader.SetGlobalFloat("_CausticsSpeed_1", CausticsSpeed_1);
        Shader.SetGlobalFloat("_CausticsSpeed_2", CausticsSpeed_2);
        Shader.SetGlobalFloat("_CausticsSpeed_3", CausticsSpeed_3);

        Shader.SetGlobalFloat("_FlowMapIntensity", FlowMapIntensity);
        Shader.SetGlobalFloat("_CausticsWave1_Multiply", CausticsIntensity);

        if (FlowMapTex)
            Shader.SetGlobalTexture("_FlowMap", FlowMapTex);
        if (CausticsTex)
            Shader.SetGlobalTexture("_CausticsMap", CausticsTex);
    }
}
