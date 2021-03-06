﻿using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

public class InstancingMgr : Singleton<InstancingMgr>
{
    //One BoneTextureInfo saves the BoneTexture of a certain Prefeb with "Name"
    public struct BoneTextureInfo
    {
        public string Name;
        public int BlockWidth;
        public int BlockHeight;
        public Texture2D[] BoneTexture;
    }

    //InstanceData saves the Infos to render a certain Prefeb
    public class InstanceData
    {
        public List<Matrix4x4[]>[] WorldMatrix;
        public List<float[]>[] FrameIndex;
        public List<float[]>[] PreFrameIndex;
        public List<float[]>[] TransitionProgress;
    }

    public class VertexCache
    {
        public int nameCode;
        public Mesh mesh = null;
        public InstanceData instanceData;
        public Vector4[] weight;
        public Vector4[] boneIndex;
        public Material[] materials = null;
        public Matrix4x4[] bindPose;
        public Transform[] bonePose;
        public int boneTextureIndex = -1;

        public class InstancingPackage
        {
            public Material[] material;
            public int animationTextureIndex = 0;
            public int subMeshCount = 1;
            public int instancingCount;
            public int size;
            public MaterialPropertyBlock propertyBlock;
        }

        public int packageIndex = 0;
        public int[] runtimePackageIndex;

        // array[index base on texture][package index]
        public List<InstancingPackage>[] packageList;
    }

    private List<string> boneNameIndex;
    public List<string> BoneNameIndex
    {
        get { return boneNameIndex; }
    }

    private List<int> boneIndexMap;
    public List<int> BoneIndexMap
    {
        get { return boneIndexMap; }
        set { boneIndexMap = value; }
    }

    private bool _useInstancing = true;

    public bool UseInstancing
    {
        get { return _useInstancing; }
        set { _useInstancing = value; }
    }

    public class InstancePkg
    {
        InstanceData Data;
        Instancing Script;
    }

    public string FileName;
    public string prefebName;

    private Dictionary<int, VertexCache> vertexCachePool;
    private Dictionary<int, InstanceData> instanceDataPool;

    private List<BoneTextureInfo> _boneTextrueInfo = new List<BoneTextureInfo>();
    private List<Instancing> _InstancingData = new List<Instancing>();

    private Transform cameraTransform;

    const int InstancingSizePerPackage = 200;
    int instancingPackageSize = InstancingSizePerPackage;

    public int InstancingPackageSize
    {
        get { return instancingPackageSize; }
        set { instancingPackageSize = value; }
    }

    public GameObject CreateInstance(GameObject prefab)
    {
        Debug.Assert(prefab != null);
        GameObject obj = Instantiate(prefab, Vector3.zero, Quaternion.identity);
        Instancing script = obj.GetComponent<Instancing>();
        //AnimationInstancing prototypeScript = prefab.GetComponent<AnimationInstancing>();
        script.prototype = prefab;
        return obj;
    }

    private void OnEnable()
    {
        UseInstancing = true;
        cameraTransform = Camera.main.transform;
        vertexCachePool = new Dictionary<int, VertexCache>();
        instanceDataPool = new Dictionary<int, InstanceData>();
    }


    public void AddInstance(GameObject obj)
    {
        //TODO
        Instancing script = obj.GetComponent<Instancing>();
        script.InitializeAnimation();
        _InstancingData.Add(script);
    }

    public void DistroyInstace(Instancing instance)
    {
        //TODO
    }

    void Start()
    {
        FileStream fs = new FileStream(FileName, FileMode.Open, FileAccess.Read);
        BinaryReader reader = new BinaryReader(fs);
        LoadBoneTexture(reader);
        fs.Close();
    }

    void Update()
    {
        generateBoneMatrixData();
        Render();
    }

