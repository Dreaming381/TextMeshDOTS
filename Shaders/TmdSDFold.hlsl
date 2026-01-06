//#ifndef TMDSDF
//#define TMDSDF

//#include "TmdGlobalsApi.hlsl"

////adjust face weight
//void GetFontWeight_float(float dilationIN, float scale, float weightNormal, float weightBold, out float dilationOUT)
//{
//	float bold = step(scale, 0); //float bold = scale < 0.0;
//	float weight = lerp(weightNormal, weightBold, bold) / 4.0;
//	dilationOUT = (weight + dilationIN) * 0.5;
//}
////adjust face weight
//void GetFontWeight2_float(float2 dilationIN, float scale, float weightNormal, float weightBold, out float2 dilationOUT)
//{
//	float bold = step(scale, 0); //float bold = scale < 0.0;
//	float weight = lerp(weightNormal, weightBold, bold) / 4.0;
//	dilationOUT = (weight + dilationIN) * 0.5;
//}
////adjust face weight
//void GetFontWeight4_float(float4 dilationIN, float scale, float weightNormal, float weightBold, out float4 dilationOUT)
//{
//	float bold = step(scale, 0); //float bold = scale < 0.0;
//	float weight = lerp(weightNormal, weightBold, bold) / 4.0;
//	dilationOUT = (weight + dilationIN) * 0.5;
//}
//// UV			: Texture coordinate of the source distance field texture
//// texelSize	: texelSize of the source distance field texture
//void ScreenSpaceRatio(float2 uvA, float2 texelSize, out float SSR)
//{
//	SSR = rsqrt(abs(ddx(uvA.x) * ddy(uvA.y) - ddy(uvA.x) * ddx(uvA.y))) * texelSize.x;
//}

//// SSR : Screen Space Ratio
//// SD  : Signed Distance (encoded : Distance / SDR + .5)
//// SDR : Signed Distance Ratio
////
//// IsoPerimeter : Dilate / Contract the shape
//void ComputeSDF_float(float SSR, float SDR, float SD, float dilation, float softness, out float outAlpha)
//{
//	softness *= SSR * SDR;
//	float d = (SD - 0.5) * SDR; // Signed distance to edge, in Texture space
//	outAlpha = saturate((d * 2.0 * SSR + 0.5 + dilation * SDR * SSR + softness * 0.5) / (1.0 + softness)); // Screen pixel coverage (alpha)
//}
//void ComputeSDF2_float(float SSR, float SDR, float2 SD, float2 dilation, float2 softness, out float2 outAlpha)
//{
//	softness *= SSR * SDR;
//	float2 d = (SD - 0.5f) * SDR;
//	outAlpha = saturate((d * 2.0f * SSR + 0.5f + dilation * SDR * SSR + softness * 0.5) / (1.0 + softness));
//}
//void ComputeSDF4_float(float SSR, float SDR, float4 SD, float4 dilation, float4 softness, out float4 outAlpha)
//{
//	softness *= SSR * SDR;
//	float4 d = (SD - 0.5f) * SDR;
//	outAlpha = saturate((d * 2.0f * SSR + 0.5f + dilation * SDR * SSR + softness * 0.5) / (1.0 + softness));
//}
//// Face only
//void Layer1_float(float alpha, float4 color0, out float4 outColor)
//{
//	color0.a *= alpha;
//	outColor = color0;
//}
//// Face + 1 Outline
//void Layer2_float(float2 alpha, float4 color0, float4 color1, out float4 outColor)
//{
//	color1.a *= alpha.y;
//	color0.rgb *= color0.a;
//	color1.rgb *= color1.a;
//	outColor = lerp(color1, color0, alpha.x);
//	outColor.rgb /= outColor.a;
//}
//// Face + 3 Outline
//void Layer4_float(float4 alpha, float4 color0, float4 color1, float4 color2, float4 color3, out float4 outColor)
//{
//	color3.a *= alpha.w;
//	color0.rgb *= color0.a;
//	color1.rgb *= color1.a;
//	color2.rgb *= color2.a;
//	color3.rgb *= color3.a;
//	outColor = lerp(lerp(lerp(color3, color2, alpha.z), color1, alpha.y), color0, alpha.x);
//	outColor.rgb /= outColor.a;
//}
//void Blend_float(float4 overlying, float4 underlying, out float4 colorOUT)
//{
//	overlying.rgb *= overlying.a;
//	underlying.rgb *= underlying.a;
//	float3 blended = overlying.rgb + ((1 - overlying.a) * underlying.rgb);
//	float alpha = underlying.a + (1 - underlying.a) * overlying.a;
//	colorOUT = float4(blended / alpha, alpha);
//}
//void ApplyVertexAlpha_float(
//    float4 vertexColor,
//    float4 colorIN,
//    out float4 colorOUT)
//{
//	colorOUT = colorIN * vertexColor.w;
//}

//float3 GetSpecular(float3 normal, float3 light, float4 lightColor, float reflectivityPower, float specularPower)
//{
//	float spec = pow(max(0.0, dot(normal, light)), reflectivityPower);
//	return lightColor.rgb * spec * specularPower;
//}
//void EvaluateLight_float(
//    float3 normal,
//    float4 faceColor,
//    float4 lightColor,
//    float lightAngle,
//    float specularPower,
//    float reflectivityPower,
//    float diffuseShadow,
//    float ambientShadow,
//    out float4 color)
//{
//	normal.z = abs(normal.z);
//	float sinAngle;
//	float cosAngle;
//	sincos(lightAngle, sinAngle, cosAngle);
//	float3 light = normalize(float3(sinAngle, cosAngle, 1.0));

//	float3 col = max(faceColor.rgb, 0) + GetSpecular(normal, light, lightColor, reflectivityPower, specularPower) * faceColor.a;

//	col *= 1 - (dot(normal, light) * diffuseShadow);
//	col *= lerp(ambientShadow, 1, normal.z * normal.z);
    
//	color = float4(col, faceColor.a);
//}
//#endif