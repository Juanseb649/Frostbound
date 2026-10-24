// Hielo cristalino como cuarzo (cristales del castillo, del camino, de la montaña y de la cumbre).
// Opaco a propósito (sin transparencia ni refracción: ni problemas de orden ni coste extra) pero con aspecto de
// volumen: cuerpo lechoso más denso en el centro de cada cara, cantos más claros por Fresnel (la luz que atraviesa el
// borde del cuarzo), una capa interior desplazada según la vista que da profundidad, vetas y fracturas finas,
// inclusiones y un relieve leve. PBR de URP con sombras y luces adicionales.
Shader "Frostbound/QuartzCrystal"
{
    Properties
    {
        _BaseColor ("Cuarzo", Color) = (0.86, 0.87, 0.88, 1)
        _DepthColor ("Interior (lechoso)", Color) = (0.62, 0.64, 0.68, 1)
        _VeinColor ("Vetas y fracturas", Color) = (0.97, 0.97, 0.96, 1)
        _Veins ("Fuerza de las vetas", Range(0, 1)) = 0.55
        _VeinScale ("Escala de las vetas", Float) = 3.2
        _Smoothness ("Suavidad", Range(0, 1)) = 0.88
        _Fresnel ("Cantos claros", Range(0, 2)) = 0.9
        _Depth ("Profundidad interior", Range(0, 1)) = 0.35
        _Bump ("Imperfecciones", Range(0, 1)) = 0.25
        _Glow ("Brillo propio", Range(0, 1)) = 0.06
        _GlowColor ("Color del brillo", Color) = (0.92, 0.95, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _DepthColor, _VeinColor, _GlowColor;
            half _Veins, _Smoothness, _Fresnel, _Depth, _Bump, _Glow;
            float _VeinScale;
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
                half fogFactor : TEXCOORD3;
                float seed : TEXCOORD4;
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
                // Cada cristal, sus propias vetas: semilla según su posición en el mundo (estable entre fotogramas).
                float3 origin = TransformObjectToWorld(float3(0, 0, 0));
                o.seed = frac(sin(dot(origin, float3(12.9898, 78.233, 37.719))) * 43758.5453) * 50;
                // Vetas en espacio de objeto escalado (no se estiran con la forma del cristal).
                float3 scale = float3(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m01_m11_m21), length(UNITY_MATRIX_M._m02_m12_m22));
                o.positionOS = v.positionOS.xyz * scale;
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
            float Fbm(float3 p) { return Noise3(p) * 0.55 + Noise3(p * 2.1 + 7.3) * 0.3 + Noise3(p * 4.3 + 1.7) * 0.15; }

            // Vetas finas: líneas donde el ruido cruza un valor (fracturas que atraviesan el cristal).
            half Veins(float3 p)
            {
                float n = Fbm(p);
                float v1 = 1 - smoothstep(0.0, 0.035, abs(n - 0.5));
                float n2 = Noise3(p * 1.7 + 11.0);
                float v2 = 1 - smoothstep(0.0, 0.02, abs(n2 - 0.45));
                return saturate(v1 * 0.8 + v2 * 0.5);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 n = normalize(i.normalWS);
                float3 viewWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float3 viewOS = TransformWorldToObjectDir(viewWS);
                half ndv = saturate(dot(n, viewWS));
                float3 p = i.positionOS * _VeinScale + i.seed;

                // Superficie y capa interior (desplazada hacia dentro según la vista): dos juegos de vetas a distinta
                // "profundidad" dan sensación de volumen sin transparencia.
                half surfaceVeins = Veins(p);
                half innerVeins = Veins(p - viewOS * (_Depth * 1.6) + 3.1);
                half cloud = Fbm(p * 0.6 - viewOS * _Depth);
                // Cuerpo: lechoso en el centro de la cara (mirando de frente se ve "a través" hacia el interior más
                // denso), más claro y frío en los cantos.
                half3 body = lerp(_DepthColor.rgb, _BaseColor.rgb, saturate(0.45 + cloud * 0.6));
                body = lerp(body, _DepthColor.rgb * 0.92, (1 - ndv) * 0.15);
                half3 albedo = lerp(body, _VeinColor.rgb, innerVeins * _Veins * 0.55);
                albedo = lerp(albedo, _VeinColor.rgb, surfaceVeins * _Veins * 0.35);
                // Inclusiones diminutas y una variación de tono apenas cálida/gris.
                half incl = step(0.93, Noise3(p * 6.0 + 5.0));
                albedo *= 1 - incl * 0.25;
                albedo *= lerp(half3(1.02, 1.0, 0.97), half3(0.97, 0.99, 1.02), Noise3(p * 0.35));

                // Imperfecciones: relieve con el gradiente del ruido y suavidad que varía (zonas pulidas y mates).
                float e = 0.15;
                float3 g = float3(Fbm(p + float3(e, 0, 0)), Fbm(p + float3(0, e, 0)), Fbm(p + float3(0, 0, e))) - Fbm(p);
                float3 bumpN = normalize(n - TransformObjectToWorldDir(g) * (_Bump * 2.5));
                half smooth = saturate(_Smoothness - (Noise3(p * 1.3) - 0.5) * 0.18 - surfaceVeins * 0.12);

                InputData input = (InputData)0;
                input.positionWS = i.positionWS;
                input.positionCS = i.positionCS;
                input.normalWS = bumpN;
                input.viewDirectionWS = viewWS;
                input.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                input.fogCoord = i.fogFactor;
                input.bakedGI = SampleSH(bumpN);
                input.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                input.shadowMask = half4(1, 1, 1, 1);

                // Cantos: la luz que atraviesa el borde del cristal (Fresnel) y un brillo propio muy tenue.
                half rim = pow(1 - ndv, 3) * _Fresnel;
                Light main = GetMainLight();
                half3 transmit = main.color * saturate(dot(-n, main.direction) * 0.5 + 0.5) * rim * 0.35;
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.metallic = 0;
                surface.smoothness = smooth;
                surface.occlusion = 1;
                surface.emission = _GlowColor.rgb * (_Glow * (0.6 + cloud * 0.8)) + transmit + _VeinColor.rgb * innerVeins * _Glow * 0.6;
                surface.alpha = 1;
                surface.normalTS = half3(0, 0, 1);

                half4 color = UniversalFragmentPBR(input, surface);
                color.rgb += rim * 0.12 * SampleSH(n);
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
