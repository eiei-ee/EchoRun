Shader "EchoRun/GhostRunner"
{
    Properties
    {
        _MainTex ("Source Texture", 2D) = "white" {}
        _Color ("Ghost Color", Color) = (0.22, 0.84, 1.00, 0.66)
        _RimColor ("Rim Color", Color) = (0.64, 0.94, 1.00, 1)
        _RimPower ("Rim Power", Range(0.7, 5)) = 2.1
        _EmissionStrength ("Emission Strength", Range(0, 4)) = 0.72
        _ScanStrength ("Scan Strength", Range(0, 1)) = 0.22
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

        sampler2D _MainTex;
        fixed4 _Color;
        fixed4 _RimColor;
        half _RimPower;
        half _EmissionStrength;
        half _ScanStrength;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float3 viewDir;
        };

        void surf(Input input, inout SurfaceOutput output)
        {
            fixed3 source = tex2D(_MainTex, input.uv_MainTex).rgb;
            half textureValue = dot(source, fixed3(0.30, 0.59, 0.11));
            half rim = pow(1.0h - saturate(dot(normalize(input.viewDir), output.Normal)),
                _RimPower);
            half scanWave = 0.5h + 0.5h * sin((input.worldPos.y + _Time.y * 0.85h) * 24.0h);
            half scan = step(0.82h, scanWave) * _ScanStrength;
            half detail = lerp(0.62h, 1.10h, textureValue);

            output.Albedo = _Color.rgb * detail;
            output.Emission = _Color.rgb * (_EmissionStrength + scan)
                + _RimColor.rgb * rim * 1.45h;
            output.Alpha = saturate(_Color.a * (0.76h + rim * 0.48h) + scan * 0.14h);
        }
        ENDCG
    }

    FallBack "Unlit/Transparent"
}
