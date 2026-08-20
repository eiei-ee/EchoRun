Shader "EchoRun/Collectible"
{
    Properties
    {
        _RingColor ("Ring", Color) = (0.94, 0.68, 0.24, 1)
        _CoreColor ("Core", Color) = (0.12, 0.82, 1.0, 1)
        _ContractColor ("Contract", Color) = (1.0, 0.34, 0.30, 1)
        _EmissionStrength ("Emission", Range(0, 4)) = 1.35
        _ContractMarker ("Contract Marker", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 180
        Cull Off

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 2.0
        #pragma multi_compile_instancing

        fixed4 _RingColor;
        fixed4 _CoreColor;
        fixed4 _ContractColor;
        half _EmissionStrength;
        half _ContractMarker;
        half _EchoVisualHigh;
        fixed4 _EchoPhaseTint;
        half _EchoPhaseIntensity;

        struct Input
        {
            float4 color : COLOR;
            float3 viewDir;
        };

        void surf(Input input, inout SurfaceOutputStandard output)
        {
            half ringWeight = saturate(input.color.r);
            half coreWeight = saturate(input.color.g);
            fixed3 ring = lerp(_RingColor.rgb, _ContractColor.rgb,
                saturate(_ContractMarker));
            fixed3 color = ring * ringWeight + _CoreColor.rgb * coreWeight;
            fixed3 phase = lerp(fixed3(1, 1, 1), _EchoPhaseTint.rgb,
                saturate(_EchoPhaseIntensity) * 0.12h);
            color *= phase;
            half fresnel = pow(1.0h - saturate(dot(normalize(input.viewDir),
                output.Normal)), 3.0h) * saturate(_EchoVisualHigh);
            output.Albedo = color * (0.72h + fresnel * 0.18h);
            output.Metallic = lerp(0.18h, 0.48h, ringWeight);
            output.Smoothness = lerp(0.42h, 0.76h,
                saturate(_EchoVisualHigh));
            output.Emission = color * (_EmissionStrength
                * (coreWeight * 0.82h + ringWeight * 0.28h + fresnel * 0.24h));
            output.Alpha = 1.0h;
        }
        ENDCG
    }
    FallBack "Mobile/Diffuse"
}