    private void generateBoneMatrixData()
    {
        Vector3 cameraPosition = cameraTransform.position;
        for (int i = 0; i != _InstancingData.Count; ++i) //对列表中每个对象做Instancing。
        {
            Instancing instance = _InstancingData[i];
            //if (!instance.IsPlaying())
            //    continue;
            //if (instance.aniIndex < 0 && instance.parentInstance == null)
            //    continue;

            instance.UpdateAnimation();

            //if (!instance.visible)
            //    continue;

            //here only one Lod
            //instance.UpdateLod(cameraPosition);

            //Instancing.LodInfo lod = instance.lodInfo[instance.lodLevel];
            Instancing.LodInfo lod = instance.lodInfo[0];

            int aniTextureIndex = -1;
            //if (instance.parentInstance != null)
            //    aniTextureIndex = instance.parentInstance.aniTextureIndex;
            //else
            aniTextureIndex = instance.aniTextureIndex;

            for (int j = 0; j != lod.vertexCacheList.Length; ++j)
            {
                VertexCache cache = lod.vertexCacheList[j] as VertexCache;
                int packageIndex = cache.runtimePackageIndex[aniTextureIndex];
                Debug.Assert(packageIndex < cache.packageList[aniTextureIndex].Count);
                VertexCache.InstancingPackage package = cache.packageList[aniTextureIndex][packageIndex];
                if (package.instancingCount + 1 > instancingPackageSize)
                {
                    ++cache.runtimePackageIndex[aniTextureIndex];
                    packageIndex = cache.runtimePackageIndex[aniTextureIndex];
                    if (packageIndex >= cache.packageList[aniTextureIndex].Count)
                    {
                        VertexCache.InstancingPackage newPackage = CreatePackage(cache.instanceData,
                            cache.mesh,
                            cache.materials,
                            aniTextureIndex);
                        cache.packageList[aniTextureIndex].Add(newPackage);
                        PreparePackageMaterial(newPackage, cache, aniTextureIndex);
                        newPackage.instancingCount = 1;
                    }
                }
                else
                    ++package.instancingCount;
            }

            VertexCache vertexCache = lod.vertexCacheList[0];
            InstanceData data = vertexCache.instanceData;

            int index = vertexCache.runtimePackageIndex[aniTextureIndex];
            VertexCache.InstancingPackage pkg = vertexCache.packageList[aniTextureIndex][index];
            int count = pkg.instancingCount - 1;
            if (count >= 0)
            {
                Matrix4x4 worldMat = instance.worldTransform.localToWorldMatrix;
                Matrix4x4[] arrayMat = data.WorldMatrix[aniTextureIndex][index];
                arrayMat[count].m00 = worldMat.m00;
                arrayMat[count].m01 = worldMat.m01;
                arrayMat[count].m02 = worldMat.m02;
                arrayMat[count].m03 = worldMat.m03;
                arrayMat[count].m10 = worldMat.m10;
                arrayMat[count].m11 = worldMat.m11;
                arrayMat[count].m12 = worldMat.m12;
                arrayMat[count].m13 = worldMat.m13;
                arrayMat[count].m20 = worldMat.m20;
                arrayMat[count].m21 = worldMat.m21;
                arrayMat[count].m22 = worldMat.m22;
                arrayMat[count].m23 = worldMat.m23;
                arrayMat[count].m30 = worldMat.m30;
                arrayMat[count].m31 = worldMat.m31;
                arrayMat[count].m32 = worldMat.m32;
                arrayMat[count].m33 = worldMat.m33;
                float frameIndex = 0, preFrameIndex = -1;
                //if (instance.parentInstance != null)
                //{
                //    frameIndex = instance.parentInstance.aniInfo[instance.parentInstance.aniIndex].animationIndex +
                //                 instance.parentInstance.curFrame;
                //    if (instance.parentInstance.preAniIndex >= 0)
                //        preFrameIndex =
                //            instance.parentInstance.aniInfo[instance.parentInstance.preAniIndex].animationIndex +
                //            instance.parentInstance.preAniFrame;
                //}
                //else
                //{
                //frameIndex = instance.aniInfo[instance.aniIndex].animationIndex + instance.curFrame;
                //if (instance.preAniIndex >= 0)
                //    preFrameIndex = instance.aniInfo[instance.preAniIndex].animationIndex + instance.preAniFrame;
                frameIndex = instance.curFrame;
                if (instance.preAniIndex >= 0)
                    preFrameIndex = instance.preAniFrame;
                //}

                data.FrameIndex[aniTextureIndex][index][count] = frameIndex;
                data.PreFrameIndex[aniTextureIndex][index][count] = preFrameIndex;
                data.TransitionProgress[aniTextureIndex][index][count] = instance.transitionProgress;
            }
        }
    }

