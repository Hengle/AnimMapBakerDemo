﻿/*
 
 * 
 * 用来烘焙动作贴图。烘焙对象使用animation组件，并且在导入时设置Rig为Legacy
 */
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.IO;

/// <summary>
/// 保存需要烘焙的动画的相关数据
/// </summary>
public struct AnimData
{
    #region 字段

    public int vertexCount;//顶点
    public int mapWidth;//贴图宽度
    public List<AnimationState> animClips;//动画属性
    public string name;//动画名称

    private Animation animation;
    private SkinnedMeshRenderer skin;

    #endregion

    public AnimData(Animation anim, SkinnedMeshRenderer smr, string goName)
    {
        vertexCount = smr.sharedMesh.vertexCount;
        mapWidth = Mathf.NextPowerOfTwo(vertexCount);
        animClips = new List<AnimationState>(anim.Cast<AnimationState>());//类型转换   把animation转换为animationState
        animation = anim;
        skin = smr;
        name = goName;
    }

    #region 方法

    public void AnimationPlay(string animName)//播放动画
    {
        this.animation.Play(animName);
    }

    public void SampleAnimAndBakeMesh(ref Mesh m)
    {
        this.SampleAnim();
        this.BakeMesh(ref m);
    }

    private void SampleAnim()//采样动画
    {
        if (this.animation == null)
        {
            Debug.LogError("animation is null!!");
            return;
        }

        this.animation.Sample();//在当前状态下采样动画
    }

    private void BakeMesh(ref Mesh m)//创建SkinnedMeshRenderer的快照并将其存储在网格中
    {
        if (this.skin == null)
        {
            Debug.LogError("skin is null!!");
            return;
        }

        this.skin.BakeMesh(m);
    }


    #endregion

}

/// <summary>
/// 烘焙后的数据
/// </summary>
public struct BakedData
{
    #region 字段

    public string name;//动画名
    public float animLen;//动画长度
    public byte[] rawAnimMap;
    public int animMapWidth;//贴图宽度
    public int animMapHeight;//贴图高度

    #endregion

    public BakedData(string name, float animLen, Texture2D animMap)//快照数据
    {
        this.name = name;
        this.animLen = animLen;
        this.animMapHeight = animMap.height;
        this.animMapWidth = animMap.width;
        this.rawAnimMap = animMap.GetRawTextureData();
    }
}

/// <summary>
/// 烘焙器
/// </summary>
public class AnimMapBaker
{

    #region 字段

    private AnimData? animData = null;//定义一个值类型(当前是结构体)的可空引用
    //private List<Vector3> vertices = new List<Vector3>();
    private Mesh bakedMesh;

    private List<BakedData> bakedDataList = new List<BakedData>();

    #endregion

    #region 方法

    public void SetAnimData(GameObject go)
    {
        if (go == null)
        {
            Debug.LogError("go is null!!");
            return;
        }

        Animation anim = go.GetComponent<Animation>();
        SkinnedMeshRenderer smr = go.GetComponentInChildren<SkinnedMeshRenderer>();

        if (anim == null || smr == null)
        {
            Debug.LogError("anim or smr is null!!");
            return;
        }
        this.bakedMesh = new Mesh();
        this.animData = new AnimData(anim, smr, go.name);
    }

    public List<BakedData> Bake()
    {
        if (this.animData == null)
        {
            Debug.LogError("bake data is null!!");
            return this.bakedDataList;
        }

        //每一个动作都生成一个动作图
        for (int i = 0; i < this.animData.Value.animClips.Count; i++)
        {
            if (!this.animData.Value.animClips[i].clip.legacy)
            {
                Debug.LogError(string.Format("{0} is not legacy!!", this.animData.Value.animClips[i].clip.name));
                continue;
            }

            BakePerAnimClip(this.animData.Value.animClips[i]);
        }

        return this.bakedDataList;
    }

    private void BakePerAnimClip(AnimationState curAnim)//制作动画片段并保存到动画纹理中
    {
        int curClipFrame = 0;
        float sampleTime = 0;
        float perFrameTime = 0;
        //curAnim.clip.frameRate 对关键帧进行采样的帧速率。（只读）
        curClipFrame = Mathf.ClosestPowerOfTwo((int)(curAnim.clip.frameRate * curAnim.length));
        perFrameTime = curAnim.length / curClipFrame;

        Texture2D animMap = new Texture2D(this.animData.Value.mapWidth, curClipFrame, TextureFormat.RGBAHalf, false);
        animMap.name = string.Format("{0}_{1}.animMap", this.animData.Value.name, curAnim.name);
        this.animData.Value.AnimationPlay(curAnim.name);

        for (int i = 0; i < curClipFrame; i++)
        {
            curAnim.time = sampleTime;

            this.animData.Value.SampleAnimAndBakeMesh(ref this.bakedMesh);

            for (int j = 0; j < this.bakedMesh.vertexCount; j++)
            {
                Vector3 vertex = this.bakedMesh.vertices[j];
                animMap.SetPixel(j, i, new Color(vertex.x, vertex.y, vertex.z)); //在坐标（x，y）处设置像素颜色。
            }

            sampleTime += perFrameTime;
        }
        animMap.Apply();

        this.bakedDataList.Add(new BakedData(animMap.name, curAnim.clip.length, animMap));
    }

    #endregion


    #region 属性


    #endregion

}
