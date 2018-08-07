﻿#ifndef ANIMATION_INSTANCING_BASE
#define ANIMATION_INSTANCING_BASE

//#pragma target 3.0

sampler2D _boneTexture;	//在mat里面读取出来
int _boneTextureBlockWidth;
int _boneTextureBlockHeight;
int _boneTextureWidth;
int _boneTextureHeight;

#if (SHADER_TARGET < 30 || SHADER_API_GLES)
uniform float frameIndex;
uniform float preFrameIndex;
uniform float transitionProgress;
#else
UNITY_INSTANCING_CBUFFER_START(Props)		//作为属性变量可以对每个像素不同，有各自的算式。
	UNITY_DEFINE_INSTANCED_PROP(float, preFrameIndex) 
	UNITY_DEFINE_INSTANCED_PROP(float, frameIndex)
	UNITY_DEFINE_INSTANCED_PROP(float, transitionProgress)
UNITY_INSTANCING_CBUFFER_END
#endif

half4x4 loadMatFromTexture(uint frameIndex, uint boneIndex)
{
	uint blockCount = _boneTextureWidth / _boneTextureBlockWidth;   //每行的block数
	int2 uv;
	//找到Index对应的x,y
	uv.y = frameIndex / blockCount * _boneTextureBlockHeight;
	uv.x = _boneTextureBlockWidth * (frameIndex - blockCount * uv.y);

    //这段说明在组织block时，可以组织一行放matCount个骨骼矩阵，一个block放boneCount/matCount行。
	int matCount = _boneTextureBlockWidth / 4;  //一个矩阵4个像素（4*4）
	uv.x = uv.x + (boneIndex % matCount) * 4;
	uv.y = uv.y + boneIndex / matCount;

    //这里的uvFrame是将Width和Height归一化为1时的uvfloat值。
	float2 uvFrame;
	uvFrame.x = uv.x / (float)_boneTextureWidth;
	uvFrame.y = uv.y / (float)_boneTextureHeight;
	half4 uvf = half4(uvFrame, 0, 0);

	float offset = 1.0f / (float)_boneTextureWidth; //对应的归一化偏移。
	half4 c1 = tex2Dlod(_boneTexture, uvf);
	uvf.x = uvf.x + offset;
	half4 c2 = tex2Dlod(_boneTexture, uvf);
	uvf.x = uvf.x + offset;
	half4 c3 = tex2Dlod(_boneTexture, uvf);
	uvf.x = uvf.x + offset;
	//half4 c4 = tex2Dlod(_boneTexture, uvf);
	half4 c4 = half4(0, 0, 0, 1);
	//float4x4 m = float4x4(c1, c2, c3, c4);
	half4x4 m;
	m._11_21_31_41 = c1;
	m._12_22_32_42 = c2;
	m._13_23_33_43 = c3;
	m._14_24_34_44 = c4;
	return m;
}

half4 skinning(inout appdata_full v)
{
	fixed4 w = v.color;
	half4 bone = half4(v.texcoord2.x, v.texcoord2.y, v.texcoord2.z, v.texcoord2.w);
#if (SHADER_TARGET < 30 || SHADER_API_GLES)
	float curFrame = frameIndex;
	float preAniFrame = preFrameIndex;
	float progress = transitionProgress;
#else
	float curFrame = UNITY_ACCESS_INSTANCED_PROP(frameIndex);
	float preAniFrame = UNITY_ACCESS_INSTANCED_PROP(preFrameIndex);
	float progress = UNITY_ACCESS_INSTANCED_PROP(transitionProgress);
#endif

	//float curFrame = UNITY_ACCESS_INSTANCED_PROP(frameIndex);
	int preFrame = curFrame;
	int nextFrame = curFrame + 1.0f;
	half4x4 localToWorldMatrixPre = loadMatFromTexture(preFrame, bone.x) * w.x;
	if (w.y > 0.0f)
		localToWorldMatrixPre = localToWorldMatrixPre + loadMatFromTexture(preFrame, bone.y) * w.y;
	if (w.z > 0.0f)
		localToWorldMatrixPre = localToWorldMatrixPre + loadMatFromTexture(preFrame, bone.z) * w.z;
	if (w.w > 0.0f)
		localToWorldMatrixPre = localToWorldMatrixPre + loadMatFromTexture(preFrame, bone.w) * w.w;

	half4x4 localToWorldMatrixNext = loadMatFromTexture(nextFrame, bone.x) * w.x;
	if (w.y > 0.0f)
		localToWorldMatrixNext = localToWorldMatrixNext + loadMatFromTexture(nextFrame, bone.y) * w.y;
	if (w.z > 0.0f)
		localToWorldMatrixNext = localToWorldMatrixNext + loadMatFromTexture(nextFrame, bone.z) * w.z;
	if (w.w > 0.0f)
		localToWorldMatrixNext = localToWorldMatrixNext + loadMatFromTexture(nextFrame, bone.w) * w.w;

	half4 localPosPre = mul(v.vertex, localToWorldMatrixPre);
	half4 localPosNext = mul(v.vertex, localToWorldMatrixNext);
	half4 localPos = lerp(localPosPre, localPosNext, curFrame - preFrame);

	if (preAniFrame >= 0.0f)
	{
		half4x4 localToWorldMatrixPreAni = loadMatFromTexture(preAniFrame, bone.x);
		half4 localPosPreAni = mul(v.vertex, localToWorldMatrixPreAni);
		localPos = lerp(localPosPreAni, localPos, progress);
	}
	return localPos;
}

half4 skinningShadow(inout appdata_full v)
{
	half4 bone = half4(v.texcoord2.x, v.texcoord2.y, v.texcoord2.z, v.texcoord2.w);
#if (SHADER_TARGET < 30 || SHADER_API_GLES)
	float curFrame = frameIndex;
	float preAniFrame = preFrameIndex;
	float progress = transitionProgress;
#else
	float curFrame = UNITY_ACCESS_INSTANCED_PROP(frameIndex);
	float preAniFrame = UNITY_ACCESS_INSTANCED_PROP(preFrameIndex);
	float progress = UNITY_ACCESS_INSTANCED_PROP(transitionProgress);
#endif
	int preFrame = curFrame;
	int nextFrame = curFrame + 1.0f;
	half4x4 localToWorldMatrixPre = loadMatFromTexture(preFrame, bone.x);
	half4x4 localToWorldMatrixNext = loadMatFromTexture(nextFrame, bone.x);
	half4 localPosPre = mul(v.vertex, localToWorldMatrixPre);
	half4 localPosNext = mul(v.vertex, localToWorldMatrixNext);
	half4 localPos = lerp(localPosPre, localPosNext, curFrame - preFrame);
	if (preAniFrame >= 0.0f)
	{
		half4x4 localToWorldMatrixPreAni = loadMatFromTexture(preAniFrame, bone.x);
		half4 localPosPreAni = mul(v.vertex, localToWorldMatrixPreAni);
		localPos = lerp(localPosPreAni, localPos, progress);
	}
	//half4 localPos = v.vertex;
	return localPos;
}

void vert(inout appdata_full v)
{
#ifdef UNITY_PASS_SHADOWCASTER
	v.vertex = skinningShadow(v);
#else
	v.vertex = skinning(v);
#endif
}

//#define DECLARE_VERTEX_SKINNING \
//	#pragma vertex vert 

#endif