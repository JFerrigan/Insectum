Shader "Insectum/NightVision"
{
    Properties
    {
        _MainTex ("Native scene", 2D) = "white" {}
        _Light ("Light center and radius", Vector) = (240, 135, 43.2, 0)
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _Light;

            fixed4 frag(v2f_img input) : SV_Target
            {
                fixed4 scene = tex2D(_MainTex, input.uv);
                float2 pixel = floor(input.uv * _MainTex_TexelSize.zw) + 0.5;
                float distanceToPlayer = distance(pixel, _Light.xy);
                float visibility = 1.0 - smoothstep(_Light.z * 0.67, _Light.z, distanceToPlayer);
                scene.rgb *= visibility;
                return scene;
            }
            ENDCG
        }
    }
}