    private void Render()
    {
        foreach (var obj in vertexCachePool)
        {
            VertexCache vertexCache = obj.Value;
            for (int k = 0; k != vertexCache.packageList.Length; ++k)
            {
                for (int i = 0; i != vertexCache.packageList[k].Count; ++i)
                {
                    VertexCache.InstancingPackage package = vertexCache.packageList[k][i];
                    if (package.instancingCount == 0)
                        continue;
                    for (int j = 0; j != package.subMeshCount; ++j)
                    {
                        if (UseInstancing)
                        {
#if UNITY_EDITOR
                            PreparePackageMaterial(package, vertexCache, k);
#endif
                            package.propertyBlock.SetFloatArray("frameIndex",
                                vertexCache.instanceData.FrameIndex[k][i]);
                            package.propertyBlock.SetFloatArray("preFrameIndex",
                                vertexCache.instanceData.PreFrameIndex[k][i]);
                            package.propertyBlock.SetFloatArray("transitionProgress",
                                vertexCache.instanceData.TransitionProgress[k][i]);
                            Graphics.DrawMeshInstanced(vertexCache.mesh,
                                j,
                                package.material[j],
                                vertexCache.instanceData.WorldMatrix[k][i],
                                package.instancingCount,
                                package.propertyBlock,
                                ShadowCastingMode.On,
                                true,
                                0);
                        }
                        else
                        {
                            package.material[j].SetFloat("frameIndex", vertexCache.instanceData.FrameIndex[k][i][0]);
                            package.material[j].SetFloat("preFrameIndex",
                                vertexCache.instanceData.PreFrameIndex[k][i][0]);
                            package.material[j].SetFloat("transitionProgress",
                                vertexCache.instanceData.TransitionProgress[k][i][0]);
                            Graphics.DrawMesh(vertexCache.mesh,
                                vertexCache.instanceData.WorldMatrix[k][i][0],
                                package.material[j],
                                0,
                                null,
                                j);
                        }
                    }

                    package.instancingCount = 0;
                }

                vertexCache.runtimePackageIndex[k] = 0;
            }
        }
    }

    public void clear()
    {
        _boneTextrueInfo.Clear();
    }

    //private LoadBoneTexture(BinaryReader reader, string prefebName)
    private void LoadBoneTexture(BinaryReader reader)
    {
        TextureFormat format = TextureFormat.RGBAHalf;
        int count = reader.ReadInt32();
        int blockWidth = reader.ReadInt32();
        int blockHeight = reader.ReadInt32();
        //byte[] input = new byte[100];
        //input = reader.ReadBytes(100);
        int nameLength;
        char character;
        boneNameIndex = new List<string>();
        for (int i = 0; i != blockHeight; ++i)
        {
            string boneName = "";
            nameLength = reader.ReadByte();
            for (int j = 0; j != nameLength; ++j)
            {
                character = reader.ReadChar();
                boneName = boneName + character;
            }
            boneNameIndex.Add(boneName);
            nameLength = reader.ReadByte();
        }
        BoneTextureInfo aniTexture = new BoneTextureInfo();
        aniTexture.BoneTexture = new Texture2D[count];
        aniTexture.Name = prefebName;
        aniTexture.BlockWidth = blockWidth;
        aniTexture.BlockHeight = blockHeight;
        for (int i = 0; i != count; ++i)
        {
            int textureWidth = reader.ReadInt32();
            int textureHeight = reader.ReadInt32();
            int byteLength = reader.ReadInt32();
            byte[] b = new byte[byteLength];
            b = reader.ReadBytes(byteLength);
            Texture2D texture = new Texture2D(textureWidth, textureHeight, format, false);
            texture.LoadRawTextureData(b);
            texture.filterMode = FilterMode.Point;
            texture.Apply();
            aniTexture.BoneTexture[i] = texture;
            FileStream debugfile = File.Open("D:/Format/3DsInput.txt", FileMode.Create);
            StreamWriter debugwriter = new StreamWriter(debugfile);
            for (int x = 0; x < textureWidth; x += 4)
            {
                for (int y = 0; y < textureHeight; y++)
                {
                    debugwriter.Write(string.Format(
                        "{0},{1}:[{2};{3};{4};{5}]\r\n", x, y,
                        texture.GetPixel(x, y).ToString(), texture.GetPixel(x + 1, y).ToString(),
                        texture.GetPixel(x + 2, y).ToString(),
                        texture.GetPixel(x + 3, y).ToString()));
                }
            }

            debugwriter.Close();
        }

        _boneTextrueInfo.Add(aniTexture);
    }


