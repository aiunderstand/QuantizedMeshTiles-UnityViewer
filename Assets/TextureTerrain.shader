Shader "Custom/TextureTerrain" {
	Properties {
		_MainTex ("Base (RGB) Alpha (A)", 2D) = "white" {}
		_Color ("Main Color", Color) = (1,1,1,1)
		_SplatControl ("Control", Color) = (0, 0, 0, 0)
		_Splat1 ("Splat1", 2D) = "white" {}
		_Splat2 ("Splat2", 2D) = "white" {}
		_Splat3 ("Splat3", 2D) = "white" {}
		_Splat4 ("Splat4", 2D) = "white" {}
		_Mask ("Mask", 2D) = "white" {}
		[HideInInspector] _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
	}
	
	SubShader {
		//Tags { "Queue"="AlphaTest" "RenderType"="Transparent" }
		Tags { "Queue"="Transparent" "RenderType"="Transparent" }
		Blend SrcAlpha OneMinusSrcAlpha
        LOD 200
		
		Cull Front
        CGPROGRAM
        #pragma surface surf Lambert vertex:vert alpha
 
        sampler2D _MainTex;
 
        struct Input {
            float2 uv_MainTex;
        };
 
        // Flip normal for back faces
        void vert (inout appdata_full v) {
            v.normal *= -1;
         }
		 
		   // Red back faces
         void surf (Input IN, inout SurfaceOutput o) {
             half4 c = tex2D (_MainTex, IN.uv_MainTex);
             o.Albedo = fixed3(1,0,0);
             //o.Alpha = c.a;
			 o.Alpha = 0.3;
         }
         ENDCG

		 Cull Back
         CGPROGRAM

		#pragma surface surf Lambert alphatest:_Cutoff

		sampler2D _Splat1;
		sampler2D _Splat2;
		sampler2D _Splat3;
		sampler2D _Splat4;
		sampler2D _Mask;
		fixed4 _Color;
		fixed4 _SplatControl;
			 
		struct Input {
			float2 uv_Splat1;
		};
		
		void surf (Input IN, inout SurfaceOutput o) {
			fixed3 c = _Color.rgb;
			if (_SplatControl.r != 0)
			{
				fixed4 tmp = tex2D(_Splat1, IN.uv_Splat1);
				c = lerp(c, tmp.rgb, _SplatControl.r * tmp.a);
			}
			if (_SplatControl.g != 0)
			{
				fixed4 tmp = tex2D(_Splat2, IN.uv_Splat1);
				c = lerp(c, tmp.rgb, _SplatControl.g * tmp.a);
			}
			if (_SplatControl.b != 0)
			{
				fixed4 tmp = tex2D(_Splat3, IN.uv_Splat1);
				c = lerp(c, tmp.rgb, _SplatControl.b * tmp.a);
			}
			if (_SplatControl.a != 0)
			{
				fixed4 tmp = tex2D(_Splat4, IN.uv_Splat1);
				c = lerp(c, tmp.rgb, _SplatControl.a * tmp.a);
			}
			o.Albedo =  c;
			fixed4 b = tex2D(_Mask, IN.uv_Splat1);
			o.Alpha = b.a;
		}
		ENDCG
	}

	Fallback "VertexLit"
}
