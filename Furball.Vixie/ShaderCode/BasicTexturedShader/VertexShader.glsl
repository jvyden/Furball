#version 300 es

layout(location = 0) in vec4 position;
layout(location = 1) in vec2 texCoord;

out vec2 v_TexCoord;

uniform mat4 vx_WindowProjectionMatrix;
uniform mat4 u_Translation;
uniform float vx_ModifierX;
uniform float vx_ModifierY;

void main() {
    gl_Position = vx_WindowProjectionMatrix * u_Translation * position;

    v_TexCoord = texCoord;

    // This flips the position into the coordinate space we want
    gl_Position.x *= vx_ModifierX;
    gl_Position.y *= vx_ModifierY;
}