    // alias is to use for attachment, it's usually a bone name
    public void AddMeshVertex(string prefabName,
        Instancing.LodInfo[] lodInfo,
        Transform[] bones,
        List<Matrix4x4> bindPose,
        int bonePerVertex,
        string alias = null)
    {
        UnityEngine.Profiling.Profiler.BeginSample("AddMeshVertex()");
        for (int x = 0; x != lodInfo.Length; ++x)
        {
            Instancing.LodInfo lod = lodInfo[x];
            for (int i = 0; i != lod.skinnedMeshRenderer.Length; ++i)
            {
                Mesh m = lod.skinnedMeshRenderer[i].sharedMesh;
                if (m == null)
                    continue;

                int nameCode = lod.skinnedMeshRenderer[i].name.GetHashCode();
                VertexCache cache = null;

                //if the nameCode have been add to the dict, load it.
                if (vertexCachePool.TryGetValue(nameCode, out cache))
                {
                    lod.vertexCacheList[i] = cache;
                    continue;
                }

                //if not, create a vertexCache and add it to the pool.

                VertexCache
                    vertexCache = CreateVertexCache(prefabName, nameCode, 0, m); //仅仅是创建，包括PackageList、InstanceData都还为空。
                vertexCache.bindPose = bindPose.ToArray(); //刚刚得到的该prefab的所有sharedmesh所用到的bone的bindpose都存进去。
                lod.vertexCacheList[i] = vertexCache; //与MeshRender数一一对应。
                SetupVertexCache(vertexCache, lod.skinnedMeshRenderer[i], bones, bonePerVertex);
            }

            //for those non-skinned mesh

            for (int i = 0, j = lod.skinnedMeshRenderer.Length; i != lod.meshRenderer.Length; ++i, ++j)
            {
                Mesh m = lod.meshFilter[i].sharedMesh;
                if (m == null)
                    continue;

                int renderName = lod.meshRenderer[i].name.GetHashCode();
                int aliasName = (alias != null ? alias.GetHashCode() : 0);
                VertexCache cache = null;
                if (vertexCachePool.TryGetValue(renderName + aliasName, out cache))
                {
                    lod.vertexCacheList[j] = cache;
                    continue;
                }

                VertexCache vertexCache = CreateVertexCache(prefabName, renderName, aliasName, m);
                if (bindPose != null)
                    vertexCache.bindPose = bindPose.ToArray();
                lod.vertexCacheList[lod.skinnedMeshRenderer.Length + i] = vertexCache;
                SetupVertexCache(vertexCache, lod.meshRenderer[i], m, bones, bonePerVertex);
            }
        }

        UnityEngine.Profiling.Profiler.EndSample();
    }

    private int FindTexture_internal(string name)
    {
        for (int i = 0; i != _boneTextrueInfo.Count; ++i)
        {
            BoneTextureInfo texture = _boneTextrueInfo[i];
            if (texture.Name == name)
            {
                return i;
            }
        }

        return -1;
    }

