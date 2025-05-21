// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Unlit/UI_BGBlur"
{
	Properties
	{
		_Size("Size Level 1", float) = 1.0     // 第一级模糊程度
		_Size2("Size Level 2", float) = 0.5   // 第二级模糊程度 (新增)
		_Color("Color", Color) = (1,1,1,1)
	}
	SubShader
	{
		// 为UI和GrabPass使用更合适的Tags
		Tags { "Queue"="Transparent" "RenderType"="Opaque" "IgnoreProjector"="True" }
		LOD 100

		// ------------- 第一级模糊 -------------

		// Pass 1: 水平模糊 (第一级)
		GrabPass { Tags{"LightMode" = "Always"} }

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag_h1 // 第一级水平模糊片元着色器
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float4 uvgrab : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			sampler2D _GrabTexture;
			float4 _GrabTexture_TexelSize;
			float _Size; // 使用第一级模糊尺寸

			v2f vert(appdata_base v) {
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				#if UNITY_UV_STARTS_AT_TOP
				float scale = -1.0;
				#else
				float scale = 1.0;
				#endif
				o.uvgrab.xy = (float2(o.vertex.x, o.vertex.y*scale) + o.vertex.w) * 0.5;
				o.uvgrab.zw = o.vertex.zw;
				return o;
			}

			fixed4 frag_h1(v2f i) : SV_Target
			{
				half4 sum = half4(0,0,0,0);
				#define GRABPIXEL_H1(weight,kernelx) tex2Dproj( _GrabTexture, UNITY_PROJ_COORD(float4(i.uvgrab.x + _GrabTexture_TexelSize.x * kernelx * _Size, i.uvgrab.y, i.uvgrab.z, i.uvgrab.w))) * weight
				sum += GRABPIXEL_H1(0.05, -4.0);
				sum += GRABPIXEL_H1(0.09, -3.0);
				sum += GRABPIXEL_H1(0.12, -2.0);
				sum += GRABPIXEL_H1(0.15, -1.0);
				sum += GRABPIXEL_H1(0.18, 0.0);
				sum += GRABPIXEL_H1(0.15, +1.0);
				sum += GRABPIXEL_H1(0.12, +2.0);
				sum += GRABPIXEL_H1(0.09, +3.0);
				sum += GRABPIXEL_H1(0.05, +4.0);
				// _Color 不在此处应用
				return sum;
			}
			ENDCG
		}

		// Pass 2: 垂直模糊 (第一级)
		GrabPass { Tags{"LightMode" = "Always"} }

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag_v1 // 第一级垂直模糊片元着色器
			#include "UnityCG.cginc"

			struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
			struct v2f { float4 uvgrab : TEXCOORD0; float4 vertex : SV_POSITION; };

			sampler2D _GrabTexture;
			float4 _GrabTexture_TexelSize;
			float _Size; // 使用第一级模糊尺寸

			v2f vert(appdata_base v) { /* 与上方vert相同 */
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				#if UNITY_UV_STARTS_AT_TOP
				float scale = -1.0;
				#else
				float scale = 1.0;
				#endif
				o.uvgrab.xy = (float2(o.vertex.x, o.vertex.y*scale) + o.vertex.w) * 0.5;
				o.uvgrab.zw = o.vertex.zw;
				return o;
			}

			fixed4 frag_v1(v2f i) : SV_Target
			{
				half4 sum = half4(0,0,0,0);
				#define GRABPIXEL_V1(weight,kernely) tex2Dproj( _GrabTexture, UNITY_PROJ_COORD(float4(i.uvgrab.x, i.uvgrab.y + _GrabTexture_TexelSize.y * kernely * _Size, i.uvgrab.z, i.uvgrab.w))) * weight
				sum += GRABPIXEL_V1(0.05, -4.0);
				sum += GRABPIXEL_V1(0.09, -3.0);
				sum += GRABPIXEL_V1(0.12, -2.0);
				sum += GRABPIXEL_V1(0.15, -1.0);
				sum += GRABPIXEL_V1(0.18, 0.0);
				sum += GRABPIXEL_V1(0.15, +1.0);
				sum += GRABPIXEL_V1(0.12, +2.0);
				sum += GRABPIXEL_V1(0.09, +3.0);
				sum += GRABPIXEL_V1(0.05, +4.0);
				// _Color 不在此处应用
				return sum;
			}
			ENDCG
		}

		// ------------- 第二级模糊 (更精细的模糊) -------------

		// Pass 3: 水平模糊 (第二级)
		GrabPass { Tags{"LightMode" = "Always"} }

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag_h2 // 第二级水平模糊片元着色器
			#include "UnityCG.cginc"

			struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
			struct v2f { float4 uvgrab : TEXCOORD0; float4 vertex : SV_POSITION; };

			sampler2D _GrabTexture;
			float4 _GrabTexture_TexelSize;
			float _Size2; // 使用第二级模糊尺寸

			v2f vert(appdata_base v) { /* 与上方vert相同 */
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				#if UNITY_UV_STARTS_AT_TOP
				float scale = -1.0;
				#else
				float scale = 1.0;
				#endif
				o.uvgrab.xy = (float2(o.vertex.x, o.vertex.y*scale) + o.vertex.w) * 0.5;
				o.uvgrab.zw = o.vertex.zw;
				return o;
			}

			fixed4 frag_h2(v2f i) : SV_Target
			{
				half4 sum = half4(0,0,0,0);
				// 注意: 此处宏定义中的kernelx*_Size需要改为kernelx*_Size2
				#define GRABPIXEL_H2(weight,kernelx) tex2Dproj( _GrabTexture, UNITY_PROJ_COORD(float4(i.uvgrab.x + _GrabTexture_TexelSize.x * kernelx * _Size2, i.uvgrab.y, i.uvgrab.z, i.uvgrab.w))) * weight
				sum += GRABPIXEL_H2(0.05, -4.0);
				sum += GRABPIXEL_H2(0.09, -3.0);
				sum += GRABPIXEL_H2(0.12, -2.0);
				sum += GRABPIXEL_H2(0.15, -1.0);
				sum += GRABPIXEL_H2(0.18, 0.0);
				sum += GRABPIXEL_H2(0.15, +1.0);
				sum += GRABPIXEL_H2(0.12, +2.0);
				sum += GRABPIXEL_H2(0.09, +3.0);
				sum += GRABPIXEL_H2(0.05, +4.0);
				// _Color 不在此处应用
				return sum;
			}
			ENDCG
		}

		// Pass 4: 垂直模糊 (第二级) - 在此应用最终颜色
		GrabPass { Tags{"LightMode" = "Always"} }

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag_v2 // 第二级垂直模糊片元着色器
			#include "UnityCG.cginc"

			struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
			struct v2f { float4 uvgrab : TEXCOORD0; float4 vertex : SV_POSITION; };

			sampler2D _GrabTexture;
			float4 _GrabTexture_TexelSize;
			float _Size2; // 使用第二级模糊尺寸
			half4 _Color; // _Color 在此Pass的变量中声明，并在末尾应用

			v2f vert(appdata_base v) { /* 与上方vert相同 */
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				#if UNITY_UV_STARTS_AT_TOP
				float scale = -1.0;
				#else
				float scale = 1.0;
				#endif
				o.uvgrab.xy = (float2(o.vertex.x, o.vertex.y*scale) + o.vertex.w) * 0.5;
				o.uvgrab.zw = o.vertex.zw;
				return o;
			}

			fixed4 frag_v2(v2f i) : SV_Target
			{
				half4 sum = half4(0,0,0,0);
				// 注意: 此处宏定义中的kernely*_Size需要改为kernely*_Size2
				#define GRABPIXEL_V2(weight,kernely) tex2Dproj( _GrabTexture, UNITY_PROJ_COORD(float4(i.uvgrab.x, i.uvgrab.y + _GrabTexture_TexelSize.y * kernely * _Size2, i.uvgrab.z, i.uvgrab.w))) * weight
				sum += GRABPIXEL_V2(0.05, -4.0);
				sum += GRABPIXEL_V2(0.09, -3.0);
				sum += GRABPIXEL_V2(0.12, -2.0);
				sum += GRABPIXEL_V2(0.15, -1.0);
				sum += GRABPIXEL_V2(0.18, 0.0);
				sum += GRABPIXEL_V2(0.15, +1.0);
				sum += GRABPIXEL_V2(0.12, +2.0);
				sum += GRABPIXEL_V2(0.09, +3.0);
				sum += GRABPIXEL_V2(0.05, +4.0);
				sum *= _Color; // 在最终Pass应用颜色
				return sum;
			}
			ENDCG
		}
	}
	Fallback "UI/Unlit/Transparent"
}
