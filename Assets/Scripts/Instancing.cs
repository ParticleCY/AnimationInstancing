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
    
    public Transform worldTransform;
    
    public float playSpeed = 1.0f;
    float speedParameter = 1.0f, cacheParameter = 1.0f;
    WrapMode wrapMode;

    public WrapMode Mode
    {
        get { return wrapMode; }
        set { wrapMode = value; }
    }

    public bool IsLoop()
    {
        return Mode == WrapMode.Loop;
    }

    public bool IsPlay()
    {
        return speedParameter != 0;
    }

    public bool IsPause()
    {
        return speedParameter == 0.0f;
    }

    [Range(1, 4)] public int bonePerVertex = 4;
    [NonSerialized] public float curFrame;
    [NonSerialized] public float preAniFrame;
    [NonSerialized] public int aniIndex = -1;
    [NonSerialized] public int preAniIndex = -1;
    [NonSerialized] public int aniTextureIndex = -1;
    int preAniTextureIndex = -1;
    float transitionDuration = 0.0f;
    bool isInTransition = false;
    float transitionTimer = 0.0f;

    [NonSerialized] public float transitionProgress = 0.0f;

    //[NonSerialized]
    //public int packageIndex;
    static int aniInfoCount = 1;

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
        List<string> BoneNameIndex = InstancingMgr.Instance.BoneNameIndex;
        List<int> BoneIndex = new List<int>();
        for (int i = 0; i < bones.Length; ++i)
        {
            BoneIndex.Add(BoneNameIndex.FindIndex((predicate) =>
            {
                return bones[i].name == predicate;
            }));
        }
        InstancingMgr.Instance.AddMeshVertex(prototype.name,
            lodInfo,
            allTransforms,
            bindPose,
            bonePerVertex);

        Destroy(GetComponent<Animator>()); //启用instancing就把Animator销毁。
        PlayAnimation(0);
    }

    public void PlayAnimation(int animationIndex)
    {
        transitionDuration = 0.0f;
        transitionProgress = 1.0f;
        isInTransition = false;
        Debug.Assert(animationIndex < aniInfoCount);
        if (0 <= animationIndex && animationIndex < aniInfoCount)
        {
            preAniIndex = aniIndex;
            aniIndex = animationIndex;
            preAniFrame = (float) (int) (curFrame + 0.5f);
            curFrame = 0.0f;
            preAniTextureIndex = aniTextureIndex;
            //aniTextureIndex = aniInfo[aniIndex].textureIndex;
            //wrapMode = aniInfo[aniIndex].wrapMode;
            //因为从3dsmax中得到的BoneTexture为单clip，所以这里的aniInfoCount为1，wrapMode不停循环。
            aniTextureIndex = 0;
            wrapMode = Mode;
            speedParameter = 1.0f;
        }
        else
        {
            Debug.LogWarning("The requested animation index is out of the count.");
            return;
        }

        //RefreshAttachmentAnimation(animationIndex);
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
        worldTransform = GetComponent<Transform>();
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

    public void UpdateAnimation()
    {
    }
}