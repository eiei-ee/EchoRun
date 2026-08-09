Shader "EchoRun/GhostRunner"
{
    Properties
    {
        _Color ("Ghost Color", Color) = (0.16, 0.68, 0.74, 0.56)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 120
        Cull Back
        ZWrite Off

        CGPROGRAM
        #pragma surface surf Lambert alpha:fade noforwardadd
        #pragma target 2.0

        fixed4 _Color;

        struct Input
        {
            float3 worldPos;
        };

        void surf(Input input, inout SurfaceOutput output)
        {
            output.Albedo = _Color.rgb;
            output.Emission = _Color.rgb * 0.22;
            output.Alpha = _Color.a;
        }
        ENDCG
    }

    FallBack "Unlit/Transparent"
}
