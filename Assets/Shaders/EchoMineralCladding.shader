Shader "EchoRun/MineralCladding"
{
    Properties
    {
        _Color ("Mineral tint", Color) = (0.75,0.78,0.8,1)
        _MainTex ("Authored panel albedo", 2D) = "white" {}
        _PanelScale ("Tiles per metre", Float) = 0.18
        _Glossiness ("Smoothness", Range(0,1)) = 0.24
        _Metallic ("Metallic", Range(0,1)) = 0.04
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color;
        half _PanelScale, _Glossiness, _Metallic;
        struct Input { float3 worldPos; float3 worldNormal; };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            // World-scale projection keeps the authored tile consistent across
            // imported facade meshes whose UV scale varies between modules.
            float3 weights = pow(abs(IN.worldNormal), 8);
            weights /= max(dot(weights, float3(1,1,1)), 0.0001);
            float3 p = IN.worldPos * _PanelScale;
            fixed3 tile = tex2D(_MainTex, p.zy).rgb * weights.x
                        + tex2D(_MainTex, p.xz).rgb * weights.y
                        + tex2D(_MainTex, p.xy).rgb * weights.z;
            o.Albedo = tile * _Color.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
