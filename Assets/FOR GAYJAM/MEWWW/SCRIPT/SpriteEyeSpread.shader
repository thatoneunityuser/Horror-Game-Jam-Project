// URP 2D LIT sprite shader (Unity 6 / URP 17).
// - The SpriteRenderer's sprite (alpha) is the AREA that eyes may spread inside.
// - Eyes are placed on a VORONOI pattern (one eye per cell, clipped to its cell).
// - Eye texture is a separate property, so you can swap eyes without touching the sprite.
// - Everything is lit by 2D lights; optional eye glow ignores lighting.
Shader "Custom/SpriteEyeSpread"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite (Area Mask)", 2D) = "white" {}
        _MaskTex ("Light Mask", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Eye)]
        [NoScaleOffset] _EyeTex ("Eye Texture", 2D) = "white" {}
        _EyeColor ("Eye Color", Color) = (1,1,1,1)
        _EyeGlow ("Eye Glow (ignores light)", Range(0, 5)) = 0

        [Header(Voronoi)]
        _Density ("Cell Density", Range(1, 30)) = 6
        _AngleOffset ("Angle Offset", Float) = 2
        _VoronoiSpeed ("Voronoi Move Speed", Range(0, 5)) = 0.5
        _Seed ("Random Seed", Float) = 0
        _EdgeColor ("Cell Edge Color", Color) = (0,0,0,0)
        _EdgeWidth ("Cell Edge Width", Range(0, 0.5)) = 0.05

        [Header(Spread)]
        _Coverage ("Coverage (chance a cell has an eye)", Range(0, 1)) = 0.7
        _ScaleMin ("Eye Scale Min", Range(0.05, 1.5)) = 0.5
        _ScaleMax ("Eye Scale Max", Range(0.05, 1.5)) = 0.9
        _RandomRotation ("Random Rotation (deg)", Range(0, 180)) = 15

        [Header(Area Limit)]
        _MaskThreshold ("Area Alpha Threshold", Range(0.01, 1)) = 0.5
        [Toggle] _ClipToMask ("Only spawn eyes whose center is inside area", Float) = 1
        [Toggle] _ShowSprite ("Draw sprite under eyes", Float) = 1

        [Header(Blink)]
        _BlinkSpeed ("Blink Speed (0 = off)", Range(0, 2)) = 0.3

        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode"="Universal2D" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"
            #pragma multi_compile_instancing

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4  color      : COLOR;
                float2 uv         : TEXCOORD0;
                half2  lightingUV : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // NOTE: LightingUtility.hlsl and the _ShapeLightTexture0..3 declarations
            // are already provided by ShapeLightShared.hlsl (included above).

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_MaskTex); SAMPLER(sampler_MaskTex);
            TEXTURE2D(_EyeTex);  SAMPLER(sampler_EyeTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_TexelSize;
                half4  _Color;
                half4  _EyeColor;
                half   _EyeGlow;
                float  _Density, _AngleOffset, _VoronoiSpeed, _Seed;
                half4  _EdgeColor;
                float  _EdgeWidth;
                float  _Coverage, _ScaleMin, _ScaleMax, _RandomRotation;
                float  _MaskThreshold, _ClipToMask, _ShowSprite, _BlinkSpeed;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                SetUpSpriteInstanceProperties();
                v.positionOS = UnityFlipSprite(v.positionOS, unity_SpriteProps.xy);
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.uv = v.uv;
                o.lightingUV = half2(ComputeScreenPos(o.positionCS / o.positionCS.w).xy);
                o.color = v.color * _Color * unity_SpriteColor;
                return o;
            }

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // Same random as Shader Graph's Voronoi node
            float2 voronoiRandom(float2 uv, float offset)
            {
                float2x2 m = float2x2(15.27, 47.63, 99.41, 89.98);
                uv = frac(sin(mul(uv, m)) * 46839.32);
                return float2(sin(uv.y * offset) * 0.5 + 0.5, cos(uv.x * offset) * 0.5 + 0.5);
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 sprite = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half4 baseCol = sprite * i.color;
                float inArea = step(_MaskThreshold, sprite.a);

                // ---------- Voronoi ----------
                float aspect = _MainTex_TexelSize.z / max(_MainTex_TexelSize.w, 1.0);
                float2 seedShift = _Seed * 7.31;
                float2 p  = float2(i.uv.x * aspect, i.uv.y) * _Density + seedShift;
                float2 ip = floor(p);
                float2 fp = frac(p);
                float angle = _AngleOffset + _Time.y * _VoronoiSpeed;

                float F1 = 8.0, F2 = 8.0;
                float2 bestCell = 0, bestR = 0, bestO = 0;

                [unroll] for (int y = -1; y <= 1; y++)
                [unroll] for (int x = -1; x <= 1; x++)
                {
                    float2 g = float2(x, y);
                    float2 o = voronoiRandom(ip + g, angle);
                    float2 r = g + o - fp;
                    float d = dot(r, r);
                    if (d < F1) { F2 = F1; F1 = d; bestCell = ip + g; bestR = r; bestO = o; }
                    else if (d < F2) { F2 = d; }
                }
                float edgeDist = sqrt(F2) - sqrt(F1);

                // Cell edges (drawn on the sprite)
                float edgeMask = 1.0 - smoothstep(_EdgeWidth, _EdgeWidth + 0.02, edgeDist);
                baseCol.rgb = lerp(baseCol.rgb, _EdgeColor.rgb, edgeMask * _EdgeColor.a);

                // ---------- Eye inside its voronoi cell ----------
                half4 eye = 0;
                bool spawn = hash21(bestCell + 17.1) <= _Coverage;

                if (spawn && _ClipToMask > 0.5)
                {
                    float2 pt = bestCell + bestO - seedShift;
                    float2 ptUV = float2(pt.x / (_Density * aspect), pt.y / _Density);
                    spawn = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, ptUV, 0).a >= _MaskThreshold;
                }

                if (spawn)
                {
                    float scale = lerp(_ScaleMin, _ScaleMax, hash21(bestCell + 3.3));
                    float ang = (hash21(bestCell + 7.7) - 0.5) * 2.0 * radians(_RandomRotation);
                    float sn = sin(ang), cs = cos(ang);

                    float2 d = -bestR; // fragment relative to the cell's point
                    d = float2(cs * d.x - sn * d.y, sn * d.x + cs * d.y);
                    float2 euv = d / scale + 0.5;

                    if (_BlinkSpeed > 0)
                    {
                        float t = frac(_Time.y * _BlinkSpeed + hash21(bestCell + 9.1));
                        float open = t > 0.95 ? abs(t - 0.975) / 0.025 : 1.0;
                        euv.y = (euv.y - 0.5) / max(open, 0.001) + 0.5;
                    }

                    if (all(euv >= 0) && all(euv <= 1))
                    {
                        eye = SAMPLE_TEXTURE2D_LOD(_EyeTex, sampler_EyeTex, euv, 0) * _EyeColor;
                    }
                }

                // Hard limit: never outside the sprite area
                eye.a *= inArea * i.color.a;
                baseCol.a *= _ShowSprite;

                // Straight-alpha "over" composite
                half outA = eye.a + baseCol.a * (1 - eye.a);
                half3 outRGB = (eye.rgb * eye.a + baseCol.rgb * baseCol.a * (1 - eye.a)) / max(outA, 1e-4);

                // ---------- 2D Lighting ----------
                const half4 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, i.uv);
                SurfaceData2D surfaceData;
                InputData2D inputData;
                InitializeSurfaceData(outRGB, outA, mask, surfaceData);
                InitializeInputData(i.uv, i.lightingUV, inputData);
                half4 lit = CombinedShapeLightShared(surfaceData, inputData);

                // Glowing eyes (visible even in darkness)
                lit.rgb += eye.rgb * eye.a * _EyeGlow;
                return lit;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/2D/Sprite-Lit-Default"
}
