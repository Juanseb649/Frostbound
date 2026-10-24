// Estatua del pingüino fundador (fuente del poblado). El modelo (Assets/Penguin.obj) es una sola malla sin textura,
// así que los colores se pintan por zonas en espacio de objeto (Y arriba, de -0.5 a 0.5; el pico mira a +Z):
// espalda, cabeza y aletas azul noche; pecho y cara blancos; pico y patas naranjas; ojos. Encima, plumas (ruido
// estirado a lo largo de Y que varía el tono y ondula la normal) y escarcha en las caras que miran arriba.
// PBR de URP con sombras, luces adicionales y niebla.
Shader "Frostbound/PenguinStatue"
{
    Properties
    {
        _BackColor ("Espalda y cabeza", Color) = (0.07, 0.09, 0.15, 1)
        _BellyColor ("Pecho y cara", Color) = (0.95, 0.94, 0.9, 1)
        _BeakColor ("Pico y patas", Color) = (0.96, 0.5, 0.1, 1)
        _EyeColor ("Ojos", Color) = (0.02, 0.02, 0.03, 1)
        _BellyW ("Pecho: ancho", Float) = 0.3
        _BellyH ("Pecho: alto", Float) = 0.4
        _BellyY ("Pecho: centro en Y", Float) = -0.05
        _BellyFront ("Pecho: cuánto mira al frente", Range(-1, 1)) = 0.05
        _BeakMin ("Pico: Y mínima", Float) = 0.1
        _BeakMax ("Pico: Y máxima", Float) = 0.3
        _BeakZ ("Pico: Z mínima", Float) = 0.21
        _FeetY ("Patas: Y máxima", Float) = -0.4
        _EyePos ("Ojo derecho (X, Y, Z)", Vector) = (0.032, 0.322, 0.19, 0)
        _EyeSize ("Tamaño de los ojos (0 = sin ojos)", Float) = 0.024
        _Feathers ("Fuerza de las plumas", Range(0, 1)) = 0.5
        _FeatherScale ("Escala de las plumas", Float) = 38
        _Frost ("Escarcha arriba", Range(0, 1)) = 0.45
        _FrostColor ("Color de la escarcha", Color) = (0.9, 0.95, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BackColor, _BellyColor, _BeakColor, _EyeColor, _FrostColor;
            float4 _EyePos;
            float _BellyW, _BellyH, _BellyY, _BellyFront, _BeakMin, _BeakMax, _BeakZ, _FeetY, _EyeSize, _FeatherScale;
            half _Feathers, _Frost;
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
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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
                float3 positionOS : TEXCOORD2;
                float3 normalOS : TEXCOORD3;
                half fogFactor : TEXCOORD4;
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
                o.positionOS = v.positionOS.xyz;
                o.normalOS = v.normalOS;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float Hash3(float3 p) { return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453); }
            float Noise3(float3 p)
            {
                float3 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                float a = lerp(lerp(Hash3(i), Hash3(i + float3(1, 0, 0)), f.x), lerp(Hash3(i + float3(0, 1, 0)), Hash3(i + float3(1, 1, 0)), f.x), f.y);
                float b = lerp(lerp(Hash3(i + float3(0, 0, 1)), Hash3(i + float3(1, 0, 1)), f.x), lerp(Hash3(i + float3(0, 1, 1)), Hash3(i + 1), f.x), f.y);
                return lerp(a, b, f.z);
            }

            // Plumas: celdas alargadas hacia abajo (más finas en el pecho que en la espalda).
            float Feathers(float3 p)
            {
                float3 q = p * float3(1, 0.35, 1) * _FeatherScale;
                return Noise3(q) * 0.65 + Noise3(q * 2.3 + 5.1) * 0.35;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 p = i.positionOS;
                float3 nOS = normalize(i.normalOS);
                float3 n = normalize(i.normalWS);
                float3 viewWS = GetWorldSpaceNormalizeViewDir(i.positionWS);

                // ----- Zonas
                float2 e = float2(p.x / _BellyW, (p.y - _BellyY) / _BellyH);
                half belly = (1 - smoothstep(0.85, 1.0, length(e))) * smoothstep(_BellyFront - 0.15, _BellyFront + 0.15, nOS.z) * step(0, p.z);
                half beak = smoothstep(_BeakZ - 0.015, _BeakZ + 0.015, p.z) * smoothstep(_BeakMin - 0.02, _BeakMin, p.y) * (1 - smoothstep(_BeakMax, _BeakMax + 0.02, p.y));
                half feet = 1 - smoothstep(_FeetY - 0.015, _FeetY + 0.015, p.y);
                float3 eyeR = _EyePos.xyz, eyeL = float3(-_EyePos.x, _EyePos.y, _EyePos.z);
                float de = min(distance(p, eyeR), distance(p, eyeL));
                half eye = _EyeSize > 0 ? 1 - smoothstep(_EyeSize * 0.8, _EyeSize, de) : 0;
                half glint = _EyeSize > 0 ? 1 - smoothstep(_EyeSize * 0.2, _EyeSize * 0.35, min(distance(p, eyeR + float3(0.006, 0.007, 0.006)), distance(p, eyeL + float3(-0.006, 0.007, 0.006)))) : 0;

                half3 albedo = _BackColor.rgb;
                albedo = lerp(albedo, _BellyColor.rgb, belly);
                half orange = saturate(beak + feet);
                albedo = lerp(albedo, _BeakColor.rgb, orange);
                albedo = lerp(albedo, _EyeColor.rgb, eye);
                albedo = lerp(albedo, half3(1, 1, 1), glint * 0.9);

                // ----- Plumas (no en el pico, las patas ni los ojos)
                half plume = (1 - orange) * (1 - eye);
                float f = Feathers(p);
                albedo *= lerp(1, 0.7 + f * 0.6, _Feathers * plume);
                float fe = 0.01;
                float3 g = float3(Feathers(p + float3(fe, 0, 0)), Feathers(p + float3(0, fe, 0)), Feathers(p + float3(0, 0, fe))) - f;
                float3 bump = normalize(n - TransformObjectToWorldDir(g / fe) * (0.009 * _Feathers * plume));

                // ----- Escarcha en lo que mira arriba (cabeza, hombros, patas), salpicada con ruido
                half frost = _Frost * smoothstep(0.45, 0.85, n.y + (Noise3(p * 14) - 0.5) * 0.5);
                albedo = lerp(albedo, _FrostColor.rgb, frost);

                half smooth = lerp(0.42, 0.3, belly);
                smooth = lerp(smooth, 0.62, orange);
                smooth = lerp(smooth, 0.85, eye);
                smooth = lerp(smooth, 0.25, frost);
                smooth -= (f - 0.5) * 0.15 * plume;

                InputData input = (InputData)0;
                input.positionWS = i.positionWS;
                input.positionCS = i.positionCS;
                input.normalWS = bump;
                input.viewDirectionWS = viewWS;
                input.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                input.fogCoord = i.fogFactor;
                input.bakedGI = SampleSH(bump);
                input.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                input.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.metallic = 0;
                surface.smoothness = saturate(smooth);
                surface.occlusion = 1;
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
