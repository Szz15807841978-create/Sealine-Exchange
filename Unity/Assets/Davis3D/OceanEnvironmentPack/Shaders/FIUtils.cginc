#ifndef FI_UTILS
#define FI_UTILS

float3 RotateAboutAxis(float4 NormalizedRotationAxisAndAngle, float3 PositionOnAxis, float3 Position)
{
    float3 ClosestPointOnAxis = PositionOnAxis + NormalizedRotationAxisAndAngle.xyz * dot(NormalizedRotationAxisAndAngle.xyz, Position - PositionOnAxis);
    float3 UAxis = Position - ClosestPointOnAxis;
    float3 VAxis = cross(NormalizedRotationAxisAndAngle.xyz, UAxis);
    float CosAngle;
    float SinAngle;
    sincos(NormalizedRotationAxisAndAngle.w, SinAngle, CosAngle);
    float3 R = UAxis * CosAngle + VAxis * SinAngle;
    float3 RotatedPosition = ClosestPointOnAxis + R;
    return RotatedPosition - Position;
}

float3 GrassWindMovement(float3 WorldPosition, float3 AdditionalWPO, float WindIntensity, float WindWeight, float WindSpeed, float WindScale, float WindMaskInvert)
{
    float3 NormalizedRotationAxis = cross(normalize(float3(0, 1, 0)), float3(0, 0, 1));
    
    float3 Section01 = WorldPosition / WindScale;
    float Section00 = _Time.y * WindSpeed * -0.5;
    float3 Section02 = normalize(float3(0, 1, 0)) * Section00;
    Section02 += 2 * WorldPosition;
    Section02 = frac(Section02 + 0.5) * 2.0 - 1.0;
    Section02 = abs(Section02);
    float3 Section04 = Section02 * Section02 * (3 - (Section02 * 2));
    Section01 = frac(Section01 + 0.5) * 2.0 - 1.0;
    float3 Section10 = abs(Section01);
    float3 Section11 = (3 - (Section10 * 2)) * Section10 * Section10;
    float RotationAngle = dot(Section04, normalize(float3(0, 1, 0))) + distance(Section11, 0);
    
    float3 Position = AdditionalWPO;
    float3 PivotPoint = AdditionalWPO + float3(0, 0, -10) * 0.02;
    
    float3 Result = RotateAboutAxis(float4(NormalizedRotationAxis, RotationAngle), Position, PivotPoint);
    Result = (Result * WindWeight * WindIntensity) + AdditionalWPO;

    return Result;
}

float4 GradientMap_Multi(float GreyscaleToGradient, float Index, sampler2D Gradient, float NumberOfGradients)
{
    NumberOfGradients = 1.0 / ceil(NumberOfGradients);
    Index = ceil(Index);
    float Section01 = NumberOfGradients * Index;
    Section01 += 0.5 * NumberOfGradients;
    float2 uv = float2(GreyscaleToGradient, Section01);

    return tex2Dlod(Gradient, float4(uv, 0, 0));
}

float3 MS_MorphTargets(float2 uv, float MorphAnimation, sampler2D MorphNormal, sampler2D MorphTexture, float NumberOfMorphTargets)
{
    float s = MorphAnimation * (NumberOfMorphTargets - 1.0);
    float s00 = floor(s);
    float s01 = s00 + 1.0;
    float s02 = frac(s);

    float4 Gradient01 = GradientMap_Multi(uv.r, s01, MorphTexture, NumberOfMorphTargets);
    float4 Gradient02 = GradientMap_Multi(uv.r, s00, MorphTexture, NumberOfMorphTargets);

    float3 vertexOffset = lerp(Gradient02, Gradient01, s02);

    return vertexOffset;
}


#endif