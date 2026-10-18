// Cordillera del fondo del menú (HauntedManor): montañas lejanas que la niebla de la escena borraría.
// Luz de la luna (luz principal) en Lambert suave, nieve en las pendientes tendidas y roca en las paredes,
// y una bruma propia: la base se funde con el color de la niebla (unity_FogColor, que sigue al destello del Frost)
// y _Haze aclara cada capa según lo lejos que está. No usa la niebla de Unity.
Shader "Frostbound/MountainBackdrop"
{
    Properties
    {
        _Snow ("Nieve", Color) = (0.78, 0.86, 1, 1)
        _Rock ("Roca", Color) = (0.16, 0.2, 0.3, 1)
        _Haze ("Bruma de la capa (0-1)", Range(0, 1)) = 0.35
        _HazeColor ("Tinte de la bruma", Color) = (0.32, 0.42, 0.62, 1)
        _Base ("Altura de la base (mundo)", Float) = 0
        _Top ("Altura de las cumbres (mundo)", Float) = 60
        _Emission ("Brillo propio", Color) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Snow, _Rock, _HazeColor, _Emission;
                half _Haze;
                float _Base, _Top;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            // Ruido barato por posición para romper la línea de nieve.
            float Hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + 1), f.x), f.y);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float h = saturate((i.positionWS.y - _Base) / max(1, _Top - _Base));
                float wobble = Noise(i.positionWS.xz * 0.08) * 0.35 + Noise(i.positionWS.xz * 0.3) * 0.15;
                // Nieve en lo tendido y en lo alto; roca en las paredes empinadas y abajo.
                float snow = smoothstep(0.35, 0.6, n.y + h * 0.35 + wobble - 0.2);
                half3 albedo = lerp(_Rock.rgb, _Snow.rgb, snow);

                Light moon = GetMainLight();
                half lambert = saturate(dot(n, moon.direction)) * 0.75 + 0.25;
                half3 lit = albedo * (moon.color * lambert * 0.9 + _HazeColor.rgb * 0.55) + _Emission.rgb;

                // Bruma: más espesa en la base (se funde con la niebla del valle) y según la capa.
                half3 fogTint = lerp(unity_FogColor.rgb, _HazeColor.rgb, 0.5);
                half mist = saturate(_Haze + (1 - h) * (1 - h) * (1 - h) * 0.7);
                return half4(lerp(lit, fogTint, mist), 1);
            }
            ENDHLSL
        }
    }
}
