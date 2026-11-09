#ifndef KEYBOARD_LETTER_SURFACE_INCLUDED
#define KEYBOARD_LETTER_SURFACE_INCLUDED

float3 LetterBevelColor(float3 mainColor, float3 independentColor, float followMain, float brightness)
{
    return lerp(independentColor,mainColor*max(brightness,0),saturate(followMain));
}

// Body is ordinary bounded surface color. HDR amplification is reserved for bevels.
float3 LetterBodyOutput(float3 color)
{
    return saturate(color);
}

// Front and side/back use existing submesh regions; no UV or texture sampling.
float3 LetterGradient(float y,float baseline,float topY,float3 mainColor,
    float topBrightness,float bottomBrightness,float sideBrightness,float side)
{
    float t=saturate((y-baseline)/max(topY-baseline,0.0001));
    float3 gradient=mainColor*max(topBrightness,0)*lerp(saturate(bottomBrightness),1,t);
    return gradient*lerp(1,saturate(sideBrightness),saturate(side));
}
#endif
