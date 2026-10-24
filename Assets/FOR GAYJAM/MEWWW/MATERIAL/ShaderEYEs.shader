
Shader "Custom/2D/ShaderEYEs_Lit"
{
    Properties
    {
        [PerRendererData] _MainTex ("Eye Texture (รูปดวงตา)", 2D) = "white" {}
        [MainColor] _TintColor ("Tint Color (สีคูณเพิ่ม)", Color) = (1, 1, 1, 1)
        _CellDensity ("Cell Density (ความถี่ของก้อน)", Float) = 5.47
        _Speed ("Speed (ความเร็วการขยับ)", Float) = 2.0
        _EyeScale ("Eye Scale (ขนาดลูกตาในเซลล์)", Range(0.5, 3.0)) = 1.2

        [Header(Glow In The Dark)]
        _GlowColor ("Glow Color (สีเรืองแสง)", Color) = (1, 1, 1, 1)
        _GlowIntensity ("Glow Intensity (ความสว่างเรืองแสง)", Range(0, 5)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        // แก้ปัญหา Sorting Layer / Order in Layer ของ Sprite Renderer
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
        

            // คีย์เวิร์ดสำหรับคำนวณแสง 2D Lights
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_0 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_1 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_2 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_3 __

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/SurfaceData2D.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/InputData2D.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"


            struct Attributes
            {
                float3 positionOS   : POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                half4  color        : COLOR;
                float2 uv           : TEXCOORD0;
                float4 screenPos    : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _TintColor;
                float4 _GlowColor;
                float _CellDensity;
                float _Speed;
                float _EyeScale;
                float _GlowIntensity;
            CBUFFER_END

            // ฟังก์ชันสุ่มค่าประจำแต่ละเซลล์ Voronoi
            inline float2 hash22(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.uv = input.uv;
                output.color = input.color;

              

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv * _CellDensity;
                float2 g = floor(uv);
                float2 f = frac(uv);

                float minDist = 8.0;
                float2 minOffset = float2(0.0, 0.0);
                float angleOffset = _Time.y * _Speed;

                // คำนวณหาจุดศูนย์กลางของเซลล์ Voronoi ที่ใกล้ที่สุด 3x3
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 lattice = float2(x, y);
                        float2 rand = hash22(g + lattice);

                        // หมุนจุดศูนย์กลางตามเวลา (ดวงตาดิ้น/ขยับ)
                        float2 offset = float2(sin(rand.x * angleOffset), cos(rand.y * angleOffset)) * 0.5 + 0.5;

                        float2 r = lattice + offset - f;
                        float d = dot(r, r);

                        if (d < minDist)
                        {
                            minDist = d;
                            minOffset = r; // เก็บเวกเตอร์ชี้จากจุดศูนย์กลางเซลล์มายังพิกเซลปัจจุบัน
                        }
                    }
                }

                // แปลงระยะห่างจากจุดกึ่งกลางเซลล์ให้เป็น UV ย่อย สำหรับแปะรูปดวงตา
                float2 cellUV = saturate(minOffset * _EyeScale + 0.5);

                // สร้าง Mask ขอบดำรอบเซลล์
                float mask = saturate(1.0 - sqrt(minDist));

                // ดึงสีจากรูปดวงตาด้วย UV ประจำเซลล์
                half4 eyeTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, cellUV);

                // รวมสีตา + Mask + Tint + สี SpriteRenderer (input.color)
                half4 baseColor = eyeTex * mask * _TintColor * input.color;

                // คำนวณแสง URP 2D (Global Light 2D, Point Light 2D, ฯลฯ)
                SurfaceData2D surfaceData;
                InputData2D inputData;

                half4 maskTex = half4(1.0, 1.0, 1.0, 1.0);
                InitializeSurfaceData(baseColor.rgb, baseColor.a, maskTex, surfaceData);

                float2 lightingUV = input.screenPos.xy / input.screenPos.w;
                InitializeInputData(input.uv, lightingUV, inputData);

                // รวมแสง 2D
                half4 finalColor = CombinedShapeLightShared(surfaceData, inputData);

                // แสงเรืองตาในที่มืด (ถ้าตั้ง _GlowIntensity > 0)
                half3 emission = _GlowColor.rgb * _GlowIntensity * eyeTex.a * mask;
                finalColor.rgb += emission;

                return finalColor;
            }
            ENDHLSL
        }
    }
    FallBack "Sprites/Default"
}