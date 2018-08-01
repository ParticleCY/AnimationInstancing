using System.Collections.Generic;
using System.IO;
using UnityEngine;

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

    private List<BoneTextureInfo> _boneTextrueInfo = new List<BoneTextureInfo>();
    private List<Instancing> _InstancingData = new List<Instancing>();

    private void OnEnable()
    {
        UseInstancing = true;
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
        render();
    }

    private void generateBoneMatrixData()
    {
        //TODO
    }

    private void render()
    {
        //TODO
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
}