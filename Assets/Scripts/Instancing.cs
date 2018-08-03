using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;
using System.Runtime.CompilerServices;
using UnityEditor;

public class Instancing : MonoBehaviour
{
    public Animator animator = null;
    public Transform Pose;
    public GameObject prototype;

    private Transform[] allTransforms;

    public int bonePerVertex = 4;
    /*
    public class InstanceAnimationInfo 
    {
          public List<AnimationInfo> listAniInfo;
          public ExtraBoneInfo extraBoneInfo;
    }
    public class AnimationInfo
    {
        public string animationName;
        public int animationNameHash;
        public int totalFrame;
        public int fps;
        public int animationIndex;
        public int textureIndex;
        public bool rootMotion;
        public WrapMode wrapMode;
        public Vector3[] velocity;
        public Vector3[] angularVelocity;
        public List<AnimationEvent> eventList; 
    }

    public class ExtraBoneInfo
    {
        public string[] extraBone;
        public Matrix4x4[] extraBindPose;
    }
    */
    
    /*
     * Start Animation, In Instancing Mode or Defalt Mode
     * 
     */
    public void InitializeAnimation()
    {
        if (prototype == null)
        {
            Debug.LogError("The prototype is NULL. Please select the prototype first.");
        }
        Debug.Assert(prototype != null);
        //防止误改？
        GameObject thisPrefab = prototype;
        List<Matrix4x4> bindPose = new List<Matrix4x4>(150);
        //bindPose information can be achieve from any lodInfo.skinnedMeshRenderer
        Transform[] bones = RuntimeHelper.MergeBone(lodInfo[0].skinnedMeshRenderer, bindPose);
        //Not all the bones in texture are used in MergeBone, so the output bones is a subcollection.
        //TODO: The BoneName should be save in BoneTexture in order to satisfy the order of bones.
        allTransforms = bones;
        InstancingMgr.Instance.AddMeshVertex(prototype.name,
            lodInfo,
            allTransforms,
            bindPose,
            bonePerVertex);
    }

    public class LodInfo
    {
        public int lodLevel;
        public SkinnedMeshRenderer[] skinnedMeshRenderer;
        public MeshRenderer[] meshRenderer;
        public MeshFilter[] meshFilter;
        public InstancingMgr.VertexCache[] vertexCacheList;
    }

    public LodInfo[] lodInfo;
    
    /*
     * In Fn Start, we do
     * 1. If the Obj has an Animator, then disable it.
     * 2. Import meshes of the Obj into LodInfo[]. (defautly in LodInfo[0])
     * 3. Add this obj to the InstancingMgr.
     */
    void Start()
    {
        LODGroup lod = GetComponent<LODGroup>();
        animator = GetComponent<Animator>();
        Pose = GetComponent<Transform>();
        if (InstancingMgr.Instance.UseInstancing && animator != null)
        {
            animator.enabled = false;
        }
        
        /*
         * Setup MeshInfo into LodInfo[]
         * if the Prefab only has Lod0 mesh
         */
        if (lod != null)
        {
            //if an Obj already has lodlevel model, then import it.
            lodInfo = new LodInfo[lod.lodCount];
            LOD[] lods = lod.GetLODs();
            for (int i = 0; i != lods.Length; ++i)
            {
                if (lods[i].renderers == null)
                {
                    continue;
                }

                LodInfo info = new LodInfo();
                info.lodLevel = i;
                info.vertexCacheList = new InstancingMgr.VertexCache[lods[i].renderers.Length];
                List<SkinnedMeshRenderer> listSkinnedMeshRenderer = new List<SkinnedMeshRenderer>();
                List<MeshRenderer> listMeshRenderer = new List<MeshRenderer>();
                foreach (var render in lods[i].renderers)
                {
                    if (render is SkinnedMeshRenderer)
                        listSkinnedMeshRenderer.Add((SkinnedMeshRenderer) render);
                    if (render is MeshRenderer)
                        listMeshRenderer.Add((MeshRenderer) render);
                }

                info.skinnedMeshRenderer = listSkinnedMeshRenderer.ToArray();
                info.meshRenderer = listMeshRenderer.ToArray();
                //todo, to make sure whether the MeshRenderer can be in the LOD.
                info.meshFilter = null;
                for (int j = 0; j != lods[i].renderers.Length; ++j)
                {
                    lods[i].renderers[j].enabled = false;
                }

                lodInfo[i] = info;
            }
        }
        else
        {
            //else defaul import meshes in lodlevel 0
            lodInfo = new LodInfo[1];
            LodInfo info = new LodInfo();
            info.lodLevel = 0;
            info.skinnedMeshRenderer = GetComponentsInChildren<SkinnedMeshRenderer>();
            info.meshRenderer = GetComponentsInChildren<MeshRenderer>();
            info.meshFilter = GetComponentsInChildren<MeshFilter>();
            info.vertexCacheList =
                new InstancingMgr.VertexCache[info.skinnedMeshRenderer.Length + info.meshRenderer.Length];
            lodInfo[0] = info;

            for (int j = 0; j != info.meshRenderer.Length; ++j)
            {
                info.meshRenderer[j].enabled = false;
            }

            for (int j = 0; j != info.skinnedMeshRenderer.Length; ++j)
            {
                info.skinnedMeshRenderer[j].enabled = false;
            }
        }
        InstancingMgr.Instance.AddInstance(gameObject);
    }
}