// 3D-гонщик в 2D-сцене (URP 2D Renderer): собственный мягкий свет (не зависит от 2D-ламп), блик, контур-подсветка,
// «хром» по отражению градиента неба. Глубина пишется в дальний край буфера: модель правильно перекрывает сама себя,
// но не закрывает спрайты, нарисованные после неё (передний план, ближняя сторона байка и т. п.).
Shader "MountainBike/Rider3D"
{
    Properties
    {
        [MainColor] _BaseColor ("Цвет", Color) = (1, 1, 1, 1)
        _Spec ("Блик", Range(0, 1)) = 0.05
        _Gloss ("Резкость блика", Range(1, 200)) = 12
        _Chrome ("Хром", Range(0, 1)) = 0
        _Rim ("Подсветка контура", Range(0, 1)) = 0.12
        _AO ("Запечённое затенение", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            ZWrite On
            ZTest LEqual
            // обе стороны: футболка и шорты — тонкая оболочка, открытая снизу (подол, штанины). Когда видна изнанка
            // (360 — спиной к нам, снизу через подол), задние грани не отбрасываются, а закрашиваются как ткань изнутри
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Spec, _Gloss, _Chrome, _Rim, _AO;
            CBUFFER_END

            // свет: сверху-спереди-слева (как на нарисованных спрайтах)
            static const float3 LightDir = float3(-0.45, 0.78, -0.43);
            static const half3 SkyTop = half3(0.95, 0.97, 1.0);
            static const half3 SkyLow = half3(0.18, 0.17, 0.16);

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; half ao : TEXCOORD2; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                o.positionWS = ws;
                half a = saturate(v.color.r);
                o.ao = max(0.3, a * a);                                                // цвет вершин в sRGB → примерно линейный
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionCS = TransformWorldToHClip(ws);
                // глубина: ±1 м вокруг центра модели → дальние 10% буфера (порядок внутри модели сохраняется)
                float vz = TransformWorldToView(ws).z;
                float cz = TransformWorldToView(float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23)).z;
                float t = saturate((cz - vz + 1.0) * 0.5);
                float z01 = 0.90 + 0.08 * t;
                #if UNITY_REVERSED_Z
                    o.positionCS.z = (1.0 - z01) * o.positionCS.w;
                #else
                    o.positionCS.z = (UNITY_NEAR_CLIP_VALUE < 0 ? z01 * 2.0 - 1.0 : z01) * o.positionCS.w;
                #endif
                return o;
            }

            half4 frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                bool front = IS_FRONT_VFACE(face, true, false);
                if (!front) n = -n;                                                   // изнанка: нормаль — внутрь оболочки
                float3 l = normalize(LightDir);
                float3 vdir = GetWorldSpaceNormalizeViewDir(i.positionWS);
                // как у нарисованных спрайтов: тёплый свет, холодная тень, отчётливый переход (складки читаются)
                half ndl = dot(n, l);
                half wrap = smoothstep(-0.25, 0.75, ndl);
                half3 base = _BaseColor.rgb;
                half3 col = base * lerp(half3(0.30, 0.32, 0.42), half3(1.05, 1.0, 0.92), wrap);
                col *= lerp(0.72, 1.0, saturate(n.y * 0.6 + 0.6));                 // низ (под подолом, под руками) темнее
                half sky = saturate(n.y * 0.5 + 0.5);
                col += base * 0.10 * lerp(SkyLow, SkyTop, sky);                     // небо сверху, земля снизу
                half ao = lerp(1.0, i.ao, _AO);
                col *= ao;                                                            // складки, швы, подмышки — темнее
                float3 r = reflect(-vdir, n);
                half3 env = lerp(SkyLow, SkyTop, smoothstep(-0.25, 0.45, r.y));
                col = lerp(col, env * base * 1.1, _Chrome);
                half spec = pow(saturate(dot(n, normalize(l + vdir))), _Gloss) * _Spec;
                half rim = pow(1.0 - saturate(dot(n, vdir)), 3.0) * _Rim * (0.4 + 0.6 * sky);
                // изнанка — тот же цвет ткани (белая футболка остаётся белой), чуть в тени, без блика и подсветки
                if (!front) return half4(lerp(base * 0.75, col, 0.5), 1.0);
                return half4(col + (spec + rim) * ao, 1.0);
            }
            ENDHLSL
        }
    }
}
