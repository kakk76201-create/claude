// Ластик-тональник: рисует кусочек чистого лица (базовой текстуры) мягким кругом поверх краски.
// Используется только для рисования в RenderTexture лица (Graphics.DrawTexture), не в сцене.
Shader "Hidden/MakeupSniper/EraserBlit"
{
    Properties
    {
        _MainTex ("Base face", 2D) = "white" {}
        _Center ("Center (uv)", Vector) = (0.5, 0.5, 0, 0)
        _Radius ("Radius (uv)", Float) = 0.1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB // прозрачность холста не трогаем, иначе на полароиде просвечивает фон

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _Center;
            float _Radius;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                float d = distance(i.uv, _Center.xy) / max(_Radius, 1e-5);
                float a = 1.0 - smoothstep(0.7, 1.0, d);
                return fixed4(c.rgb, a);
            }
            ENDCG
        }
    }
}