    private VertexCache CreateVertexCache(string prefabName, int renderName, int alias, Mesh mesh)
    {
        VertexCache vertexCache = new VertexCache();
        int cacheName = renderName + alias;
        vertexCachePool[cacheName] = vertexCache;
        vertexCache.nameCode = cacheName;
        vertexCache.mesh = mesh;
        vertexCache.boneTextureIndex =
            FindTexture_internal(
                prefabName); //因为Texture不是从AnimationInfo读进来的，而是作为BoneInfo读入，所以需要在这里通过prefabName将boneTexture和vertexCache连起来。
        vertexCache.weight = new Vector4[mesh.vertexCount];
        vertexCache.boneIndex = new Vector4[mesh.vertexCount];
        int packageCount = 1;
        if (vertexCache.boneTextureIndex >= 0) //如果找到了boneTexture，那么packageCount装载为该prefab的BoneTexture数。
        {
            BoneTextureInfo texture = _boneTextrueInfo[vertexCache.boneTextureIndex];
            packageCount = texture.BoneTexture.Length;
        }

        //建立了packageList，也是空的。

        vertexCache.packageList = new List<VertexCache.InstancingPackage>[packageCount];
        for (int i = 0; i != vertexCache.packageList.Length; ++i)
        {
            vertexCache.packageList[i] = new List<VertexCache.InstancingPackage>();
        }

        vertexCache.runtimePackageIndex = new int[packageCount];

        InstanceData data = null;
        int instanceName = prefabName.GetHashCode() + alias;

        //这里往instanceDataPool里面加入了一个空的data，只有Instancename。,并且创建只进行到List<float[]>[]的List层次，往List里面注入float[]在CreatPackage里面完成。
        if (!instanceDataPool.TryGetValue(instanceName, out data))
        {
            data = new InstanceData();
            data.WorldMatrix = new List<Matrix4x4[]>[packageCount];
            data.FrameIndex = new List<float[]>[packageCount];
            data.PreFrameIndex = new List<float[]>[packageCount];
            data.TransitionProgress = new List<float[]>[packageCount];
            for (int i = 0; i != packageCount; ++i)
            {
                data.WorldMatrix[i] = new List<Matrix4x4[]>();
                data.FrameIndex[i] = new List<float[]>();
                data.PreFrameIndex[i] = new List<float[]>();
                data.TransitionProgress[i] = new List<float[]>();
            }

            instanceDataPool.Add(instanceName, data);
        }

        vertexCache.instanceData = data;

        return vertexCache;
    }

    private void SetupVertexCache(VertexCache vertexCache,
        SkinnedMeshRenderer render,
        Transform[] boneTransform,
        int bonePerVertex)
    {
        int[] boneIndex = null;
        if (render.bones.Length != boneTransform.Length)
        {
            if (render.bones.Length == 0)
            {
                boneIndex = new int[1];
                int hashRenderParentName = render.transform.parent.name.GetHashCode();
                for (int k = 0; k != boneTransform.Length; ++k)
                {
                    if (hashRenderParentName == boneTransform[k].name.GetHashCode())
                    {
                        boneIndex[0] = k;
                        break;
                    }
                }
            }
            else
            {
                boneIndex = new int[render.bones.Length];
                for (int j = 0; j != render.bones.Length; ++j)
                {
                    boneIndex[j] = -1;
                    Transform trans = render.bones[j];
                    int hashTransformName = trans.name.GetHashCode();
                    for (int k = 0; k != boneTransform.Length; ++k)
                    {
                        if (hashTransformName == boneTransform[k].name.GetHashCode())
                        {
                            boneIndex[j] = k;
                            break;
                        }
                    }
                }

                if (boneIndex.Length == 0)
                {
                    boneIndex = null;
                }
            }
        }

        UnityEngine.Profiling.Profiler.BeginSample("Copy the vertex data in SetupVertexCache()");
        Mesh m = render.sharedMesh;
        BoneWeight[] boneWeights = m.boneWeights;
        Debug.Assert(boneWeights.Length > 0);

        for (int j = 0; j != m.vertexCount; ++j)
        {
            vertexCache.weight[j].x = boneWeights[j].weight0;
            Debug.Assert(vertexCache.weight[j].x > 0.0f);
            vertexCache.weight[j].y = boneWeights[j].weight1;
            vertexCache.weight[j].z = boneWeights[j].weight2;
            vertexCache.weight[j].w = boneWeights[j].weight3;
            vertexCache.boneIndex[j].x
                = boneIndex == null ? boneWeights[j].boneIndex0 : boneIndex[boneWeights[j].boneIndex0];
            vertexCache.boneIndex[j].y
                = boneIndex == null ? boneWeights[j].boneIndex1 : boneIndex[boneWeights[j].boneIndex1];
            vertexCache.boneIndex[j].z
                = boneIndex == null ? boneWeights[j].boneIndex2 : boneIndex[boneWeights[j].boneIndex2];
            vertexCache.boneIndex[j].w
                = boneIndex == null ? boneWeights[j].boneIndex3 : boneIndex[boneWeights[j].boneIndex3];
            Debug.Assert(vertexCache.boneIndex[j].x >= 0);
            if (bonePerVertex == 3)
            {
                float rate = 1.0f / (vertexCache.weight[j].x + vertexCache.weight[j].y + vertexCache.weight[j].z);
                vertexCache.weight[j].x = vertexCache.weight[j].x * rate;
                vertexCache.weight[j].y = vertexCache.weight[j].y * rate;
                vertexCache.weight[j].z = vertexCache.weight[j].z * rate;
                vertexCache.weight[j].w = -0.1f;
            }
            else if (bonePerVertex == 2)
            {
                float rate = 1.0f / (vertexCache.weight[j].x + vertexCache.weight[j].y);
                vertexCache.weight[j].x = vertexCache.weight[j].x * rate;
                vertexCache.weight[j].y = vertexCache.weight[j].y * rate;
                vertexCache.weight[j].z = -0.1f;
                vertexCache.weight[j].w = -0.1f;
            }
            else if (bonePerVertex == 1)
            {
                vertexCache.weight[j].x = 1.0f;
                vertexCache.weight[j].y = -0.1f;
                vertexCache.weight[j].z = -0.1f;
                vertexCache.weight[j].w = -0.1f;
            }
        }

        UnityEngine.Profiling.Profiler.EndSample();

        if (vertexCache.materials == null)
            vertexCache.materials = render.sharedMaterials;


        SetupAdditionalData(vertexCache);

        for (int i = 0; i != vertexCache.packageList.Length; ++i)
        {
            VertexCache.InstancingPackage package =
                CreatePackage(vertexCache.instanceData, vertexCache.mesh, render.sharedMaterials, i);
            vertexCache.packageList[i].Add(package);
            PreparePackageMaterial(package, vertexCache, i);
        }
    }

