// Vertex-coloured voxels: chunk meshes carry colour per vertex (with baked corner occlusion), instanced
// fragments per instance (_BaseColor). Lighting follows PixelLand's toon cube material (TCP2 settings):
// wrapped N.L through a ramp step at _RampThreshold, a small specular highlight, hemisphere ambient,
// URP main-light shadows, then fog. Faces that carry uv0 (position along the face) and uv2 (face size)
// get PixelLand's rounded "pillow" face texture (_FaceMap) and/or a dark line at each cube edge;
// meshes without uv2 render plain.
Shader "VoxelSmash/VoxelLit"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _SkyColor ("Ambient Sky", Color) = (0.79,0.89,1,1)
        _GroundColor ("Ambient Ground", Color) = (0.27,0.22,0.17,1)
        _Ambient ("Ambient Strength", Range(0,2)) = 0.75
        _EdgeWidth ("Edge Width (uv units)", Float) = 0.012
        _EdgeDark ("Edge Darkening", Range(0,1)) = 0.28
        _FaceMap ("Face Texture (one cube face)", 2D) = "white" {}
        _FaceStrength ("Face Texture Strength", Range(0,1)) = 0
        _RampThreshold ("Ramp Threshold", Range(0,1)) = 0.6
        _RampSmooth ("Ramp Smoothing", Range(0.01,1)) = 0.2
        _ShadowTint ("Shadow Side Tint", Color) = (0.62,0.66,0.8,1)
        _SpecStrength ("Specular Strength", Range(0,1)) = 0.26
        _SpecPower ("Specular Power", Float) = 19.6
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _SkyColor;
                half4 _GroundColor;
                half _Ambient;
                float _EdgeWidth;
                half _EdgeDark;
                half _FaceStrength;
                half _RampThreshold;
                half _RampSmooth;
                half4 _ShadowTint;
                half _SpecStrength;
                half _SpecPower;
                float4 _FaceMap_ST;
            CBUFFER_END

            TEXTURE2D(_FaceMap);
            SAMPLER(sampler_FaceMap);

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 faceSize : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float4 face : TEXCOORD2;
                half fog : TEXCOORD3;
                half4 color : COLOR;
            };

            Varyings vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.face = float4(v.uv, v.faceSize);
                o.fog = ComputeFogFactor(p.positionCS.z);
                o.color = v.color * UNITY_ACCESS_INSTANCED_PROP(Props, _BaseColor);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 n = normalize(i.normalWS);
                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                // Toon ramp on wrapped lighting: a soft step between the lit and the shadowed side.
                half wrapped = dot(n, light.direction) * .5h + .5h;
                half ramp = smoothstep(_RampThreshold - _RampSmooth * .5h, _RampThreshold + _RampSmooth * .5h, wrapped);
                ramp *= light.shadowAttenuation * light.distanceAttenuation;
                half3 ambient = lerp(_GroundColor.rgb, _SkyColor.rgb, n.y * .5h + .5h) * _Ambient;
                half3 lit = lerp(ambient * _ShadowTint.rgb, light.color + ambient * .35h, ramp);
                half3 view = normalize(GetWorldSpaceViewDir(i.positionWS));
                half spec = pow(saturate(dot(n, normalize(light.direction + view))), _SpecPower) * _SpecStrength * ramp;
                half3 albedo = i.color.rgb;
                if (i.face.z > 0)
                {
                    float2 uv = i.face.xy / i.face.zw;
                    albedo *= lerp(1, SAMPLE_TEXTURE2D(_FaceMap, sampler_FaceMap, uv).rgb, _FaceStrength);
                    float2 d = min(i.face.xy, i.face.zw - i.face.xy);
                    float w = max(_EdgeWidth, fwidth(i.face.x) * 1.2);
                    half edge = smoothstep(0, w, min(d.x, d.y));
                    albedo *= lerp(1 - _EdgeDark, 1, edge);
                }
                return half4(MixFog(albedo * lit + spec * light.color, i.fog), 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
