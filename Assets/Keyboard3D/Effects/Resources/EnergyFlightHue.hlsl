// Shared hue shift keeps the accepted gradients and white highlights intact.
float _HueShift;
float3 _BaseColor;
float3 ShiftFlightHue(float3 color)
{
 // Base color override: recolor with letter RGB × luminance structure (overexposed core tends toward white).
 // The palette is a blue-violet-teal multi-hue gradient; a single hue shift cannot map it to an arbitrary target color.
 if(_BaseColor.r+_BaseColor.g+_BaseColor.b>.001)
 {
  color=max(color,0);
  float v=max(color.r,max(color.g,color.b));
  return _BaseColor*v+saturate(v-1);
 }
 if(abs(_HueShift)<.00001)return color;
 color=max(color,0);
 float high=max(color.r,max(color.g,color.b)),low=min(color.r,min(color.g,color.b));
 float chroma=high-low;
 if(chroma<.00001)return color;
 float hue=high==color.r?(color.g-color.b)/chroma:high==color.g?2+(color.b-color.r)/chroma:4+(color.r-color.g)/chroma;
 hue=frac(hue/6+_HueShift);
 float3 ramp=saturate(abs(frac(hue+float3(0,2.0/3.0,1.0/3.0))*6-3)-1);
 return low+chroma*ramp;
}
