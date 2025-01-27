#version 460

layout (location = 1) in vec4 vBLENDINDICES;
layout (location = 2) in vec4 vBLENDWEIGHT;

uniform bool bAnimated;
uniform sampler2D animationTexture;

mat4 getMatrix(float id) {
    int boneIndex = int(id);
    /*if (boneIndex >= textureSize(animationTexture, 0).y) {
        return mat4(1.0);
    }*/

    return mat4(
        texelFetch(animationTexture, ivec2(0, boneIndex), 0),
        texelFetch(animationTexture, ivec2(1, boneIndex), 0),
        texelFetch(animationTexture, ivec2(2, boneIndex), 0),
        texelFetch(animationTexture, ivec2(3, boneIndex), 0)
    );
}

mat4 getSkinMatrix(){
    //[branch]
    if (bAnimated)
    {
        mat4 skinMatrix = mat4(0.0);
        skinMatrix += vBLENDWEIGHT.x * getMatrix(vBLENDINDICES.x);
        skinMatrix += vBLENDWEIGHT.y * getMatrix(vBLENDINDICES.y);
        skinMatrix += vBLENDWEIGHT.z * getMatrix(vBLENDINDICES.z);
        skinMatrix += vBLENDWEIGHT.w * getMatrix(vBLENDINDICES.w);
        return skinMatrix;
    }

    return mat4(1.0);
}
