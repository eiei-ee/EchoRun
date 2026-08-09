Shader "EchoRun/ExoGrayBlueTech"
{
    Properties
    {
        _MainTex ("Base Texture", 2D) = "white" {}
        _DarkColor ("Dark Blue Gray", Color) = (0.035, 0.075, 0.12, 1)
        _LightColor ("Light Blue Gray", Color) = (0.32, 0.46, 0.60, 1)
        [HDR] _EmissionColor ("Cyan Emission", Color) = (0, 1.3, 2.1, 1)
        _EmissionStrength ("Emission Strength", Range(0, 4)) = 1.6
        _AccentThreshold ("Warm Accent Threshold", Range(0, 0.5)) = 0.08
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 180

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 2.0

        sampler2D _MainTex;
        fixed4 _DarkColor;
        fixed4 _LightColor;
        fixed4 _EmissionColor;
        half _EmissionStrength;
        half _AccentThreshold;

        struct Input
        {
            float2 uv_MainTex;
        };

        void surf(Input input, inout SurfaceOutputStandard output)
        {
            fixed3 source = tex2D(_MainTex, input.uv_MainTex).rgb;
            half luminance = dot(source, half3(0.299, 0.587, 0.114));
            half warmAccent = saturate((source.r - max(source.g, source.b) - _AccentThreshold) * 5.0);

            fixed3 blueGray = lerp(_DarkColor.rgb, _LightColor.rgb, luminance);
            output.Albedo = lerp(blueGray, _EmissionColor.rgb * 0.16, warmAccent);
            output.Metallic = 0.22;
            output.Smoothness = 0.38;
            output.Emission = _EmissionColor.rgb * warmAccent * _EmissionStrength;
            output.Alpha = 1.0;
        }
        ENDCG
    }

    FallBack "Mobile/Diffuse"
}