    //SetupVertexCache(vertexCache, lod.meshRenderer[i], m, bones, bonePerVertex);
    private void SetupVertexCache(VertexCache vertexCache,
        MeshRenderer render,
        Mesh mesh,
        Transform[] boneTransform,
        int bonePerVertex)
    {
        int boneIndex = -1;
        if (boneTransform != null)
        {
            for (int k = 0; k != boneTransform.Length; ++k)
            {
                //找到vertexCache对应render的parent在boneTransform中的对应序号。
                if (render.transform.parent.name.GetHashCode() == boneTransform[k].name.GetHashCode())
                {
                    boneIndex = k;
                    break;
                }
            }
        }

        if (boneIndex >= 0)
        {
            BindAttachment(vertexCache, vertexCache.mesh, boneIndex);
        }


        if (vertexCache.materials == null)
            vertexCache.materials =
                render.sharedMaterials; //材质从render.sharedMaterials里面获得，来自于prefab的materials，故需要在prefab中就对Materials更改为InstacingShader。

        SetupAdditionalData(vertexCache);
        for (int i = 0; i != vertexCache.packageList.Length; ++i)
        {
            VertexCache.InstancingPackage package =
                CreatePackage(vertexCache.instanceData, vertexCache.mesh, render.sharedMaterials, i);
            vertexCache.packageList[i].Add(package);
            PreparePackageMaterial(package, vertexCache, i);
        }
    }

    public void BindAttachment(VertexCache cache, Mesh sharedMesh, int boneIndex)
    {
        Matrix4x4 mat = cache.bindPose[boneIndex].inverse;
        cache.mesh = Instantiate(sharedMesh);
        Vector3 offset = mat.GetColumn(3);
        Quaternion q = RuntimeHelper.QuaternionFromMatrix(mat);
        Vector3[] vertices = cache.mesh.vertices;
        for (int k = 0; k != cache.mesh.vertexCount; ++k)
        {
            vertices[k] = q * vertices[k];
            vertices[k] = vertices[k] + offset;
        }

        cache.mesh.vertices = vertices;

        for (int j = 0; j != cache.mesh.vertexCount; ++j)
        {
            cache.weight[j].x = 1.0f;
            cache.weight[j].y = -0.1f;
            cache.weight[j].z = -0.1f;
            cache.weight[j].w = -0.1f;
            cache.boneIndex[j].x = boneIndex;
        }
    }

