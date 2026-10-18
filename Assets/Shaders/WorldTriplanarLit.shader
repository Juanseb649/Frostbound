// Material del mundo jugable (poblado, castillo): PBR de URP con texturas proyectadas por mundo (triplanar), porque
// las piezas son primitivas escaladas y sus UV se estirarían. Conserva el color de cada material (_BaseColor) y usa
// la textura como relieve de luz y sombra (_Detail), con una variación grande por ruido para que no se note la
// repetición y nieve acumulada en las caras que miran arriba. Texturas CC0 de Poly Haven (Assets/Frostbound/Textures).
Shader "Frostbound/WorldTriplanarLit"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _MainTex ("Textura (color + suavidad en alfa)", 2D) = "white" {}
        [NoScaleOffset][Normal] _BumpMap ("Relieve (normal)", 2D) = "bump" {}
        _Tile ("Metros por repetición", Float) = 2
        _Detail ("Fuerza de la textura", Range(0, 1)) = 0.8
        _TexColor ("Color propio de la textura", Range(0, 1)) = 0
        _TexMean ("Luminancia media de la textura", Float) = 0.4
        _NormalScale ("Fuerza del relieve", Range(0, 2)) = 1
        _Smoothness ("Suavidad", Range(0, 1)) = 0.3
        _Metallic ("Metálico", Range(0, 1)) = 0
        _Variation ("Variación grande de tono", Range(0, 0.5)) = 0.12
        [NoScaleOffset] _SnowTex ("Nieve (color + suavidad)", 2D) = "white" {}
        [NoScaleOffset][Normal] _SnowBump ("Nieve (normal)", 2D) = "bump" {}
        _Snow ("Nieve acumulada arriba", Range(0, 1)) = 0
        _SnowColor ("Color de la nieve", Color) = (0.93, 0.96, 1, 1)
        [HDR] _EmissionColor ("Emisión", Color) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _SnowColor, _EmissionColor;
            float _Tile;
            half _Detail, _TexColor, _TexMean, _NormalScale, _Smoothness, _Metallic, _Variation, _Snow;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_SnowTex); SAMPLER(sampler_SnowTex);
            TEXTURE2D(_SnowBump); SAMPLER(sampler_SnowBump);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + 1), f.x), f.y);
            }

            // Mezcla "whiteout" de las tres proyecciones de normal (Ben Golus).
            float3 TriNormal(TEXTURE2D_PARAM(tex, smp), float2 ux, float2 uy, float2 uz, float3 n, float3 w, half scale)
            {
                float3 tx = UnpackNormalScale(SAMPLE_TEXTURE2D(tex, smp, ux), scale);
                float3 ty = UnpackNormalScale(SAMPLE_TEXTURE2D(tex, smp, uy), scale);
                float3 tz = UnpackNormalScale(SAMPLE_TEXTURE2D(tex, smp, uz), scale);
                tx = float3(tx.xy + n.zy, abs(tx.z) * n.x);
                ty = float3(ty.xy + n.xz, abs(ty.z) * n.y);
                tz = float3(tz.xy + n.xy, abs(tz.z) * n.z);
                return normalize(tx.zyx * w.x + ty.xzy * w.y + tz.xyz * w.z);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 n = normalize(i.normalWS);
                float3 w = pow(abs(n), 4);
                w /= (w.x + w.y + w.z);
                float3 p = i.positionWS / max(0.01, _Tile);
                float2 ux = p.zy, uy = p.xz, uz = p.xy;

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, ux) * w.x
                          + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uy) * w.y
                          + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uz) * w.z;
                half lum = dot(tex.rgb, half3(0.299, 0.587, 0.114));
                // La textura da luz y sombra (juntas, vetas) sobre el color del material; _TexColor deja pasar su color.
                half3 shade = lerp(half3(1, 1, 1), (lum / max(0.05, _TexMean)).xxx, _Detail);
                half3 own = tex.rgb / max(0.05, _TexMean) * 0.5;
                half3 albedo = _BaseColor.rgb * lerp(shade, own * 2, _TexColor * _Detail);
                // Variación grande (manchas de varios metros) para romper la repetición.
                half big = Noise(i.positionWS.xz * 0.11) * 0.65 + Noise(i.positionWS.xz * 0.37 + i.positionWS.y * 0.2) * 0.35;
                albedo *= lerp(1 - _Variation, 1 + _Variation, big);
                half smooth = lerp(_Smoothness, tex.a, _Detail * 0.7);
                float3 normalWS = TriNormal(TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), ux, uy, uz, n, w, _NormalScale * _Detail);

                // Nieve en las caras de arriba (con borde irregular).
                half snow = _Snow * smoothstep(0.62, 0.86, n.y + (big - 0.5) * 0.3);
                if (snow > 0.001)
                {
                    float2 su = i.positionWS.xz / 2.5;
                    half4 s = SAMPLE_TEXTURE2D(_SnowTex, sampler_SnowTex, su);
                    half3 snowAlbedo = _SnowColor.rgb * (0.85 + s.rgb * 0.3);
                    float3 sn = UnpackNormalScale(SAMPLE_TEXTURE2D(_SnowBump, sampler_SnowBump, su), 0.6);
                    float3 snowNormal = normalize(float3(sn.x, 1, sn.y));
                    albedo = lerp(albedo, snowAlbedo, snow);
                    smooth = lerp(smooth, 0.45 + s.a * 0.2, snow);
                    normalWS = normalize(lerp(normalWS, snowNormal, snow));
                }

                InputData input = (InputData)0;
                input.positionWS = i.positionWS;
                input.positionCS = i.positionCS;
                input.normalWS = normalWS;
                input.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                input.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                input.fogCoord = i.fogFactor;
                input.bakedGI = SampleSH(normalWS);
                input.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                input.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.metallic = _Metallic;
                surface.smoothness = smooth;
                surface.occlusion = 1;
                surface.emission = _EmissionColor.rgb;
                surface.alpha = 1;
                surface.normalTS = half3(0, 0, 1);

                half4 color = UniversalFragmentPBR(input, surface);
                color.rgb = MixFog(color.rgb, i.fogFactor);
                return half4(color.rgb, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDir = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDir = _LightDirection;
                #endif
                o.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir)));
                return o;
            }

            half4 Frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half Frag(Varyings i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target { return half4(NormalizeNormalPerPixel(i.normalWS), 0); }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
