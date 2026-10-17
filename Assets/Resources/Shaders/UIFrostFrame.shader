// Marco de UI del "gótico de escarcha" (guía /d2r-ui). Igual que UI/Default (máscaras, recorte y alfa de uGUI)
// más dos efectos sobre el metal del marco:
//   - Destello: una franja de luz que cruza el marco en diagonal (_SheenPos va de -0,4 a 1,4).
//   - Escarcha: un velo helado que entra desde el borde, con algún cristal suelto (_Frost de 0 a 1).
//   - Disolución: el ruido se come la imagen a manchas con un borde helado brillante (_Dissolve de 0 a 1,2);
//     la usa UIDissolve al cerrar ventanas.
// _Rect es el rectángulo de la imagen en el espacio del Canvas raíz (x, y, ancho, alto), que es el espacio de los
// vértices que recibe el shader; lo pone el script cada fotograma.
// Está en Resources para que entre en la build (se carga con Resources.Load).
Shader "Frostbound/UI Frost Frame"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tinte", Color) = (1,1,1,1)

        _Rect ("Rectángulo local", Vector) = (0,0,100,100)
        _SheenPos ("Posición del destello", Float) = -1
        _SheenWidth ("Ancho del destello", Float) = 0.22
        _SheenColor ("Color del destello", Color) = (1, 0.92, 0.7, 1.2)
        _Frost ("Escarcha", Range(0,1)) = 0
        _FrostColor ("Color de la escarcha", Color) = (0.82, 0.94, 1, 1)
        _FrostPixels ("Alcance de la escarcha (px)", Float) = 5
        _Dissolve ("Disolución", Range(0,1.2)) = 0
        _DissolveEdge ("Borde de la disolución", Color) = (0.75, 0.92, 1, 1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float2 local : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float4 _Rect;
            float _SheenPos, _SheenWidth, _Frost, _FrostPixels, _Dissolve;
            fixed4 _SheenColor, _FrostColor, _DissolveEdge;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.local = v.vertex.xy;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash(i), b = Hash(i + float2(1, 0)), c = Hash(i + float2(0, 1)), d = Hash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                // Posición dentro de la imagen: normalizada (0..1) y en píxeles hasta el borde más cercano.
                float2 size = max(_Rect.zw, 1.0);
                float2 n = (IN.local - _Rect.xy) / size;
                float2 px = IN.local - _Rect.xy;
                float edge = min(min(px.x, size.x - px.x), min(px.y, size.y - px.y));

                // Destello diagonal: solo sobre el metal (donde el sprite es opaco).
                float band = saturate(1.0 - abs(n.x + n.y * 0.35 - _SheenPos) / max(_SheenWidth, 0.001));
                color.rgb += _SheenColor.rgb * _SheenColor.a * band * band;

                // Escarcha: motas en celdas de 3 px que aparecen más cerca del borde y crecen con _Frost.
                // Velo helado que entra desde el borde y unos pocos destellos sueltos (cristales).
                float reach = max(_Frost * _FrostPixels, 0.001);
                float veil = saturate(1.0 - edge / reach) * _Frost;
                float sparkle = step(0.94, Hash(floor(IN.local / 2.0))) * veil;
                color.rgb = lerp(color.rgb, _FrostColor.rgb, veil * 0.3 + sparkle * 0.6);

                // Disolución: manchas grandes con detalle fino; justo delante del corte, un borde helado que brilla.
                if (_Dissolve > 0.0)
                {
                    float noise = ValueNoise(IN.local / 46.0) * 0.65 + ValueNoise(IN.local / 11.0) * 0.35;
                    float rim = saturate(1.0 - (noise - _Dissolve) / 0.05);
                    color.rgb = lerp(color.rgb, _DissolveEdge.rgb * 1.25, rim * step(_Dissolve, noise));
                    color.a *= step(_Dissolve, noise);
                }

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