    public void SetupAdditionalData(VertexCache vertexCache)
    {
        Color[] colors = new Color[vertexCache.weight.Length];
        for (int i = 0; i != colors.Length; ++i)
        {
            colors[i].r = vertexCache.weight[i].x;
            colors[i].g = vertexCache.weight[i].y;
            colors[i].b = vertexCache.weight[i].z;
            colors[i].a = vertexCache.weight[i].w;
        }

        vertexCache.mesh.colors = colors;

        List<Vector4> uv2 = new List<Vector4>(vertexCache.boneIndex.Length);
        for (int i = 0; i != vertexCache.boneIndex.Length; ++i)
        {
            uv2.Add(vertexCache.boneIndex[i]);
        }

        vertexCache.mesh.SetUVs(2, uv2);
        vertexCache.mesh.UploadMeshData(false);
    }

    //给shader传入参数？
    public void PreparePackageMaterial(VertexCache.InstancingPackage package, VertexCache vertexCache,
        int aniTextureIndex)
    {
        if (vertexCache.boneTextureIndex < 0)
            return;

        for (int i = 0; i != package.subMeshCount; ++i)
        {
            BoneTextureInfo texture = _boneTextrueInfo[vertexCache.boneTextureIndex];
            package.material[i].SetTexture("_boneTexture", texture.BoneTexture[aniTextureIndex]);
            package.material[i].SetInt("_boneTextureWidth", texture.BoneTexture[aniTextureIndex].width);
            package.material[i].SetInt("_boneTextureHeight", texture.BoneTexture[aniTextureIndex].height);
            package.material[i].SetInt("_boneTextureBlockWidth", texture.BlockWidth);
            package.material[i].SetInt("_boneTextureBlockHeight", texture.BlockHeight);
        }
    }

    /*
    public class InstanceData
    {
        public List<Matrix4x4[]>[] WorldMatrix;
        public List<float[]>[] FrameIndex;
        public List<float[]>[] PreFrameIndex;
        public List<float[]>[] TransitionProgress;
    }
    public class InstancingPackage
    {
        public Material[] material;
        public int animationTextureIndex = 0;
        public int subMeshCount = 1;
        public int instancingCount;
        public int size;
        public MaterialPropertyBlock propertyBlock;
    }
    */
    public VertexCache.InstancingPackage CreatePackage(InstanceData data, Mesh mesh, Material[] originalMaterial,
        int index)
    {
        VertexCache.InstancingPackage package = new VertexCache.InstancingPackage();
        package.material = new Material[mesh.subMeshCount];
        package.subMeshCount = mesh.subMeshCount;
        package.size = 1;
        for (int i = 0; i != mesh.subMeshCount; ++i)
        {
            package.material[i] = new Material(originalMaterial[i]);
            //package.material[i].name = "AniInstancing";
            package.material[i].enableInstancing = UseInstancing;
            if (UseInstancing)
                package.material[i].EnableKeyword("INSTANCING_ON");
            else
                package.material[i].DisableKeyword("INSTANCING_ON");

            package.propertyBlock = new MaterialPropertyBlock();
            package.material[i].EnableKeyword("USE_CONSTANT_BUFFER");
            package.material[i].DisableKeyword("USE_COMPUTE_BUFFER");
        }

        Matrix4x4[] mat = new Matrix4x4[instancingPackageSize];
        float[] frameIndex = new float[instancingPackageSize];
        float[] preFrameIndex = new float[instancingPackageSize];
        float[] transitionProgress = new float[instancingPackageSize];
        data.WorldMatrix[index].Add(mat);
        data.FrameIndex[index].Add(frameIndex);
        data.PreFrameIndex[index].Add(preFrameIndex);
        data.TransitionProgress[index].Add(transitionProgress);
        return package;
    }
}